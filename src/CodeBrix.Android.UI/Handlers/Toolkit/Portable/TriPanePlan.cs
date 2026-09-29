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
/// The adaptive form of ONE TriPaneView (adaptive table row "TriPaneView", AdaptivePolicy.TriPane) as the weights to
/// DISPLAY (AP1.12: the answer of the Toolkit Core's display override, WPE1-13 ITriPaneDisplayOverride): pure C#, no
/// Android or XAML type, so it is tested host-free against the Core engine itself. The application's weights (the four
/// percent properties) and its minimized flags are NEVER written (D-P7B-TP-4 closed): a region the window is too small
/// for is displayed with weight 0, so the engine lays it out minimized with its restore grip (RestoreGripMode Auto or
/// Always), and a tap on that grip asks <see cref="Restore"/> to show it instead of its sibling.
/// <list type="bullet">
/// <item>Expanded (<see cref="TriPaneForm.ThreePanes"/>) or no form: the application's weights, unchanged.</item>
/// <item>Medium (<see cref="TriPaneForm.SidePaneAndOneStacked"/>): the side axis as the application has it, and ONE stacked
/// pane when both the upper and the lower pane are open (chosen = the one the user or the app opened last or restored
/// by its grip, else the upper pane).</item>
/// <item>Compact (<see cref="TriPaneForm.OnePane"/>): one pane: on the side axis either the side pane or the stack (chosen as
/// above, else the stack), and one stacked pane as in Medium. The restore grips switch panes.</item>
/// </list>
/// A region the application closed itself (weight 0) stays closed in every form: the plan only ever hides more.
/// </summary>
internal sealed class TriPanePlan
{
    private TriPaneRegion _sideAxisChoice = TriPaneRegion.Stack;
    private TriPaneRegion _stackAxisChoice = TriPaneRegion.Upper;
    private TriPaneWeights? _lastApplication;

    /// <summary>The form the window size class asks for (null = display the application's weights).</summary>
    internal TriPaneForm? Form { get; set; }

    /// <summary>The region shown on the side axis when only one fits (Side or Stack).</summary>
    internal TriPaneRegion SideAxisChoice => _sideAxisChoice;

    /// <summary>The stacked pane shown when only one fits (Upper or Lower).</summary>
    internal TriPaneRegion StackAxisChoice => _stackAxisChoice;

    /// <summary>
    /// The weights to display for the application's weights (called by the engine on every state pass). A region the
    /// application opened since the previous call becomes its axis's choice.
    /// </summary>
    /// <param name="application">The application's weights.</param>
    /// <returns>The weights to lay out.</returns>
    internal TriPaneWeights Display(TriPaneWeights application)
    {
        if (_lastApplication is { } last)
        {
            _sideAxisChoice = OpenedLast(last, application, TriPaneRegion.Side, TriPaneRegion.Stack, _sideAxisChoice);
            _stackAxisChoice = OpenedLast(last, application, TriPaneRegion.Upper, TriPaneRegion.Lower, _stackAxisChoice);
        }

        _lastApplication = application;
        if (Form is not { } form || form == TriPaneForm.ThreePanes)
        {
            return application;
        }

        var display = application;
        if (form == TriPaneForm.OnePane)
        {
            display = KeepOne(display, _sideAxisChoice, TriPaneRegion.Side, TriPaneRegion.Stack);
        }

        return KeepOne(display, _stackAxisChoice, TriPaneRegion.Upper, TriPaneRegion.Lower);
    }

    /// <summary>A tap on the restore grip of a region this plan hides: show that region instead of its sibling.</summary>
    /// <param name="region">The hidden region.</param>
    /// <returns>True when the display changed.</returns>
    internal bool Restore(TriPaneRegion region)
    {
        if (Form is not { } form || form == TriPaneForm.ThreePanes)
        {
            return false;
        }

        if (region is TriPaneRegion.Side or TriPaneRegion.Stack)
        {
            var changed = _sideAxisChoice != region;
            _sideAxisChoice = region;
            return changed;
        }

        var stackChanged = _stackAxisChoice != region;
        _stackAxisChoice = region;
        return stackChanged;
    }

    private static TriPaneRegion OpenedLast(TriPaneWeights before, TriPaneWeights now, TriPaneRegion a, TriPaneRegion b, TriPaneRegion choice)
    {
        var aOpened = TriPaneWeights.IsClosed(before.Of(a)) && !TriPaneWeights.IsClosed(now.Of(a));
        var bOpened = TriPaneWeights.IsClosed(before.Of(b)) && !TriPaneWeights.IsClosed(now.Of(b));
        return aOpened && !bOpened ? a : bOpened && !aOpened ? b : choice;
    }

    /// <summary>Hides one of the two regions of an axis when both are open: the one not chosen.</summary>
    private static TriPaneWeights KeepOne(TriPaneWeights display, TriPaneRegion choice, TriPaneRegion a, TriPaneRegion b)
    {
        if (TriPaneWeights.IsClosed(display.Of(a)) || TriPaneWeights.IsClosed(display.Of(b)))
        {
            return display;
        }

        return display.With(choice == a ? b : a, 0d);
    }
}
