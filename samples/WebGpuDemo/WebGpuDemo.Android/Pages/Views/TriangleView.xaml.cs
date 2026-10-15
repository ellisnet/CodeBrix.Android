using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WebGpuDemo.Rendering;

namespace WebGpuDemo.Views;

public sealed partial class TriangleView : UserControl
{
    private AnimatedTriangle _triangle;
    private long _framesRendered;
    private int _devicesReady;

    public TriangleView()
    {
        InitializeComponent();
    }

    /// <summary>The page's WebGpuRenderView.</summary>
    public WebGpuRenderView RenderView => View;

    /// <summary>Frames this page has drawn.</summary>
    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    /// <summary>Devices this page has been handed (a new one each time the page comes back).</summary>
    public int DevicesReady => _devicesReady;

    /// <summary>The triangle's colour in the last frame.</summary>
    public (byte R, byte G, byte B) LastColor => _triangle?.LastColor ?? default;

    private void OnDeviceReady(object sender, WebGpuDeviceReadyEventArgs e)
    {
        _triangle?.Dispose();
        _triangle = new AnimatedTriangle(e.Device, View.PreferredFormat);
        _devicesReady++;
        Placeholder.Visibility = Visibility.Collapsed;
        Status.Text = $"Rendering on {e.Device.Backend}, render path {View.RenderPath}";
    }

    private void OnRender(object sender, WebGpuRenderEventArgs e)
    {
        _triangle?.Draw(e.Frame);
        Interlocked.Increment(ref _framesRendered);
    }

    private void OnDeviceLost(object sender, WebGpuDeviceLostEventArgs e)
    {
        _triangle?.Dispose();
        _triangle = null;
        DevicePlaceholder.Show(Placeholder, Status, e.Reason);
    }
}
