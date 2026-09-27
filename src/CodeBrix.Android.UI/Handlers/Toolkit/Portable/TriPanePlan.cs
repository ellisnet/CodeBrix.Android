using System.Collections.Generic;
using CodeBrix.Android.UI.Policy;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The four weights of a TriPaneView (its SidePanePercent, StackPercent, UpperPanePercent and LowerPanePercent).</summary>
/// <param name="Side">The side pane's weight.</param>
/// <param name="Stack">The stack's weight.</param>
/// <param name="Upper">The upper pane's weight.</param>
/// <param name="Lower">The lower pane's weight.</param>
internal readonly record struct TriPaneWeights(double Side, double Stack, double Upper, double Lower)
{
    /// <summary>Whether a weight means "minimized" (zero, negative or not a number - the Core engine's reading).</summary>
    /// <param name="weight">The weight.</param>
    /// <returns>True when the region is minimized.</returns>
    internal static bool IsClosed(double weight) => !(weight > 0d);

    /// <summary>The weight of a region.</summary>
    /// <param name="region">The region.</param>
    /// <returns>Its weight.</returns>
    internal double Of(TriPaneRegion region) => region switch
    {
        TriPaneRegion.Side => Side,
        TriPaneRegion.Stack => Stack,
        TriPaneRegion.Upper => Upper,
        _ => Lower,
    };

    /// <summary>The same weights with one region's weight replaced.</summary>
    /// <param name="region">The region.</param>
    /// <param name="weight">Its new weight.</param>
    /// <returns>The new weights.</returns>
    internal TriPaneWeights With(TriPaneRegion region, double weight) => region switch
    {
        TriPaneRegion.Side => this with { Side = weight },
        TriPaneRegion.Stack => this with { Stack = weight },
        TriPaneRegion.Upper => this with { Upper = weight },
        _ => this with { Lower = weight },
    };
}

/// <summary>The four weighted regions of a TriPaneView (the side pane and the stack across; the upper and lower pane down the stack).</summary>
internal enum TriPaneRegion
{
    /// <summary>The full-height side pane.</summary>
    Side,

    /// <summary>The stack (upper and lower pane).</summary>
    Stack,

    /// <summary>The stack's upper pane.</summary>
    Upper,

    /// <summary>The stack's lower pane.</summary>
    Lower,
}

/// <summary>
/// The adaptive form of ONE TriPaneView (adaptive table row "TriPaneView", AdaptivePolicy.TriPane), as weights: pure C#, no
/// Android or XAML type, so it is tested host-free against the Core engine itself. The form is reached THROUGH the Core
/// engine: a region the window is too small for gets weight 0 (exactly what a divider dragged all the way over does, so
/// Core's engine minimizes it and - under RestoreGripMode Auto or Always - leaves its restore grip on the divider for the
/// user), and the weight it had is remembered and written back when the window is wide enough again.
/// <list type="bullet">
/// <item>Expanded (<see cref="TriPaneForm.ThreePanes"/>): every region this plan closed and that is still closed gets its
/// weight back; nothing else is touched.</item>
/// <item>Medium (<see cref="TriPaneForm.SidePaneAndOneStacked"/>): the side pane and ONE stacked pane: when both the upper
/// and the lower pane are open, the one not chosen closes (chosen = the one the user or the app opened last, else the
/// upper pane). The side axis is left as it is (a region closed on it in Compact reopens).</item>
/// <item>Compact (<see cref="TriPaneForm.OnePane"/>): one pane: on the side axis either the side pane or the stack (chosen =
/// the one opened last, else the stack), and one stacked pane as in Medium. The divider's restore grip switches panes: the
/// engine reopens the pane behind the grip, and the plan closes its sibling.</item>
/// </list>
/// A region the app closed itself (weight 0 that this plan did not write) is never reopened by the plan. A region this
/// plan closed and that something else reopened (a grip, the app) gets the weight it had before the plan closed it.
/// </summary>
internal sealed class TriPanePlan
{
    private readonly Dictionary<TriPaneRegion, double> _closed = new();
    private TriPaneWeights? _last;

