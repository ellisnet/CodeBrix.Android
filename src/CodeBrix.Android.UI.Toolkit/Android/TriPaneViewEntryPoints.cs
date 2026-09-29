using System;
using CodeBrix.Platform.UI.Toolkit;
using CodeBrix.Platform.UI.Toolkit.Engine;
using CodeBrix.Platform.UI.Toolkit.Internal;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Toolkit.Android;

/// <summary>
/// The Android side's door into the Toolkit Core's TriPaneView (its internal seams; this assembly has the Core's
/// InternalsVisibleTo grant, CodeBrix.Android.UI has not):
/// <list type="bullet">
/// <item>DIVIDER DRAGS (AP1.12, WPE1-13 item e): a divider drag that Android's own touch handling recognized
/// (CodeBrix.Android.UI's TriPaneViewHandler: a finger inside the divider's 48-dp touch target) is raised through the
/// divider's own platform entry points (RaiseDragStarted/Delta/CompletedFromPlatform), so the divider raises its public
/// DragStarted / DragDelta / DragCompleted exactly as for a Core pointer, TriPaneView's own divider handlers measure the
/// panes and gate CanUserDrag*, and the engine resolves tap versus drag, drag-to-minimize, restore by grip and roll-back
/// on cancel. The divider shows its pressed state (IsDragging) for the whole gesture.</item>
/// <item>THE DISPLAY OVERRIDE (AP1.12, WPE1-13 item f): the adaptive form by window size class is the weights the control
/// DISPLAYS (ITriPaneDisplayOverride); the application's percent and IsMinimized properties are never written.</item>
/// </list>
/// Regions cross this door as indexes in the order Side, Stack, Upper, Lower (CodeBrix.Android.UI's TriPaneRegion).
/// </summary>
internal static class TriPaneViewEntryPoints
{
    /// <summary>The divider of a kind (null before the template is applied).</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <returns>The divider, or null.</returns>
    internal static TriPaneViewDivider Divider(TriPaneView view, TriPaneViewDividerKind kind) => view?.GetDivider(kind);

    /// <summary>Whether a divider takes a finger now: shown, enabled, and either draggable or a restore grip.</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <returns>True when a finger on it starts a drag.</returns>
    internal static bool AcceptsTouch(TriPaneView view, TriPaneViewDividerKind kind) =>
        Divider(view, kind) is { } divider
        && divider.Visibility == Visibility.Visible
        && divider.IsEnabled
        && divider.ActualWidth > 0
        && divider.ActualHeight > 0;

    /// <summary>Starts a divider drag through the divider's platform entry point (ignored while one runs).</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    internal static void StartDrag(TriPaneView view, TriPaneViewDividerKind kind) => Divider(view, kind)?.RaiseDragStartedFromPlatform();

    /// <summary>Advances a drag by the move since the previous one (DIPs along the divider's axis).</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <param name="delta">The change in DIPs (positive = away from the left/upper pane).</param>
    internal static void UpdateDrag(TriPaneView view, TriPaneViewDividerKind kind, double delta)
    {
        if (kind == TriPaneViewDividerKind.Side)
        {
            Divider(view, kind)?.RaiseDragDeltaFromPlatform(delta, 0d);
        }
        else
        {
            Divider(view, kind)?.RaiseDragDeltaFromPlatform(0d, delta);
        }
    }

    /// <summary>Ends a drag; the divider reports the sum of this gesture's deltas and the engine decides tap / drag / cancel.</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <param name="canceled">Whether the gesture was cancelled.</param>
    internal static void CompleteDrag(TriPaneView view, TriPaneViewDividerKind kind, bool canceled) =>
        Divider(view, kind)?.RaiseDragCompletedFromPlatform(canceled);

    /// <summary>
    /// Sets (or, with null delegates, removes) the control's display override. <paramref name="display"/> maps the
    /// application's weights to the weights to lay out; <paramref name="restore"/> answers a tap on the restore grip of a
    /// region the override hides (region index Side 0, Stack 1, Upper 2, Lower 3) and returns whether the display changed.
    /// </summary>
    /// <param name="view">The control.</param>
    /// <param name="display">The weights to display for the application's weights.</param>
    /// <param name="restore">The grip-tap answer.</param>
    internal static void SetDisplayOverride(
        TriPaneView view,
        Func<(double Side, double Stack, double Upper, double Lower), (double Side, double Stack, double Upper, double Lower)> display,
        Func<int, bool> restore)
    {
        if (view != null)
        {
            view.DisplayOverride = display == null ? null : new DisplayOverride(display, restore);
        }
    }

    /// <summary>Whether the control has a display override (diagnostics and fences).</summary>
    /// <param name="view">The control.</param>
    /// <returns>True when one is set.</returns>
    internal static bool HasDisplayOverride(TriPaneView view) => view?.DisplayOverride != null;

    /// <summary>Runs the engine's state pass after the display override's answer changed (a new size class).</summary>
    /// <param name="view">The control.</param>
    internal static void RefreshDisplayOverride(TriPaneView view) => view?.RefreshDisplayOverride();

    /// <summary>What the engine's last state pass laid out (diagnostics and the device fences).</summary>
    /// <param name="view">The control.</param>
    /// <returns>A one-line description.</returns>
    internal static string Describe(TriPaneView view) =>
        $"effective side={view.SidePaneEffectiveWeight:0.##} stack={view.StackEffectiveWeight:0.##} upper={view.UpperPaneEffectiveWeight:0.##} "
        + $"lower={view.LowerPaneEffectiveWeight:0.##}; side divider visible={view.IsSideDividerVisible} grip={view.IsSideRestoreGripVisible}; "
        + $"stack divider visible={view.IsStackDividerVisible} grip={view.IsStackRestoreGripVisible}";

    /// <summary>Whether the engine shows a region's divider as a restore grip.</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <returns>True for a restore grip.</returns>
    internal static bool IsRestoreGrip(TriPaneView view, TriPaneViewDividerKind kind) =>
        kind == TriPaneViewDividerKind.Side ? view.IsSideRestoreGripVisible : view.IsStackRestoreGripVisible;

    /// <summary>The Core display override over the two delegates.</summary>
    private sealed class DisplayOverride : ITriPaneDisplayOverride
    {
        private readonly Func<(double Side, double Stack, double Upper, double Lower), (double Side, double Stack, double Upper, double Lower)> _display;
        private readonly Func<int, bool> _restore;

        internal DisplayOverride(
            Func<(double Side, double Stack, double Upper, double Lower), (double Side, double Stack, double Upper, double Lower)> display,
            Func<int, bool> restore)
        {
            _display = display;
            _restore = restore;
        }

        public TriPaneDisplayWeights GetDisplayWeights(TriPaneDisplayWeights applicationWeights)
        {
            var (side, stack, upper, lower) = _display((applicationWeights.Side, applicationWeights.Stack, applicationWeights.Upper, applicationWeights.Lower));
            return new TriPaneDisplayWeights(side, stack, upper, lower);
        }

        public bool RestoreRequested(TriPaneViewRegion region) => _restore != null && _restore((int)region);
    }
}
