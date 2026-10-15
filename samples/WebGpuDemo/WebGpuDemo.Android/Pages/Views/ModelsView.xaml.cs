using System;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WebGpuDemo.Rendering;

namespace WebGpuDemo.Views;

public sealed partial class ModelsView : UserControl
{
    private ModelScene _scene;
    private long _framesRendered;

    public ModelsView()
    {
        InitializeComponent();
    }

    /// <summary>The page's WebGpuRenderView.</summary>
    public WebGpuRenderView RenderView => View;

    /// <summary>Frames this page has drawn.</summary>
    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    /// <summary>The scene while the view has a device, else null.</summary>
    public ModelScene Scene => _scene;

    private void OnDeviceReady(object sender, WebGpuDeviceReadyEventArgs e)
    {
        _scene?.Dispose();
        _scene = null;
        try
        {
            // Android sample: the models are APK assets, copied out to the app's files folder on first use (the loader
            // reads files); on the desktop heads they are loose files beside the app.
            _scene = new ModelScene(e.Device, SampleAssets.EnsureCopied());
            _scene.UserYaw = (float)(CameraSlider.Value * Math.PI / 180);
            Placeholder.Visibility = Visibility.Collapsed;
            Status.Text = $"{_scene.ModelCount} models loaded, rendering on {e.Device.Backend}, render path {View.RenderPath}";
        }
        catch (Exception failure)
        {
            // A model that cannot be read is the application's problem, not the device's: say so on the page.
            Placeholder.Text = "The models could not be loaded: " + failure.Message;
            Placeholder.Visibility = Visibility.Visible;
            Status.Text = "No models";
        }
    }

    private void OnRender(object sender, WebGpuRenderEventArgs e)
    {
        if (_scene is null) return;
        _scene.Render(e.Frame, View);
        Interlocked.Increment(ref _framesRendered);
    }

    private void OnDeviceLost(object sender, WebGpuDeviceLostEventArgs e)
    {
        // Raised BEFORE the view releases the device, so the scene's GPU objects are released on a live device.
        _scene?.Dispose();
        _scene = null;
        DevicePlaceholder.Show(Placeholder, Status, e.Reason);
    }

    private void OnCameraChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_scene != null) _scene.UserYaw = (float)(e.NewValue * Math.PI / 180);
    }
}
