#if __ANDROID__
using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// The Android <see cref="IOverlayPresenterPlatform"/> (seam hooks H11/H12): Core asks it to present each
/// ContentDialog and each flyout. It answers true - "presented natively" - for a ContentDialog whose title
/// and content are text (a Material 3 dialog, <see cref="NativeContentDialog"/>) and for a MenuFlyout of
/// standard items (a Material popup menu, or a bottom sheet in a Compact window, <see cref="NativeMenuFlyout"/>);
/// false for everything else, which Core then shows in its own popup layer (tier 1). A failure while building
/// the platform window is logged and answered false, so the app still gets Core's presentation.
/// </summary>
internal sealed class AndroidOverlayPresenter : IOverlayPresenterPlatform
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Overlay");

    /// <inheritdoc />
    public bool TryShowContentDialog(ContentDialog dialog)
    {
        try
        {
            return NativeContentDialog.TryShow(dialog, _log) != null;
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "The Material dialog for a ContentDialog failed; Core presents it instead.");
            return false;
        }
    }

    /// <inheritdoc />
    public void HideContentDialog(ContentDialog dialog, ContentDialogResult result) =>
        NativeOverlays.Find<NativeContentDialog>(dialog)?.CloseFromCore();

    /// <inheritdoc />
    public bool TryShowFlyout(FlyoutBase flyout, FrameworkElement placementTarget, FlyoutShowOptions options)
    {
        try
        {
            return flyout is MenuFlyout menu && NativeMenuFlyout.TryShow(menu, placementTarget, options, _log) != null;
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "The Material menu for a MenuFlyout failed; Core presents it instead.");
            return false;
        }
    }

    /// <inheritdoc />
    public void HideFlyout(FlyoutBase flyout) => NativeOverlays.Find(flyout)?.CloseFromCore();
}
#endif
