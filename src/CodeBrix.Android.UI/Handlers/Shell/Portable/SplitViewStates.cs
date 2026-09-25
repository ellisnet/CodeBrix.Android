using CodeBrix.Android.UI.Policy;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the visual state a SplitView's Fluent template shows (the display-mode state group), as Core names it
/// (SplitView.GetStateName: "Closed", "Open" / "Closed" + "Overlay" / "Inline" / "Compact" / "CompactOverlay" +
/// "Left" / "Right"), and the state the adaptive table (plan 2.10 row SplitView) wants: in a Compact window an
/// Inline pane is shown as an Overlay pane and a CompactInline pane as a CompactOverlay pane (the app's DisplayMode
/// itself is never changed).
/// </summary>
internal static class SplitViewStates
{
    /// <summary>The state Core puts the template in.</summary>
    /// <param name="isPaneOpen">IsPaneOpen.</param>
    /// <param name="mode">The DisplayMode.</param>
    /// <param name="placement">The PanePlacement.</param>
    /// <returns>The state name.</returns>
    internal static string CoreState(bool isPaneOpen, SplitViewDisplayMode mode, SplitViewPanePlacement placement)
    {
        var open = isPaneOpen ? "Open" : "Closed";
        var side = placement == SplitViewPanePlacement.Right ? "Right" : "Left";
        var display = mode switch
        {
            SplitViewDisplayMode.Overlay => isPaneOpen ? "Overlay" : string.Empty,
            SplitViewDisplayMode.Inline => isPaneOpen ? "Inline" : string.Empty,
            SplitViewDisplayMode.CompactOverlay => isPaneOpen ? "CompactOverlay" : "Compact",
            SplitViewDisplayMode.CompactInline => isPaneOpen ? "Inline" : "Compact",
            _ => string.Empty,
        };
        if (!isPaneOpen && string.IsNullOrWhiteSpace(display))
        {
            side = string.Empty;
        }

        return open + display + side;
    }

    /// <summary>The state the template should show in a window of the given classes.</summary>
    /// <param name="window">The window's size classes.</param>
    /// <param name="isPaneOpen">IsPaneOpen.</param>
    /// <param name="mode">The app's DisplayMode.</param>
    /// <param name="placement">The PanePlacement.</param>
    /// <returns>The state name.</returns>
    internal static string Shown(WindowSizeClass window, bool isPaneOpen, SplitViewDisplayMode mode, SplitViewPanePlacement placement) =>
        CoreState(isPaneOpen, AdaptivePolicy.SplitView(window, mode), placement);
}
