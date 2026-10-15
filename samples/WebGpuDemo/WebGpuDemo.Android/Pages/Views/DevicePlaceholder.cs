using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WebGpuDemo.Views;

/// <summary>
/// What every page shows when its view has no device: the add-in raises DeviceLost with the reason (no adapter on this
/// machine, a launch that said there is no WebGPU, an error the device reported) and shows whatever is behind it, so
/// the page puts its own message there. Leaving the page is not worth a message: the view also raises DeviceLost when
/// it is unloaded, with the add-in's documented reason, and gets a new device when it comes back.
/// </summary>
internal static class DevicePlaceholder
{
    private const string UnloadedReason = "The view was unloaded.";

    internal const string Message = "WebGPU is not available on this machine.";

    internal static void Show(TextBlock placeholder, TextBlock status, string reason)
    {
        if (reason == UnloadedReason)
        {
            status.Text = "Paused while the page is hidden";
            return;
        }

        placeholder.Text = Message + " " + reason;
        placeholder.Visibility = Visibility.Visible;
        status.Text = "No device: " + reason;
    }
}
