#nullable enable

using System;
using System.Linq;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests;

/// <summary>
/// The Android DirectSurface render path through the real engine, presenter and pass on this machine's WebGPU device,
/// with the window surface replaced by <see cref="FakeWindowSurface"/> (an off-screen texture standing in for the
/// SurfaceView's frames): the path choice, what reaches the surface (the BGRA frame drawn onto an RGBA surface), the
/// window's lifecycle (destroyed, created again, changed), the acquire statuses, and every fallback to Readback.
/// </summary>
public sealed class DirectSurfaceGpuTests
{
	private const int Width = 32;
	private const int Height = 24;

	private sealed class Rig : IDisposable
	{
		private readonly DirectGpuLatch _latch;

		internal Rig(bool latch = true, bool connectHost = true, bool attachWindow = true)
		{
			_latch = latch ? DirectGpuLatch.On() : DirectGpuLatch.Off();
			Host = new RecordingEngineHost();
			WebGpuRenderEngine? engine = null;
			Factory = new FakeWindowSurfaceFactory(() => engine!.Device!);
			Engine = engine = new WebGpuRenderEngine(Host, surfaceFactory: Factory);
			Engine.Attach(WebGpuDeviceGateDecision.Proceed);
			Engine.SetPixelSize(Width, Height);
			if (connectHost)
			{
				Engine.ConnectWindowHost();
			}

			if (attachWindow)
			{
				AttachWindow();
			}
		}

		internal RecordingEngineHost Host { get; }

		internal FakeWindowSurfaceFactory Factory { get; }

		internal WebGpuRenderEngine Engine { get; }

		internal int Releases;

		internal nint NextHandle = 0x1000;

		internal void AttachWindow() => Engine.AttachWindow(NextHandle += 0x10, Width, Height, _ => Interlocked.Increment(ref Releases));

		internal WebGpuFrameOutcome Frame(SKColor clear)
		{
			Engine.ClearColor = clear;
			var outcome = Engine.OpenFrame(wait: true);
			if (outcome == WebGpuFrameOutcome.Rendered)
			{
				Engine.EndFrame();
			}

			return outcome;
		}

		public void Dispose()
		{
			Engine.Detach();
			Engine.DisconnectWindowHost();
			_latch.Dispose();
		}
	}

	private static (byte R, byte G, byte B, byte A) Centre(FakeWindowSurface surface)
	{
		var (pixels, width) = surface.ReadFrame() ?? throw new InvalidOperationException("no frame");
		var i = ((Height / 2) * width + (width / 2)) * 4;
		return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
	}

	[Fact]
	public void with_the_latch_on_and_a_window_the_view_presents_to_the_window_surface_on_its_own_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();

