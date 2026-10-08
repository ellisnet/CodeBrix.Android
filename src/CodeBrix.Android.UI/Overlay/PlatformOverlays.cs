#if __ANDROID__
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// The Core overlays CodeBrix.Android is showing as platform windows right now (a text ContentDialog as a Material
/// dialog, a MenuFlyout as a Material menu or bottom sheet). Core's VisualTreeHelper.GetOpenPopupsForXamlRoot does
/// not list them (Core opens no popup for them), so diagnostics and app self-checks look here.
/// </summary>
/// <remarks>
/// Internal (no new public API before the handler-API decision D-O1): the UIReqs device app reads it through
/// InternalsVisibleTo; an app's own self-check can only read it by reflection.
/// </remarks>
internal static class PlatformOverlays
{
    /// <summary>The ContentDialogs showing as Material dialogs, oldest first.</summary>
    public static IReadOnlyList<ContentDialog> ContentDialogs =>
        NativeOverlays.Open.OfType<NativeContentDialog>().Where(d => d.IsShowing).Select(d => d.Dialog).ToList();

    /// <summary>The flyouts showing as Material menus or bottom sheets, oldest first.</summary>
    public static IReadOnlyList<FlyoutBase> Flyouts =>
        NativeOverlays.Open.OfType<NativeMenuFlyout>().Where(m => m.IsShowing).Select(m => (FlyoutBase)m.Owner).ToList();
}
#endif
