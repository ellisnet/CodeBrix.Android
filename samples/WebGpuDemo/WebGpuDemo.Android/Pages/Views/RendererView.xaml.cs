using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WebGpuDemo.Rendering;

namespace WebGpuDemo.Views;

public sealed partial class RendererView : UserControl
{
    public RendererView()
    {
        InitializeComponent();
    }

    /// <summary>The page's WebGpuRenderView.</summary>
    public WebGpuRenderView RenderView => View;

    /// <summary>The renderer the XAML gave the view.</summary>
    public QuadrantSquaresRenderer SquaresRenderer => (QuadrantSquaresRenderer)View.Renderer;

    private void OnDeviceReady(object sender, WebGpuDeviceReadyEventArgs e)
    {
        Placeholder.Visibility = Visibility.Collapsed;
        Status.Text = $"{nameof(QuadrantSquaresRenderer)} rendering on {e.Device.Backend}, render path {View.RenderPath}";
    }

    private void OnDeviceLost(object sender, WebGpuDeviceLostEventArgs e) => DevicePlaceholder.Show(Placeholder, Status, e.Reason);
}
