namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// Which overlays CodeBrix.Android presents as platform windows (plan 2.9, tier 2) and which it leaves to
/// Core's own in-window popups (tier 1, the PopupRoot overlay drawn by the element handlers).
/// </summary>
/// <remarks>
/// Tier 2 is the default: a ContentDialog whose title and content are text becomes a Material 3 dialog, a
/// MenuFlyout becomes a Material popup menu (a bottom sheet in a Compact window). Everything else - a
/// ContentDialog with arbitrary XAML content, a Flyout with content, a Popup, a TeachingTip, a rich ToolTip -
/// stays tier 1, so pasted XAML keeps working whatever it puts in an overlay. Turning a switch off sends
/// that overlay to tier 1 as well (the Core presentation that every Skia head uses).
/// </remarks>
internal static class OverlayPresentation
{
    /// <summary>True (default) to show text-only ContentDialogs as Material dialogs.</summary>
    internal static bool NativeContentDialogs { get; set; } = true;

    /// <summary>True (default) to show MenuFlyouts as Material popup menus / bottom sheets.</summary>
    internal static bool NativeMenuFlyouts { get; set; } = true;

    /// <summary>
    /// The window width class overlays are laid out for; null (default) = computed from the window's width.
    /// A diagnostics and test knob: it lets one emulator show the Compact forms (bottom sheets) and the wide ones.
    /// </summary>
    internal static WindowWidthClass? WidthClassOverride { get; set; }

    /// <summary>The width class overlays use for a window <paramref name="widthDips"/> wide.</summary>
    internal static WindowWidthClass WidthClassFor(double widthDips) => WidthClassOverride ?? WindowSizeClasses.FromWidth(widthDips);

    /// <summary>Puts every switch back to its default.</summary>
    internal static void Reset()
    {
        NativeContentDialogs = true;
        NativeMenuFlyouts = true;
        WidthClassOverride = null;
    }
}
