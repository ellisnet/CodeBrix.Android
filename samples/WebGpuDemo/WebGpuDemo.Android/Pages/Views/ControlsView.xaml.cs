using System.Globalization;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WebGpuDemo.Rendering;

namespace WebGpuDemo.Views;

public sealed partial class ControlsView : UserControl
{
    private readonly HudOverlay _hud = new();
    private AnimatedTriangle _triangle;
    private long _framesRendered;

    public ControlsView()
    {
        InitializeComponent();
    }

    /// <summary>The page's WebGpuRenderView.</summary>
    public WebGpuRenderView RenderView => View;

    /// <summary>The heads-up display the Overlay draws.</summary>
    public HudOverlay Hud => _hud;

    /// <summary>Frames this page has drawn (by the built-in drivers and by "Render one frame").</summary>
    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    private void OnDeviceReady(object sender, WebGpuDeviceReadyEventArgs e)
    {
        _triangle?.Dispose();
        _triangle = new AnimatedTriangle(e.Device, View.PreferredFormat);
        Placeholder.Visibility = Visibility.Collapsed;
        Status.Text = $"Rendering on {e.Device.Backend}";
        ShowDeviceInfo(e.DeviceInfo);
    }

    // Raised on the UI thread on this page (the built-in drivers and "Render one frame" both run there).
    private void ShowDeviceInfo(WebGpuDeviceInfo info)
    {
        AdapterText.Text = $"Adapter: {info.AdapterName} ({info.AdapterType}, {info.Backend})";
        CapabilitiesText.Text = string.Format(CultureInfo.InvariantCulture,
            "Features: {0} enabled of {1} available, max texture {2}, max bind groups {3}",
            info.Features.Count + info.NativeFeatures.Count,
            info.AdapterFeatures.Count + info.AdapterNativeFeatures.Count,
            info.Limits.MaxTextureDimension2D, info.Limits.MaxBindGroups);
    }

    // The built-in drivers (Continuous, OnDemand) raise Render on the UI thread.
    private void OnRender(object sender, WebGpuRenderEventArgs e) => Draw(e.Frame);

    private void Draw(WebGpuFrame frame)
    {
        _triangle?.Draw(frame);
        _hud.Update(View.RenderPath, View.FrameDriver);
        var count = Interlocked.Increment(ref _framesRendered);
        // Every frame when frames are rare (OnDemand, Manual), a few times a second while running continuously.
        if (View.FrameDriver != WebGpuFrameDriver.Continuous || count % 15 == 0) ShowFrameCount(count);
    }

    // The head's paint: SkiaSharp only, and only text the frame's thread already prepared.
    private void OnOverlay(object sender, WebGpuOverlayEventArgs e) => _hud.Draw(e.Canvas);

    private void OnRenderPathChanged(object sender, WebGpuRenderPathChangedEventArgs e)
    {
        RenderPathText.Text = "Render path: " + e.NewPath;
        _hud.Describe(e.NewPath, View.FrameDriver);
    }

    private void OnDeviceLost(object sender, WebGpuDeviceLostEventArgs e)
    {
        _triangle?.Dispose();
        _triangle = null;
        AdapterText.Text = "Adapter: none";
        CapabilitiesText.Text = "Features: none";
        DevicePlaceholder.Show(Placeholder, Status, e.Reason);
    }

    private void OnDriverChanged(object sender, SelectionChangedEventArgs e)
    {
        // Raised once while InitializeComponent sets SelectedIndex, before the later named elements exist.
        if (View is null || RenderOneButton is null) return;
        View.FrameDriver = DriverSelector.SelectedIndex switch
        {
            1 => WebGpuFrameDriver.OnDemand,
            2 => WebGpuFrameDriver.Manual,
            _ => WebGpuFrameDriver.Continuous,
        };
        RenderOneButton.IsEnabled = View.FrameDriver == WebGpuFrameDriver.Manual;
        _hud.Describe(View.RenderPath, View.FrameDriver);
        ShowFrameCount(FramesRendered);
    }

    // OnDemand: one frame soon. Under Continuous and Manual it does nothing (the view's rule).
    private void OnInvalidate(object sender, RoutedEventArgs e) => View.Invalidate();

    // Manual: this page is the driver - it opens the frame, records it and ends it, here on the UI thread (any one
    // thread at a time would do). BeginFrame creates the device on the first call, raising DeviceReady inside it.
    private void OnRenderOne(object sender, RoutedEventArgs e)
    {
        var frame = View.BeginFrame();
        if (frame is null)
        {
            Status.Text = "The view cannot render now";
            return;
        }

        try
        {
            Draw(frame);
        }
        finally
        {
            View.EndFrame();
        }
    }

    private void OnPauseChanged(object sender, RoutedEventArgs e) => View.IsPaused = PauseCheck.IsChecked == true;

    private void OnRateChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (View is null || RateText is null) return;
        View.TargetFrameRate = e.NewValue;
        RateText.Text = e.NewValue.ToString("0", CultureInfo.InvariantCulture);
    }

    private void ShowFrameCount(long count)
    {
        FrameCountText.Text = "Frames: " + count.ToString(CultureInfo.InvariantCulture);
        // Android sample: the frame rate in the readout too (the HUD's own measurement over the last quarter second).
        FpsText.Text = _hud.FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " fps";
    }
}