    /// <summary>The regions this plan closed, with the weight each had (for diagnostics and fences).</summary>
    internal IReadOnlyDictionary<TriPaneRegion, double> ClosedByPlan => _closed;

    /// <summary>The form the plan applied last (null before the first <see cref="Apply"/>).</summary>
    internal TriPaneForm? Form { get; private set; }

    /// <summary>
    /// Works out the weights for <paramref name="form"/> from the control's <paramref name="current"/> weights. Call it after
    /// every change of the form or of a weight (never while a divider drag is in progress), write back the result when it
    /// differs from <paramref name="current"/>, and call <see cref="Observe"/> with what the control holds afterwards.
    /// </summary>
    /// <param name="form">The form the window size class asks for.</param>
    /// <param name="current">The control's weights now.</param>
    /// <returns>The weights the control should hold.</returns>
    internal TriPaneWeights Apply(TriPaneForm form, TriPaneWeights current)
    {
        var previous = _last ?? current;
        var target = current;
        Form = form;

        // A region the plan closed that is open again (a restore grip, the app): it gets the weight it had.
        foreach (var region in new[] { TriPaneRegion.Side, TriPaneRegion.Stack, TriPaneRegion.Upper, TriPaneRegion.Lower })
        {
            if (_closed.TryGetValue(region, out var weight) && !TriPaneWeights.IsClosed(current.Of(region)))
            {
                target = target.With(region, weight);
                _closed.Remove(region);
            }
        }

        // Regions the form has room for again: back to their weights.
        if (form == TriPaneForm.ThreePanes)
        {
            target = ReopenAll(target, TriPaneRegion.Side, TriPaneRegion.Stack, TriPaneRegion.Upper, TriPaneRegion.Lower);
        }
        else if (form == TriPaneForm.SidePaneAndOneStacked)
        {
            target = ReopenAll(target, TriPaneRegion.Side, TriPaneRegion.Stack);
        }

        // One region per constrained axis.
        if (form == TriPaneForm.OnePane)
        {
            target = KeepOne(target, previous, TriPaneRegion.Stack, TriPaneRegion.Side);
        }

        if (form != TriPaneForm.ThreePanes)
        {
            target = KeepOne(target, previous, TriPaneRegion.Upper, TriPaneRegion.Lower);
        }

        return target;
    }

    /// <summary>Records the weights the control holds after an <see cref="Apply"/> (what "opened last" is measured from).</summary>
    /// <param name="weights">The control's weights.</param>
    internal void Observe(TriPaneWeights weights) => _last = weights;

    private TriPaneWeights ReopenAll(TriPaneWeights target, params TriPaneRegion[] regions)
    {
        foreach (var region in regions)
        {
            if (_closed.TryGetValue(region, out var weight))
            {
                if (TriPaneWeights.IsClosed(target.Of(region)))
                {
                    target = target.With(region, weight);
                }

                _closed.Remove(region);
            }
        }

        return target;
    }

    /// <summary>Closes one of the two regions of an axis when both are open: the one not chosen.</summary>
    private TriPaneWeights KeepOne(TriPaneWeights target, TriPaneWeights previous, TriPaneRegion preferred, TriPaneRegion other)
    {
        if (TriPaneWeights.IsClosed(target.Of(preferred)) || TriPaneWeights.IsClosed(target.Of(other)))
        {
            return target;
        }

        // Chosen: the region that was opened since the last observation; when neither (or both) was, the preferred one.
        var preferredOpened = TriPaneWeights.IsClosed(previous.Of(preferred));
        var otherOpened = TriPaneWeights.IsClosed(previous.Of(other));
        var close = otherOpened && !preferredOpened ? preferred : other;
        _closed[close] = target.Of(close);
        return target.With(close, 0d);
    }
}