		//Act
		var outcome = rig.Frame(new SKColor(255, 0, 0));
		var path = rig.Engine.RenderPath;
		var handler = rig.Engine.HasUncapturedErrorHandler;
		var surface = rig.Factory.Latest!;

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Rendered);
		path.Should().Be(WebGpuRenderPath.DirectSurface);
		handler.Should().BeTrue("DirectSurface runs on the view's OWN device, which reports its errors to the view");
		rig.Host.Events.Should().ContainInOrder("Path:None->DirectSurface", "DeviceReady");
		rig.Host.SurfaceFrames.Should().Be(1);
		rig.Host.Presented.Should().Be(0, "nothing is read back on the DirectSurface path");
		surface.PresentCount.Should().Be(1);
		surface.Configurations.Should().ContainSingle();
		surface.Configurations[0].Configuration.Format.Should().Be(TextureFormat.RGBA8Unorm);
		surface.Configurations[0].Configuration.PresentMode.Should().Be(PresentMode.Fifo);
		(surface.Configurations[0].Width, surface.Configurations[0].Height).Should().Be((Width, Height));
		((WebGpuSurfacePresenter)rig.Engine.Presenter!).Target!.ReadbackCount.Should().Be(0);
	}

	[Fact]
	public void the_bgra_frame_reaches_the_rgba_surface_with_its_channels_in_place()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();

		//Act
		rig.Frame(new SKColor(255, 32, 0)).Should().Be(WebGpuFrameOutcome.Rendered);
		var red = Centre(rig.Factory.Latest!);
		rig.Frame(new SKColor(0, 64, 255)).Should().Be(WebGpuFrameOutcome.Rendered);
		var blue = Centre(rig.Factory.Latest!);

		//Assert
		red.Should().Be(((byte)255, (byte)32, (byte)0, (byte)255));
		blue.Should().Be(((byte)0, (byte)64, (byte)255, (byte)255));
	}

	[Fact]
	public void the_frame_protocol_is_readbacks_bgra_texture_of_the_element_size()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();

		//Act
		rig.Engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		var frame = rig.Engine.Frame;
		var (format, width, height, usage) = (frame.Format, frame.Width, frame.Height, rig.Engine.Usage);
		var textureIsSet = !frame.Texture.IsNull && !frame.TextureView.IsNull && !frame.Encoder.IsNull;
		rig.Engine.EndFrame();

		//Assert
		format.Should().Be(TextureFormat.BGRA8Unorm);
		(width, height).Should().Be((Width, Height));
		textureIsSet.Should().BeTrue();
		(usage & TextureUsage.RenderAttachment).Should().Be(TextureUsage.RenderAttachment);
		(usage & TextureUsage.TextureBinding).Should().Be(TextureUsage.TextureBinding);
	}

	[Fact]
	public void with_the_host_but_no_window_yet_the_first_device_waits_then_goes_direct_when_the_window_comes()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig(attachWindow: false);

		//Act
		var before = rig.Frame(SKColors.Red);
		var deviceWhileWaiting = rig.Engine.IsDeviceReady;
		rig.AttachWindow();
		var after = rig.Frame(SKColors.Red);

		//Assert
		before.Should().Be(WebGpuFrameOutcome.NotReady);
		deviceWhileWaiting.Should().BeFalse("no device is created while the window surface is awaited");
		after.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
	}

	[Fact]
	public void a_window_that_never_comes_falls_back_to_readback_after_the_wait_and_drops_the_host()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig(attachWindow: false);
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.NotReady);

		//Act
		Thread.Sleep(TimeSpan.FromMilliseconds(WebGpuSurfaceRules.WindowWaitMilliseconds + 150));
		var outcome = rig.Frame(SKColors.Red);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Engine.WantsWindowSurface.Should().BeFalse("the platform view removes its SurfaceView after a direct failure");
		rig.Host.Presented.Should().Be(1);
	}

	[Fact]
	public void without_a_window_host_the_latch_gives_readback()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig(connectHost: false, attachWindow: false);

		//Act
		var outcome = rig.Frame(SKColors.Red);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Factory.Created.Should().BeEmpty();
	}

	[Fact]
	public void with_the_latch_off_a_connected_window_is_never_used()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig(latch: false);

		//Act
		var outcome = rig.Frame(SKColors.Red);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Engine.WantsWindowSurface.Should().BeFalse();
		rig.Factory.Created.Should().BeEmpty();
	}

	[Fact]
	public void a_destroyed_window_releases_the_surface_and_the_window_and_frames_wait_without_losing_the_device()
	{
		//Arrange - Home: Android destroys the SurfaceView's surface, the view stays loaded
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		var first = rig.Factory.Latest!;

		//Act
		var releasedAtOnce = rig.Engine.DetachWindow();
		var whileGone = rig.Frame(SKColors.Red);
		rig.AttachWindow(); // back: a new surface, a new window
		var back = rig.Frame(SKColors.Blue);

		//Assert
		releasedAtOnce.Should().BeTrue();
		first.IsDisposed.Should().BeTrue("the WebGPU surface is released before the window is given back");
		rig.Releases.Should().Be(1);
		whileGone.Should().Be(WebGpuFrameOutcome.NotReady);
		back.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Factory.Created.Should().HaveCount(2);
		rig.Factory.Latest!.PresentCount.Should().Be(1);
		Centre(rig.Factory.Latest!).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255));
		rig.Host.Events.Count(e => e.StartsWith("DeviceLost", StringComparison.Ordinal)).Should().Be(0, "the device outlives the window");
		rig.Host.Events.Count(e => e == "DeviceReady").Should().Be(1);
		rig.Factory.Created.Any(s => s.AcquiredUnconfigured).Should().BeFalse();
	}

	[Fact]
	public void a_window_destroyed_inside_this_threads_frame_is_released_by_that_frames_end()
	{
		//Arrange - a Render handler that takes the view out of the window
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		var surface = rig.Factory.Latest!;

		//Act
		rig.Engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		var releasedAtOnce = rig.Engine.DetachWindow();
		var releasesInsideFrame = rig.Releases;
		rig.Engine.EndFrame();

		//Assert
		releasedAtOnce.Should().BeFalse();
		releasesInsideFrame.Should().Be(0);
		rig.Releases.Should().Be(1);
		surface.IsDisposed.Should().BeTrue();
		surface.PresentCount.Should().Be(1, "the frame that lost its window presents nothing");
	}

	[Fact]
	public void a_window_destroyed_while_another_thread_holds_a_frame_waits_for_that_frame()
	{
		//Arrange - the Manual driver on a worker thread
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		using var opened = new ManualResetEventSlim();
		using var proceed = new ManualResetEventSlim();
		var worker = new Thread(() =>
		{
			rig.Engine.OpenFrame(wait: true);
			opened.Set();
			proceed.Wait(TimeSpan.FromSeconds(10));
			rig.Engine.EndFrame();
		});
		worker.Start();
		opened.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();

		//Act
		var detach = new Thread(() => rig.Engine.DetachWindow());
		detach.Start();
		Thread.Sleep(200);
		var releasesWhileHeld = rig.Releases;
		proceed.Set();
		worker.Join(TimeSpan.FromSeconds(10));
		detach.Join(TimeSpan.FromSeconds(10));

		//Assert
		releasesWhileHeld.Should().Be(0);
		rig.Releases.Should().Be(1);
		rig.Factory.Latest!.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void a_changed_window_and_a_new_size_reconfigure_the_surface()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		var surface = rig.Factory.Latest!;

		//Act
		rig.Frame(SKColors.Red);
		var unchanged = surface.Configurations.Count;
		rig.Engine.ResizeWindow(Width, Height); // surfaceChanged (rotation reports one even at the same size)
		rig.Frame(SKColors.Red);
		var afterChange = surface.Configurations.Count;
		rig.Engine.SetPixelSize(Height, Width); // the element turned with the display
		rig.Frame(SKColors.Red);

		//Assert
		unchanged.Should().Be(1);
		afterChange.Should().Be(2);
		surface.Configurations.Should().HaveCount(3);
		(surface.Configurations[2].Width, surface.Configurations[2].Height).Should().Be((Height, Width));
		surface.PresentCount.Should().Be(4);
		rig.Factory.Created.Should().ContainSingle("a change reconfigures the same surface");
	}

	[Theory]
	[InlineData(SurfaceGetCurrentTextureStatus.Outdated)]
	[InlineData(SurfaceGetCurrentTextureStatus.Lost)]
	public void an_outdated_or_lost_surface_skips_that_present_and_is_reconfigured(SurfaceGetCurrentTextureStatus status)
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		var surface = rig.Factory.Latest!;
		surface.ScriptAcquire(status);

		//Act
		rig.Frame(SKColors.Red);
		var presentedAfterSkip = surface.PresentCount;
		rig.Frame(SKColors.Red);

		//Assert
		presentedAfterSkip.Should().Be(1);
		surface.PresentCount.Should().Be(2);
		surface.Configurations.Should().HaveCount(2);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
	}

	[Fact]
	public void a_timed_out_acquire_only_skips_that_present()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red);
		var surface = rig.Factory.Latest!;
		surface.ScriptAcquire(SurfaceGetCurrentTextureStatus.Timeout);

		//Act
		rig.Frame(SKColors.Red);
		rig.Frame(SKColors.Red);

		//Assert
		surface.PresentCount.Should().Be(2);
		surface.Configurations.Should().ContainSingle();
	}

	[Fact]
	public void a_failed_acquire_falls_back_to_readback_with_device_lost()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Factory.Latest!.ScriptAcquire(SurfaceGetCurrentTextureStatus.Error);

		//Act
		rig.Frame(SKColors.Red);
		rig.Frame(SKColors.Red); // the fallback happens at the start of this frame, which then renders on Readback
		rig.Frame(SKColors.Red);

		//Assert
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Engine.WantsWindowSurface.Should().BeFalse();
		rig.Host.Events.Should().ContainInOrder("Path:None->DirectSurface", "DeviceReady", "Path:DirectSurface->Readback", "DeviceReady");
		rig.Host.LastLostReason.Should().Contain("DirectSurface").And.Contain("could not be acquired");
		rig.Host.Presented.Should().BeGreaterThan(0);
		rig.Factory.Latest!.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void a_surface_that_cannot_be_created_falls_back_to_readback()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Factory.FailCreate = true;

		//Act
		rig.Frame(SKColors.Red);
		var second = rig.Frame(SKColors.Red);

		//Assert
		second.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Host.LastLostReason.Should().Contain("test: no surface for this window");
	}

	[Fact]
	public void a_surface_offering_no_usable_format_falls_back_to_readback_without_being_configured()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Factory.Capabilities = new WebGpuSurfaceCapabilities(
			new[] { TextureFormat.RGBA16Float }, new[] { PresentMode.Fifo }, new[] { CompositeAlphaMode.Opaque }, TextureUsage.RenderAttachment);

		//Act
		rig.Frame(SKColors.Red);
		rig.Frame(SKColors.Red);

		//Assert
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		rig.Factory.Latest!.Configurations.Should().BeEmpty();
		rig.Factory.Latest!.IsDisposed.Should().BeTrue();
		rig.Host.LastLostReason.Should().Contain("RGBA16Float");
	}

	[Fact]
	public void a_rejected_frame_is_not_presented_and_the_surface_keeps_the_previous_one()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(new SKColor(0, 255, 0)).Should().Be(WebGpuFrameOutcome.Rendered);
		var surface = rig.Factory.Latest!;

		//Act - a pipeline made for another colour format, drawn into the frame: the frame's commands fail validation
		using var wrongFormat = new TrianglePipeline(((IWebGpuDeviceSource)rig.Engine).Device, TextureFormat.RGBA8Unorm);
		rig.Engine.ClearColor = new SKColor(255, 0, 0);
		rig.Engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		wrongFormat.Draw(rig.Engine.Frame.Encoder, rig.Engine.Frame.TextureView);
		Action end = rig.Engine.EndFrame;
		end.Should().Throw<WebGpuSkiaValidationException>();

		//Assert
		surface.PresentCount.Should().Be(1);
		Centre(surface).Should().Be(((byte)0, (byte)255, (byte)0, (byte)255));
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
	}

	[Fact]
	public void a_device_options_change_replaces_a_direct_surface_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);

		//Act
		rig.Engine.DeviceOptions = new WebGpuSkiaDeviceOptions();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);

		//Assert
		rig.Host.Events.Should().Contain("DeviceLost:" + WebGpuRenderViewRules.DeviceOptionsChangedReason);
		rig.Host.Events.Count(e => e == "DeviceReady").Should().Be(2);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
		rig.Factory.Created.Should().HaveCount(2);
	}

	[Fact]
	public void require_owned_device_goes_to_readback_and_back_to_direct_surface()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);

		//Act
		rig.Engine.RequireOwnedDevice = true;
		rig.Frame(SKColors.Red);
		var owned = (rig.Engine.RenderPath, rig.Engine.WantsWindowSurface);
		rig.Engine.RequireOwnedDevice = false;
		rig.Frame(SKColors.Red);

		//Assert
		owned.Should().Be((WebGpuRenderPath.Readback, false));
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
		rig.Engine.WantsWindowSurface.Should().BeTrue();
	}

	[Fact]
	public void unloading_releases_the_surface_and_keeps_the_window_until_android_destroys_it()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var rig = new Rig();
		rig.Frame(SKColors.Red).Should().Be(WebGpuFrameOutcome.Rendered);
		var surface = rig.Factory.Latest!;

		//Act
		rig.Engine.Detach();
		var releasesAfterUnload = rig.Releases;
		rig.Engine.Attach(WebGpuDeviceGateDecision.Proceed);
		var again = rig.Frame(SKColors.Red);

		//Assert
		surface.IsDisposed.Should().BeTrue();
		releasesAfterUnload.Should().Be(0, "the window is Android's until surfaceDestroyed");
		again.Should().Be(WebGpuFrameOutcome.Rendered);
		rig.Engine.RenderPath.Should().Be(WebGpuRenderPath.DirectSurface);
		rig.Factory.Created.Should().HaveCount(2);
	}
}
