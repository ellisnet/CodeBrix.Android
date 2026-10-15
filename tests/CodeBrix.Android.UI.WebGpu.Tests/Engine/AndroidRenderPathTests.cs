using System.Linq;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.Platform.UI.WebGpu.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests;

/// <summary>
/// What the Android port changed in the render-path choice: there is no head GPU device to adopt; with the
/// application-wide direct GPU rendering latch on, the direct path is DirectSurface, which needs a window surface under
/// the view (DirectSurfaceGpuTests) - without one, or with RequireOwnedDevice, the view renders on Readback.
/// </summary>
public sealed class AndroidRenderPathTests
{
    [Fact]
    public void with_direct_gpu_rendering_enabled_but_no_window_surface_host_the_view_renders_on_readback()
    {
        //Arrange
        TestGpu.RequireAdapter();
        using var latch = DirectGpuLatch.On();
        var host = new RecordingEngineHost();
        var engine = new WebGpuRenderEngine(host);
        engine.Attach(WebGpuDeviceGateDecision.Proceed);
        engine.SetPixelSize(16, 16);

        //Act
        engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
        engine.EndFrame();
        var path = engine.RenderPath;
        var handler = engine.HasUncapturedErrorHandler;
        var wants = engine.WantsWindowSurface;
        engine.Detach();

        //Assert
        path.Should().Be(WebGpuRenderPath.Readback);
        wants.Should().BeTrue("a platform view that can host a window surface still should (the latch is on)");
        handler.Should().BeTrue("the view's own device reports its errors to the view");
        host.Events.Should().ContainInOrder("Path:None->Readback", "DeviceReady");
        host.Presented.Should().Be(1);
    }

    [Fact]
    public void require_owned_device_with_the_latch_on_replaces_the_device_and_stays_on_readback()
    {
        //Arrange
        TestGpu.RequireAdapter();
        using var latch = DirectGpuLatch.On();
        var host = new RecordingEngineHost();
        var engine = new WebGpuRenderEngine(host);
        engine.Attach(WebGpuDeviceGateDecision.Proceed);
        engine.SetPixelSize(16, 16);
        engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
        engine.EndFrame();

        //Act
        engine.RequireOwnedDevice = true;
        engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
        engine.EndFrame();
        var path = engine.RenderPath;
        engine.Detach();

        //Assert
        path.Should().Be(WebGpuRenderPath.Readback);
        host.Events.Should().Contain("DeviceLost:" + WebGpuRenderViewRules.DeviceOptionsChangedReason);
        host.Events.Count(e => e == "DeviceReady").Should().Be(2);
    }

    [Fact]
    public void the_no_window_host_line_names_the_direct_surface_path_and_readback()
    {
        //Arrange
        //Act
        var message = WebGpuRenderViewRules.DirectPathFallbackMessage(WebGpuRenderPath.DirectSurface, WebGpuSurfaceRules.NoWindowHostReason);

        //Assert
        message.Should().Contain("DirectSurface");
        message.Should().Contain("hosts no window surface");
        message.Should().Contain("Readback");
    }
}
