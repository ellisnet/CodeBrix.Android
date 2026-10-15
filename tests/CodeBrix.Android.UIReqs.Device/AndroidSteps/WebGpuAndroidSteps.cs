using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reqnroll;
using SilverAssertions;
using WinColor = Windows.UI.Color;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The WebGpu render view on Android" feature (AndroidFeatures/AndroidWebGpu): the WebGpu
/// add-in's own element, its frames on the SkiaSharp.Views canvas, its render paths (Readback; DirectSurface with the
/// direct GPU rendering latch on) and its frame drivers. Every check reads the view itself, its SurfaceView's buffer or
/// the captured screen; logcat tag UIReqs.WebGpu carries the device the view got.
/// </summary>
[Binding]
public sealed class WebGpuAndroidSteps
{
    private static readonly FieldInfo Latch =
        typeof(DirectGpuRenderingMode).GetField("_isEnabled", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DirectGpuRenderingMode no longer has a private static _isEnabled latch.");

    private static string? _lostReason;
    private static bool? _latchBefore;

    /// <summary>
    /// Sets the application-wide direct GPU rendering latch for this scenario (a one-way switch in an application -
    /// CodeBrixApplication.UseDirectGpuRenderingMode - so the scenario sets its private field and puts it back after).
    /// The view's handler and device read it when they are created.
    /// </summary>
    [Given("direct GPU rendering is {word}")]
    public void Given_latch(string state)
    {
        _latchBefore ??= DirectGpuRenderingMode.IsEnabled;
        Latch.SetValue(null, state == "on");
    }

    /// <summary>Puts the latch back as the scenario found it.</summary>
    [AfterScenario]
    public static void RestoreLatch()
    {
        if (_latchBefore is { } before)
        {
            Latch.SetValue(null, before);
            _latchBefore = null;
        }
    }

    /// <summary>A WebGpuRenderView clearing to a colour, with a TextBlock written from its RenderPathChanged event.</summary>
    [Given("a WebGpuRenderView named {string} {int} by {int} clearing to {string} is on the panel, its render path shown in {string}")]
    public async Task Given_a_view(string name, int width, int height, string color, string pathName)
    {
        ElementRegistry.Clear();
        _lostReason = null;
        StackPanel? panel = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = new WebGpuRenderView { Width = width, Height = height, ClearColor = Parse(color) };
            var path = new TextBlock { Text = "RenderPath=" + view.RenderPath, FontSize = 16 };
            view.RenderPathChanged += (_, e) => path.DispatcherQueue.TryEnqueue(() => path.Text = "RenderPath=" + e.NewPath);
            view.DeviceReady += (_, e) => global::Android.Util.Log.Info("UIReqs.WebGpu", $"device ready: {e.DeviceInfo}");
            view.DeviceLost += (_, e) =>
            {
                // Taking the view out of the tree at the end of the scenario is not a failure.
                if (e.Reason != "The view was unloaded.")
                {
                    _lostReason = e.Reason;
                }
            };
            ElementRegistry.Register(name, view);
            ElementRegistry.Register(pathName, path);
            panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(view);
            panel.Children.Add(path);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(panel!).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A presented picture exists (the view's own surface holds one).</summary>
    [Then("the WebGpuRenderView {string} presents a frame within {int} milliseconds")]
    public async Task Then_presents(string name, int milliseconds)
    {
        var presented = false;
        for (var waited = 0; waited < milliseconds && !presented && _lostReason == null; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
            presented = await OnUIThreadAsync(() => View(name).Surface.HasFrame).ConfigureAwait(false);
        }

        _lostReason.Should().BeNull();
        presented.Should().BeTrue("the view presents its first read-back frame");
    }

    /// <summary>The element's type comes from the Android add-in and its handler shows a native Skia view.</summary>
    [Then("{string} is a WebGpuRenderView of the Android WebGpu add-in, shown on the Android Skia canvas view")]
    public async Task Then_is_android_view(string name)
    {
        (string? assembly, string? handler, string? nativeView) = await OnUIThreadAsync(() =>
        {
            var view = View(name);
            var h = CodeBrix.Android.UI.Policy.PolicyDiagnostics.HandlerOf(view);
            return (view.GetType().Assembly.GetName().Name, h?.GetType().Name,
                (h as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView?.GetType().Name);
        }).ConfigureAwait(false);
        assembly.Should().Be("CodeBrix.Android.UI.WebGpu");
        handler.Should().Be("SkiaCanvasElementHandler");
        nativeView.Should().Be("SkiaCanvasView");
    }

    /// <summary>With the latch on: the add-in's surface host handler - a SurfaceView under the Skia canvas view.</summary>
    [Then("{string} is a WebGpuRenderView of the Android WebGpu add-in, shown on a SurfaceView under the Android Skia canvas view")]
    public async Task Then_is_android_surface_view(string name)
    {
        (string? assembly, string? handler, string? bottom, string? top) = await OnUIThreadAsync(() =>
        {
            var view = View(name);
            var h = CodeBrix.Android.UI.Policy.PolicyDiagnostics.HandlerOf(view);
            var group = (h as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView as global::Android.Views.ViewGroup;
            return (view.GetType().Assembly.GetName().Name, h?.GetType().Name,
                group?.ChildCount > 0 ? group.GetChildAt(0)?.GetType().Name : null,
                group?.ChildCount > 1 ? group.GetChildAt(group.ChildCount - 1)?.GetType().Name : null);
        }).ConfigureAwait(false);
        assembly.Should().Be("CodeBrix.Android.UI.WebGpu");
        handler.Should().Be("WebGpuSurfaceHostHandler");
        bottom.Should().Be("SurfaceView");
        top.Should().Be("SkiaCanvasView");
    }

    /// <summary>
    /// The share of the view's SurfaceView buffer (read with PixelCopy: what the DirectSurface path presented) within 8
    /// per channel of a colour, polled until it is reached or the time is up.
    /// </summary>
    [Then("the window surface of the WebGpuRenderView {string} is at least {int} percent {string} within {int} milliseconds")]
    public async Task Then_window_surface_is(string name, int percent, string color, int milliseconds)
    {
        var want = Parse(color);
        var share = 0.0;
        for (var waited = 0; waited <= milliseconds && share * 100 < percent; waited += 200)
        {
            share = await SurfaceShareAsync(name, want).ConfigureAwait(false);
            if (share * 100 < percent)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
        }

        global::Android.Util.Log.Info("UIReqs.WebGpu", $"window surface of {name}: {share * 100:0.00}% {color}");
        (share * 100).Should().BeGreaterThanOrEqualTo(percent);
    }

    private static async Task<double> SurfaceShareAsync(string name, WinColor want)
    {
        var done = new TaskCompletionSource<global::Android.Graphics.Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var h = CodeBrix.Android.UI.Policy.PolicyDiagnostics.HandlerOf(View(name));
            var group = (h as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView as global::Android.Views.ViewGroup;
            if (group?.GetChildAt(0) is not global::Android.Views.SurfaceView surface || surface.Width <= 0 || surface.Height <= 0)
            {
                done.TrySetResult(null);
                return;
            }

            var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(surface.Width, surface.Height, global::Android.Graphics.Bitmap.Config.Argb8888!)!;
            global::Android.Views.PixelCopy.Request(surface, bitmap, new CopyListener(bitmap, done),
                new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!));
        }).ConfigureAwait(false);

        using var copy = await done.Task.ConfigureAwait(false);
        if (copy == null)
        {
            return 0;
        }

        var width = copy.Width;
        var height = copy.Height;
        var pixels = new int[width * height];
        copy.GetPixels(pixels, 0, width, 0, 0, width, height);
        var matching = 0;
        foreach (var argb in pixels)
        {
            var r = (argb >> 16) & 0xFF;
            var g = (argb >> 8) & 0xFF;
            var b = argb & 0xFF;
            if (Math.Abs(r - want.R) <= 8 && Math.Abs(g - want.G) <= 8 && Math.Abs(b - want.B) <= 8)
            {
                matching++;
            }
        }

        return pixels.Length == 0 ? 0 : matching / (double)pixels.Length;
    }

    private sealed class CopyListener : Java.Lang.Object, global::Android.Views.PixelCopy.IOnPixelCopyFinishedListener
    {
        private readonly global::Android.Graphics.Bitmap _bitmap;
        private readonly TaskCompletionSource<global::Android.Graphics.Bitmap?> _done;

        internal CopyListener(global::Android.Graphics.Bitmap bitmap, TaskCompletionSource<global::Android.Graphics.Bitmap?> done)
        {
            _bitmap = bitmap;
            _done = done;
        }

        public void OnPixelCopyFinished(int copyResult)
        {
            if (copyResult == (int)global::Android.Views.PixelCopyResult.Success)
            {
                _done.TrySetResult(_bitmap);
            }
            else
            {
                _bitmap.Dispose();
                _done.TrySetResult(null);
            }
        }
    }

    /// <summary>The view's RenderPath.</summary>
    [Then("the render path of the WebGpuRenderView {string} is {string}")]
    public async Task Then_render_path(string name, string path)
    {
        var actual = await OnUIThreadAsync(() => View(name).RenderPath).ConfigureAwait(false);
        actual.Should().Be(Enum.Parse<WebGpuRenderPath>(path));
    }

    /// <summary>A TextBlock's text (polled briefly: it is written from a queued RenderPathChanged update).</summary>
    [Then("the TextBlock {string} reads {string}")]
    public async Task Then_text_reads(string name, string text)
    {
        var actual = string.Empty;
        for (var waited = 0; waited <= 2000 && actual != text; waited += 100)
        {
            actual = await OnUIThreadAsync(() => ((TextBlock)ElementRegistry.Resolve(name)).Text).ConfigureAwait(false);
            if (actual != text)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        actual.Should().Be(text);
    }

    /// <summary>Switches the frame driver.</summary>
    [When("the frame driver of the WebGpuRenderView {string} is {string}")]
    public async Task When_driver(string name, string driver)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => View(name).FrameDriver = Enum.Parse<WebGpuFrameDriver>(driver)).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>After a quiet period, one Invalidate renders exactly this many frames.</summary>
    [Then("one Invalidate of the WebGpuRenderView {string} renders exactly {int} frame(s)")]
    public async Task Then_invalidate_renders(string name, int frames)
    {
        // Quiet first: a frame the driver switch itself asked for (OnDemand renders once on a switch) must not count.
        await Task.Delay(700).ConfigureAwait(false);
        var before = await OnUIThreadAsync(() => View(name).Surface.FramesPresented).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => View(name).Invalidate()).ConfigureAwait(false);
        await Task.Delay(1000).ConfigureAwait(false);
        var after = await OnUIThreadAsync(() => View(name).Surface.FramesPresented).ConfigureAwait(false);
        global::Android.Util.Log.Info("UIReqs.WebGpu", $"one Invalidate under {await OnUIThreadAsync(() => View(name).FrameDriver).ConfigureAwait(false)}: {after - before} frame(s)");
        (after - before).Should().Be(frames);
    }

    /// <summary>The Manual driver: a frame opened and ended on a worker thread (BeginFrame / EndFrame).</summary>
    [When("a frame clearing to {string} is rendered into the WebGpuRenderView {string} from a worker thread")]
    public async Task When_worker_frame(string color, string name)
    {
        var view = await OnUIThreadAsync(() =>
        {
            var v = View(name);
            v.ClearColor = Parse(color);
            return v;
        }).ConfigureAwait(false);

        var rendered = false;
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                var frame = view.BeginFrame();
                if (frame != null)
                {
                    view.EndFrame();
                    rendered = true;
                }
            }
            catch (Exception e)
            {
                failure = e;
            }
        })
        { Name = "UIReqs WebGpu manual frame", IsBackground = true };
        worker.Start();
        await Task.Run(() => worker.Join(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

        failure.Should().BeNull();
        rendered.Should().BeTrue("a loaded view with a device opens a frame on any thread under the Manual driver");
    }

    /// <summary>
    /// The share of the view's presented picture (its surface's capture: the read-back frame the native view draws)
    /// within 8 per channel of a colour, polled until it is reached or the time is up.
    /// </summary>
    [Then("the picture of the WebGpuRenderView {string} is at least {int} percent {string} within {int} milliseconds")]
    public async Task Then_picture_is(string name, int percent, string color, int milliseconds)
    {
        var want = Parse(color);
        var share = 0.0;
        for (var waited = 0; waited <= milliseconds && share * 100 < percent; waited += 100)
        {
            share = await OnUIThreadAsync(() => Share(View(name), want)).ConfigureAwait(false);
            if (share * 100 < percent)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        global::Android.Util.Log.Info("UIReqs.WebGpu", $"picture of {name}: {share * 100:0.00}% {color}");
        (share * 100).Should().BeGreaterThanOrEqualTo(percent);
    }

    private static double Share(WebGpuRenderView view, WinColor want)
    {
        using var picture = view.Surface.CaptureFrame();
        if (picture == null)
        {
            return 0;
        }

        using var bitmap = new global::SkiaSharp.SKBitmap(new global::SkiaSharp.SKImageInfo(picture.Width, picture.Height,
            global::SkiaSharp.SKColorType.Bgra8888, global::SkiaSharp.SKAlphaType.Unpremul));
        if (!picture.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
        {
            return 0;
        }

        var matching = 0;
        var pixels = bitmap.Pixels;
        foreach (var p in pixels)
        {
            if (Math.Abs(p.Red - want.R) <= 8 && Math.Abs(p.Green - want.G) <= 8 && Math.Abs(p.Blue - want.B) <= 8 && p.Alpha >= 247)
            {
                matching++;
            }
        }

        return pixels.Length == 0 ? 0 : matching / (double)pixels.Length;
    }

    /// <summary>No DeviceLost other than the unload at the end.</summary>
    [Then("the WebGpuRenderView {string} reported no device loss")]
    public void Then_no_loss(string name) => _lostReason.Should().BeNull();

    private static WebGpuRenderView View(string name) => (WebGpuRenderView)ElementRegistry.Resolve(name);

    private static WinColor Parse(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return WinColor.FromArgb(0xFF, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
