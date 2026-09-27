using System.Linq;
using CodeBrix.Platform.UI.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Toolkit.Android;

/// <summary>
/// The Android side's door into the Toolkit Core's TriPaneView ENGINE (WPE1 C12; the TriPaneView's internal drag entry
/// points, which forward to Engine/TriPaneLayoutState): a divider drag that Android's own touch handling recognized
/// (CodeBrix.Android.UI's TriPaneViewHandler: a finger inside the divider's 48-dp touch target) is started, advanced and
/// completed HERE, exactly as the control's own divider does it for a Core pointer - the pixel lengths of the two panes on
/// the divider's axis when the drag starts, the change since the previous move, the net travel and the cancel flag - so the
/// Core engine resolves it (tap versus drag, drag-to-minimize, restore by grip, roll-back on cancel) and raises
/// DividerDragCompleted. Nothing of the engine is re-implemented. The divider shows its pressed state through its IsDragging
/// property for the whole gesture.
/// </summary>
/// <remarks>
/// The divider's own public DragStarted / DragDelta / DragCompleted events are NOT raised for a drag recognized here: the
/// divider has no platform entry points of its own (Thumb has RaiseDrag*FromPlatform; TriPaneViewDivider's RaiseDrag* use the
/// drag origin that only its Core pointer handling sets, so a platform drag would report a stale travel). FIXLIST
/// [AP7-B TriPaneView] PLATFORM line.
/// </remarks>
internal static class TriPaneViewEntryPoints
{
    private const string SideDividerPartName = "PART_SideDivider";
    private const string StackDividerPartName = "PART_StackDivider";
    private const string SidePaneScrollViewerPartName = "PART_SidePaneScrollViewer";
    private const string UpperPaneScrollViewerPartName = "PART_UpperPaneScrollViewer";
    private const string LowerPaneScrollViewerPartName = "PART_LowerPaneScrollViewer";
    private const string StackGridPartName = "PART_StackGrid";

    /// <summary>The divider template part of a kind (null before the template is applied).</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <returns>The divider, or null.</returns>
    internal static TriPaneViewDivider Divider(TriPaneView view, TriPaneViewDividerKind kind) =>
        Part<TriPaneViewDivider>(view, kind == TriPaneViewDividerKind.Side ? SideDividerPartName : StackDividerPartName);

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

    /// <summary>
    /// Starts a divider drag: the lengths of the two panes on its axis (left/upper first), as TriPaneView's own divider
    /// handler measures them, go to the engine; the divider shows its pressed state.
    /// </summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    internal static void StartDrag(TriPaneView view, TriPaneViewDividerKind kind)
    {
        double first;
        double second;
        if (kind == TriPaneViewDividerKind.Side)
        {
            var isPlacedLeft = view.SidePanePlacement == TriPaneViewSidePanePlacement.Left;
            var sideLength = Part<FrameworkElement>(view, SidePaneScrollViewerPartName)?.ActualWidth ?? 0d;
            var stackLength = Part<FrameworkElement>(view, StackGridPartName)?.ActualWidth ?? 0d;
            first = isPlacedLeft ? sideLength : stackLength;
            second = isPlacedLeft ? stackLength : sideLength;
        }
        else
        {
            first = Part<FrameworkElement>(view, UpperPaneScrollViewerPartName)?.ActualHeight ?? 0d;
            second = Part<FrameworkElement>(view, LowerPaneScrollViewerPartName)?.ActualHeight ?? 0d;
        }

        Divider(view, kind)?.SetValue(TriPaneViewDivider.IsDraggingProperty, true);
        view.StartDividerDrag(kind, first, second);
    }

    /// <summary>Advances a drag by the move since the previous one (DIPs along the divider's axis).</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <param name="delta">The change in DIPs (positive = away from the left/upper pane).</param>
    internal static void UpdateDrag(TriPaneView view, TriPaneViewDividerKind kind, double delta)
    {
        // The same gate as TriPaneView's own DragDelta handlers: a divider the app does not let the user drag only
        // answers taps (restore grip).
        var canDrag = kind == TriPaneViewDividerKind.Side ? view.CanUserDragSideDivider : view.CanUserDragStackDivider;
        if (canDrag)
        {
            view.UpdateDividerDrag(kind, delta);
        }
    }

    /// <summary>Ends a drag; the engine decides tap / drag / cancel and raises DividerDragCompleted when the layout changed.</summary>
    /// <param name="view">The control.</param>
    /// <param name="kind">The divider.</param>
    /// <param name="totalTravel">The net travel in DIPs along the divider's axis.</param>
    /// <param name="canceled">Whether the gesture was cancelled.</param>
    internal static void CompleteDrag(TriPaneView view, TriPaneViewDividerKind kind, double totalTravel, bool canceled)
    {
        Divider(view, kind)?.SetValue(TriPaneViewDivider.IsDraggingProperty, false);
        view.CompleteDividerDrag(kind, totalTravel, canceled);
    }

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

    private static T Part<T>(TriPaneView view, string name)
        where T : FrameworkElement
    {
        if (view == null || VisualTreeHelper.GetChildrenCount(view) == 0 || VisualTreeHelper.GetChild(view, 0) is not FrameworkElement root)
        {
            return null;
        }

        // The template's own namescope: the parts are found by name from the template root (not in a nested TriPaneView).
        return root.FindName(name) as T ?? Descendants(root).OfType<T>().FirstOrDefault(e => e.Name == name && Owner(e) == view);
    }

    private static TriPaneView Owner(FrameworkElement element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is TriPaneView owner)
            {
                return owner;
            }
        }

        return null;
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
