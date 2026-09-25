using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using AndroidLog = Android.Util.Log;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>Logs (logcat tag UIReqs.Input) the routed pointer events the injected input produces at the root.</summary>
internal static class InputTrace
{
    private static bool _attached;

    /// <summary>Attaches the root handlers once (UI thread).</summary>
    internal static void EnsureAttached()
    {
        if (_attached || VirtualApplication.Running?.Root is not { } root)
        {
            return;
        }

        _attached = true;
        root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) => Log("PointerPressed", e)), true);
        root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, e) => Log("PointerReleased", e)), true);
        root.AddHandler(UIElement.TappedEvent, new TappedEventHandler((_, e) => AndroidLog.Debug("UIReqs.Input", "Tapped source=" + (e.OriginalSource as FrameworkElement)?.Name + " " + e.OriginalSource?.GetType().Name)), true);
    }

    private static void Log(string what, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        AndroidLog.Debug("UIReqs.Input", $"{what} at {point.Position} source={(e.OriginalSource as FrameworkElement)?.Name}:{e.OriginalSource?.GetType().Name} handled={e.Handled}");
    }
}
