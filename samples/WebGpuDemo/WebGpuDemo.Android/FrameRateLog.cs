using System;
using System.Diagnostics;
using System.Globalization;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Dispatching;
using WebGpuDemo.Views;
using ALog = Android.Util.Log;

namespace WebGpuDemo;

/// <summary>
/// Writes one logcat line a second (tag <c>WebGpuDemo</c>) about the page on screen: its name, the view's render path,
/// its texture size, the adapter, the frames the page has drawn and the frame rate over the last second - the proof a
/// device run is checked by (<c>adb logcat -s WebGpuDemo</c>).
/// </summary>
internal static class FrameRateLog
{
    private const string Tag = "WebGpuDemo";
    private static DispatcherQueueTimer _timer;
    private static object _lastPage;
    private static long _lastFrames;
    private static long _lastTimestamp;

    /// <summary>Starts the log on the user-interface thread (once).</summary>
    internal static void Start()
    {
        if (_timer != null || DispatcherQueue.GetForCurrentThread() is not { } queue)
        {
            return;
        }

        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => WriteLine();
        _timer.Start();
    }

    private static void WriteLine()
    {
        if (App.MainPage?.CurrentPage is not { } page)
        {
            return;
        }

        var (name, view, frames) = page switch
        {
            TriangleView triangle => ("Triangle", triangle.RenderView, triangle.FramesRendered),
            ModelsView models => ("Models", models.RenderView, models.FramesRendered),
            RendererView renderer => ("Renderer class", renderer.RenderView, renderer.SquaresRenderer.FramesRendered),
            ControlsView controls => ("Controls", controls.RenderView, controls.FramesRendered),
            _ => (page.GetType().Name, (WebGpuRenderView)null, 0L),
        };

        var now = Stopwatch.GetTimestamp();
        var fps = 0.0;
        if (ReferenceEquals(page, _lastPage) && _lastTimestamp != 0)
        {
            fps = (frames - _lastFrames) / Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        }

        _lastPage = page;
        _lastFrames = frames;
        _lastTimestamp = now;

        var info = view?.DeviceInfo;
        ALog.Info(Tag, string.Format(CultureInfo.InvariantCulture,
            "FRAMES page={0} path={1} driver={2} size={3}x{4} adapter={5} backend={6} frames={7} fps={8:0.0}",
            name, view?.RenderPath, view?.FrameDriver, view?.TextureWidth, view?.TextureHeight,
            info?.AdapterName ?? "none", info?.Backend.ToString() ?? "none", frames, fps));
    }
}
