using System;
using System.Threading;
using System.Threading.Tasks;
using AClipboardManager = global::Android.Content.ClipboardManager;
using AContext = global::Android.Content.Context;
using AAccessibilityManager = global::Android.Views.Accessibility.AccessibilityManager;
using AContentMode = global::Android.Views.Accessibility.ContentMode;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>
/// Lets transient clipboard UI time out before capturing a frame. It observes clipboard-change
/// notifications without reading the clipboard or changing system settings. Only the harness uses it.
/// </summary>
internal static class ClipboardCaptureWait
{
    private static AClipboardManager? _clipboard;
    private static long _deadline;
    private static int _quietMilliseconds;

    /// <summary>Observes copies while the scenario activity is alive.</summary>
    internal static void Start(AContext context)
    {
        Stop();
        // Leave margin for the overlay's entry and exit animations; honour longer accessibility timeouts.
        const int quietMilliseconds = 10000;
        var accessibility = context.GetSystemService(AContext.AccessibilityService) as AAccessibilityManager;
        _quietMilliseconds = Math.Max(quietMilliseconds, accessibility?.GetRecommendedTimeoutMillis(
            quietMilliseconds, AContentMode.Controls | AContentMode.Text | AContentMode.Icons) ?? quietMilliseconds);
        _clipboard = context.GetSystemService(AContext.ClipboardService) as AClipboardManager;
        if (_clipboard != null)
        {
            _clipboard.PrimaryClipChanged += OnClipboardChanged;
        }
    }

    /// <summary>Releases the activity's clipboard listener.</summary>
    internal static void Stop()
    {
        if (_clipboard != null)
        {
            _clipboard.PrimaryClipChanged -= OnClipboardChanged;
            _clipboard = null;
        }
    }

    private static void OnClipboardChanged(object? sender, EventArgs args) =>
        Interlocked.Exchange(ref _deadline, Environment.TickCount64 + _quietMilliseconds);

    /// <summary>Waits for a quiet period after the last copy, restarting if another copy arrives.</summary>
    internal static async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (Interlocked.Read(ref _deadline) - Environment.TickCount64 is var remaining && remaining > 0)
        {
            global::Android.Util.Log.Info("UIReqs.Clipboard", $"Waiting {remaining} ms before capture after a clipboard change.");
            await Task.Delay(TimeSpan.FromMilliseconds(remaining), deadline.Token).ConfigureAwait(false);
        }
    }
}
