// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/WebGpuRenderEngineGpuTests.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly; (2) Attach takes no provider probe.

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using SilverAssertions;
using SkiaSharp;
using Xunit;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// The frame protocol on this machine's real WebGPU device (Readback path): pixels in, pixels out, from the threads the
/// drivers and modules use. Every test skips with the engine's reason where there is no adapter.
/// </summary>
public sealed unsafe class WebGpuRenderEngineGpuTests : IDisposable
{
	private readonly RecordingEngineHost _host = new();
	private readonly WebGpuRenderEngine _engine;
	private TrianglePipeline? _triangle;

	public WebGpuRenderEngineGpuTests()
	{
		_engine = new WebGpuRenderEngine(_host);
		_host.OnReady = () => _triangle = new TrianglePipeline(((IWebGpuDeviceSource)_engine).Device, _engine.Format);
		_host.OnLost = _ =>
		{
			_triangle?.Dispose();
			_triangle = null;
		};
	}

	public void Dispose()
	{
		_engine.Detach();
		_triangle?.Dispose();
	}

	private void AttachAt(int width, int height)
	{
		_engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)
		_engine.SetPixelSize(width, height);
	}

	private void RenderOne(bool drawTriangle)
	{
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		if (drawTriangle)
		{
			_triangle!.Draw(_engine.Frame.Encoder, _engine.Frame.TextureView);
		}

		_engine.EndFrame();
	}

	[Fact]
	public void a_frame_cleared_to_the_clear_colour_reads_back_as_that_colour()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(64, 48);
		_engine.ClearColor = new SKColor(200, 30, 40);

		//Act
		RenderOne(drawTriangle: false);

		//Assert
		_host.PictureSize.Should().Be((64, 48));
		_host.PixelAt(3, 3).Should().Be(new SKColor(200, 30, 40));
		_host.PixelAt(60, 44).Should().Be(new SKColor(200, 30, 40));
		_host.Presented.Should().Be(1);
	}

	[Fact]
	public void the_device_comes_with_the_first_frame_after_attach_and_the_path_is_readback()
	{
		//Arrange
		TestGpu.RequireAdapter();
		_engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)
		var readyBeforeFrame = _engine.IsDeviceReady;

		//Act
		var unsized = _engine.OpenFrame(wait: true); // creates the device, then finds no size
		_engine.SetPixelSize(16, 16);
		RenderOne(drawTriangle: false);

		//Assert
		readyBeforeFrame.Should().BeFalse();
		unsized.Should().Be(WebGpuFrameOutcome.NotReady);
		_host.Events.Should().Equal("Path:None->Readback", "DeviceReady");
		_engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		_engine.IsDeviceReady.Should().BeTrue();
	}

	[Fact]
	public void the_device_source_and_its_hand_off_name_the_views_own_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		RenderOne(drawTriangle: false);
		IWebGpuDeviceSource source = _engine;

		//Assert
		source.Device.IsNull.Should().BeFalse();
		source.Queue.IsNull.Should().BeFalse();
		source.HasUncapturedErrorHandler.Should().BeTrue();
		source.Backend.Should().NotBe(BackendType.Undefined);
		_engine.DeviceHandoff->Device.Should().Be(source.Device);
		_engine.DeviceHandoff->Version.Should().Be(CodebrixDeviceHandoff.CurrentVersion);
		(_engine.DeviceHandoff->Flags & CodebrixDeviceHandoff.FlagUncapturedErrorHandler).Should().NotBe(0u);
	}

	[Fact]
	public void a_triangle_drawn_into_the_frame_reaches_the_picture_over_the_clear_colour()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(80, 80);
		_engine.ClearColor = SKColors.Black;

		//Act
		RenderOne(drawTriangle: true);

		//Assert
		_host.PixelAt(40, 45).Should().Be(new SKColor(0, 255, 0));
		_host.PixelAt(2, 2).Should().Be(SKColors.Black);
	}

	[Fact]
	public void a_transparent_clear_colour_stays_transparent()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(8, 8);
		_engine.ClearColor = SKColors.Transparent;

		//Act
		RenderOne(drawTriangle: false);

		//Assert
		_host.PixelAt(4, 4)!.Value.Alpha.Should().Be((byte)0);
	}

	[Fact]
	public void the_frame_describes_its_texture_and_counts_and_times_itself()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(40, 30);

		//Act
		RenderOne(drawTriangle: false);
		Thread.Sleep(20);
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		var frame = _engine.Frame;
		var snapshot = (frame.Width, frame.Height, frame.Format, frame.FrameIndex, frame.IsOpen,
			NullTexture: frame.Texture.IsNull, NullEncoder: frame.Encoder.IsNull, frame.ElapsedSeconds, frame.DeltaSeconds);
		var handoff = frame.GetHandoff();
		var handoffWidth = handoff->Width;
		var handoffEncoder = handoff->CommandEncoder;
		frame.Dispose(); // == EndFrame

		//Assert
		snapshot.Width.Should().Be(40);
		snapshot.Height.Should().Be(30);
		snapshot.Format.Should().Be(TextureFormat.BGRA8Unorm);
		snapshot.FrameIndex.Should().Be(1L);
		snapshot.IsOpen.Should().BeTrue();
		snapshot.NullTexture.Should().BeFalse();
		snapshot.NullEncoder.Should().BeFalse();
		snapshot.DeltaSeconds.Should().BeGreaterThan(0.015);
		snapshot.ElapsedSeconds.Should().BeGreaterThanOrEqualTo(snapshot.DeltaSeconds);
		handoffWidth.Should().Be(40u);
		handoffEncoder.IsNull.Should().BeFalse();
		frame.IsOpen.Should().BeFalse();
		frame.Encoder.IsNull.Should().BeTrue();
	}

	[Fact]
	public void a_new_size_is_a_new_target_at_the_next_frame()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(64, 48);
		RenderOne(drawTriangle: false);

		//Act
		_engine.SetPixelSize(32, 16);
		RenderOne(drawTriangle: false);

		//Assert
		_host.PictureSize.Should().Be((32, 16));
	}

	[Fact]
	public void detach_tears_down_after_device_lost_and_the_next_attach_creates_a_new_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		RenderOne(drawTriangle: true);
		var firstDevice = ((IWebGpuDeviceSource)_engine).Device;

		//Act
		_engine.Detach();
		var afterDetach = (_engine.IsDeviceReady, _engine.RenderPath, NullHandoff: _engine.DeviceHandoff->Device.IsNull, _host.Events.ToArray());
		var whileDetached = _engine.OpenFrame(wait: true);
		_engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)
		RenderOne(drawTriangle: true);

		//Assert
		afterDetach.IsDeviceReady.Should().BeFalse();
		afterDetach.RenderPath.Should().Be(WebGpuRenderPath.None);
		afterDetach.NullHandoff.Should().BeTrue();
		afterDetach.Item4.Should().Equal("Path:None->Readback", "DeviceReady", "DeviceLost:The view was unloaded.", "ClearPresented", "Path:Readback->None");
		whileDetached.Should().Be(WebGpuFrameOutcome.NotReady);
		_host.Events.Count(e => e == "DeviceReady").Should().Be(2);
		_engine.Frame.FrameIndex.Should().Be(0L);
		((IWebGpuDeviceSource)_engine).Device.IsNull.Should().BeFalse();
		firstDevice.IsNull.Should().BeFalse();
		_host.PixelAt(8, 9).Should().Be(new SKColor(0, 255, 0));
	}

	[Fact]
	public void the_manual_driver_renders_from_a_worker_thread()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(32, 32);
		var testThread = Environment.CurrentManagedThreadId;
		var workerThread = 0;
		Exception? failure = null;

		//Act
		var worker = new Thread(() =>
		{
			try
			{
				workerThread = Environment.CurrentManagedThreadId;
				for (var i = 0; i < 5; i++)
				{
					RenderOne(drawTriangle: true);
				}
			}
			catch (Exception e)
			{
				failure = e;
			}
		});
		worker.Start();
		worker.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();

		//Assert
		failure.Should().BeNull();
		workerThread.Should().NotBe(testThread);
		_host.Presented.Should().Be(5);
		_host.EventThreads.Should().AllSatisfy(id => id.Should().Be(workerThread)); // DeviceReady came on the worker
		_host.PixelAt(16, 18).Should().Be(new SKColor(0, 255, 0));
	}

	[Fact]
	public void a_second_thread_waits_for_the_open_frame_and_a_built_in_driver_skips_it()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		RenderOne(drawTriangle: false);
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered); // held by the test thread
		var other = WebGpuFrameOutcome.Unavailable;
		var otherTry = WebGpuFrameOutcome.Unavailable;
		using var started = new ManualResetEventSlim();

		//Act
		var thread = new Thread(() =>
		{
			otherTry = _engine.OpenFrame(wait: false);
			started.Set();
			other = _engine.OpenFrame(wait: true);
			_engine.EndFrame();
		});
		thread.Start();
		started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
		var finishedWhileHeld = thread.Join(TimeSpan.FromMilliseconds(200));
		_engine.EndFrame();
		thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();

		//Assert
		otherTry.Should().Be(WebGpuFrameOutcome.NotReady);
		finishedWhileHeld.Should().BeFalse();
		other.Should().Be(WebGpuFrameOutcome.Rendered);
		_host.Presented.Should().Be(3);
	}

	[Fact]
	public void opening_a_second_frame_on_the_same_thread_is_refused()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(8, 8);
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);

		//Act
		Action act = () => _engine.OpenFrame(wait: true);

		//Assert
		act.Should().Throw<InvalidOperationException>().WithMessage("*twice*");
		_engine.EndFrame();
	}

	[Fact]
	public void rejected_commands_throw_keep_the_previous_picture_and_leave_the_view_usable()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(32, 32);
		_engine.ClearColor = SKColors.Blue;
		RenderOne(drawTriangle: false);
		using var wrongFormat = new TrianglePipeline(((IWebGpuDeviceSource)_engine).Device, TextureFormat.RGBA8Unorm);
		_engine.ClearColor = SKColors.Red;

		//Act
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		wrongFormat.Draw(_engine.Frame.Encoder, _engine.Frame.TextureView);
		Action end = _engine.EndFrame;

		//Assert
		end.Should().Throw<WebGpuSkiaValidationException>();
		_engine.Frame.IsOpen.Should().BeFalse();
		_host.PixelAt(16, 16).Should().Be(SKColors.Blue);
		RenderOne(drawTriangle: false);
		_host.PixelAt(16, 16).Should().Be(SKColors.Red);
	}

	[Fact]
	public void an_error_the_device_reports_replaces_the_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		RenderOne(drawTriangle: false);
		var first = ((IWebGpuDeviceSource)_engine).Device;

		//Act
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);
		var invalid = new BufferDescriptor { Usage = BufferUsage.MapRead | BufferUsage.Storage, Size = 64 };
		var buffer = Wgpu.DeviceCreateBuffer(first, &invalid);
		if (!buffer.IsNull)
		{
			Wgpu.BufferRelease(buffer);
		}

		_engine.EndFrame();
		var afterError = (_engine.IsDeviceReady, _engine.RenderPath, _host.LastLostReason);
		RenderOne(drawTriangle: false);

		//Assert
		afterError.IsDeviceReady.Should().BeFalse();
		afterError.RenderPath.Should().Be(WebGpuRenderPath.None);
		afterError.LastLostReason.Should().StartWith("The WebGPU device reported an error and was replaced:");
		_host.Events.Count(e => e == "DeviceReady").Should().Be(2);
		_engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
	}

	[Fact]
	public void a_library_driving_the_pass_protocol_gets_the_open_frames_encoder()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(8, 8);
		IWebGpuTextureTarget target = _engine;
		_engine.OpenFrame(wait: true).Should().Be(WebGpuFrameOutcome.Rendered);

		var frameEncoder = _engine.Frame.Encoder;

		//Act
		var encoder = target.BeginWebGpuPass();
		var inPass = target.IsInWebGpuPass;
		Action twice = () => target.BeginWebGpuPass();
		var description = (target.Width, target.Height, NullTexture: target.Texture.IsNull, target.Usage);
		twice.Should().Throw<InvalidOperationException>();
		target.EndWebGpuPass();
		Action endAgain = target.EndWebGpuPass;
		endAgain.Should().Throw<InvalidOperationException>();
		_engine.EndFrame();

		//Assert
		encoder.Should().Be(frameEncoder);
		encoder.IsNull.Should().BeFalse();
		inPass.Should().BeTrue();
		description.Width.Should().Be(8);
		description.Height.Should().Be(8);
		description.NullTexture.Should().BeFalse();
		(description.Usage & TextureUsage.RenderAttachment).Should().Be(TextureUsage.RenderAttachment);
		target.IsInWebGpuPass.Should().BeFalse();
	}

	[Fact]
	public void a_native_module_drives_frames_through_the_hand_off_from_its_own_thread()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(48, 48);
		_engine.ClearColor = SKColors.Black;
		var view = _engine.ViewHandoff;
		var results = new string[3];
		Exception? failure = null;

		//Act
		// The "module": it sees only the C struct - the function pointers, the context, the device hand-off with its
		// function table - and records through that table, never through this process's binding.
		var module = new Thread(() =>
		{
			try
			{
				for (var i = 0; i < 3; i++)
				{
					var frame = view->BeginFrame(view->Context);
					if (frame == null)
					{
						results[i] = "skipped";
						continue;
					}

					// Layered validation: the view's struct first, then the device hand-off it points at.
					var device = view->Device;
					if (view->Size < (uint)sizeof(WebGpuRenderViewHandoff) || view->Version != WebGpuRenderViewHandoff.CurrentVersion
						|| device == null || device->Size < (uint)sizeof(CodebrixDeviceHandoff) || device->Version != CodebrixDeviceHandoff.CurrentVersion)
					{
						results[i] = "refused";
						view->EndFrame(view->Context);
						continue;
					}

					var procs = device->Procs;
					results[i] = $"{frame->Width}x{frame->Height} v{frame->Version} device:{!device->Device.IsNull} procs:{procs->FunctionCount}";
					_triangle!.Draw(frame->CommandEncoder, frame->TextureView, procs);
					view->EndFrame(view->Context);
				}
			}
			catch (Exception e)
			{
				failure = e;
			}
		});
		module.Start();
		module.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();

		//Assert
		failure.Should().BeNull();
		results.Should().AllSatisfy(r => r.Should().StartWith("48x48 v1 device:True procs:"));
		_host.Presented.Should().Be(3);
		_host.PixelAt(24, 26).Should().Be(new SKColor(0, 255, 0));
		_host.PixelAt(1, 1).Should().Be(SKColors.Black);
	}

	[Fact]
	public void the_device_info_reports_the_real_adapter_and_the_devices_features_and_limits()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		var before = _engine.DeviceInfo;
		WebGpuDeviceInfo? atReady = null;
		_host.OnReady += () => atReady = _engine.DeviceInfo;

		//Act
		RenderOne(drawTriangle: false);
		var info = _engine.DeviceInfo!;
		IWebGpuDeviceSource source = _engine;
		var backend = source.Backend;
		var (deviceHas, adapterFeatures) = DeviceAndAdapterFeatures(source.Device, source.Adapter);
		_engine.Detach();

		//Assert
		before.Should().BeNull();
		atReady.Should().BeSameAs(info);
		info.AdapterName.Should().NotBeNullOrEmpty();
		info.Backend.Should().Be(backend);
		info.Backend.Should().NotBe(BackendType.Undefined);
		info.AdapterType.Should().NotBe((AdapterType)0);
		// The features are exactly what the device answers to DeviceHasFeature, and a subset of its adapter's
		// (a device has only the features it was created with: the owned route asks for none today).
		info.Features.Select(f => (uint)f).Concat(info.NativeFeatures.Select(f => (uint)f)).Should().BeEquivalentTo(deviceHas);
		info.Features.Select(f => (uint)f).Should().BeSubsetOf(adapterFeatures);
		adapterFeatures.Should().NotBeEmpty();
		// The adapter's offer is reported beside them, so "not enabled" can be told from "not available".
		info.AdapterFeatures.Select(f => (uint)f).Concat(info.AdapterNativeFeatures.Select(f => (uint)f)).Should().BeEquivalentTo(adapterFeatures);
		info.AdapterHasFeature(info.AdapterFeatures[0]).Should().BeTrue();
		info.HasFeature(info.AdapterFeatures[0]).Should().Be(deviceHas.Contains((uint)info.AdapterFeatures[0]));
		info.AdapterLimits.MaxTextureDimension2D.Should().BeGreaterThanOrEqualTo(info.Limits.MaxTextureDimension2D);
		// The limits are the device's real ones: at least WebGPU's guaranteed minimums.
		info.Limits.MaxTextureDimension2D.Should().BeGreaterThanOrEqualTo(8192u);
		info.Limits.MaxBindGroups.Should().BeGreaterThanOrEqualTo(4u);
		info.Limits.MaxBufferSize.Should().BeGreaterThanOrEqualTo(256UL * 1024 * 1024);
		info.ToString().Should().Contain(info.AdapterName);
		_engine.DeviceInfo.Should().BeNull();
	}

	// What the device says it has, feature by feature (every standard value and every adapter-reported one), and what
	// its adapter offers.
	private static (uint[] DeviceHas, uint[] Adapter) DeviceAndAdapterFeatures(Device device, Adapter adapter)
	{
		SupportedFeatures supported = default;
		Wgpu.AdapterGetFeatures(adapter, &supported);
		var adapterFeatures = new uint[(int)supported.FeatureCount];
		for (var i = 0; i < adapterFeatures.Length; i++)
		{
			adapterFeatures[i] = (uint)supported.Features[i];
		}

		Wgpu.SupportedFeaturesFreeMembers(supported);
		var candidates = Enum.GetValues<FeatureName>().Select(f => (uint)f).Concat(adapterFeatures).Distinct();
		var deviceHas = candidates.Where(f => Wgpu.DeviceHasFeature(device, (FeatureName)f) == 1u).ToArray();
		return (deviceHas, adapterFeatures);
	}

	[Fact]
	public void the_device_options_decide_how_the_owned_device_is_created()
	{
		//Arrange
		TestGpu.RequireAdapter();
		_engine.DeviceOptions = new WebGpuSkiaDeviceOptions
		{
			PreferredBackend = OperatingSystem.IsMacOS() ? WebGpuSkiaBackend.Direct3D12 : WebGpuSkiaBackend.Metal,
			AllowFallbackBackend = false,
		};
		AttachAt(16, 16);

		//Act
		var refused = _engine.OpenFrame(wait: true);
		var lostWithOptions = _host.LastLostReason;
		var latched = _engine.OpenFrame(wait: true);
		_engine.DeviceOptions = null; // a working choice: tried again at the next frame
		RenderOne(drawTriangle: false);

		//Assert
		refused.Should().Be(WebGpuFrameOutcome.Unavailable);
		latched.Should().Be(WebGpuFrameOutcome.Unavailable);
		lostWithOptions.Should().NotBeNullOrEmpty();
		_host.Events.Count(e => e.StartsWith("DeviceLost:")).Should().Be(1);
		_engine.IsDeviceReady.Should().BeTrue();
		_engine.RenderPath.Should().Be(WebGpuRenderPath.Readback);
		_host.Events.Last().Should().Be("DeviceReady");
	}

	[Fact]
	public void changing_the_device_options_replaces_the_device_at_the_next_frame()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(32, 32);
		_engine.ClearColor = SKColors.Black;
		RenderOne(drawTriangle: false);
		var firstDevice = ((IWebGpuDeviceSource)_engine).Device;
		var view = _engine.ViewHandoff;
		var deviceHandoff = view->Device;

		//Act
		_engine.DeviceOptions = new WebGpuSkiaDeviceOptions { PowerPreference = PowerPreference.HighPerformance };
		var stillOld = ((IWebGpuDeviceSource)_engine).Device;
		RenderOne(drawTriangle: true);

		//Assert
		stillOld.Should().Be(firstDevice);
		_host.Events.Should().Equal(
			"Path:None->Readback", "DeviceReady",
			"DeviceLost:Device options changed.", "ClearPresented", "Path:Readback->None",
			"Path:None->Readback", "DeviceReady");
		((IWebGpuDeviceSource)_engine).Device.IsNull.Should().BeFalse();
		_engine.DeviceInfo.Should().NotBeNull();
		(view->Device == deviceHandoff).Should().BeTrue();          // the hand-off never moves...
		deviceHandoff->Device.Should().Be(((IWebGpuDeviceSource)_engine).Device); // ...and names the new device
		_host.PixelAt(16, 18).Should().Be(new SKColor(0, 255, 0));
	}

	[Fact]
	public void setting_require_owned_device_without_the_latch_keeps_the_device()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		RenderOne(drawTriangle: false);

		//Act
		_engine.RequireOwnedDevice = true;
		RenderOne(drawTriangle: false);

		//Assert
		_host.Events.Should().Equal("Path:None->Readback", "DeviceReady");
		_engine.Device!.Route.Should().Be(WebGpuSkiaDeviceRoute.Owned);
	}

	[Fact]
	public void on_demand_renders_exactly_once_per_invalidate()
	{
		//Arrange
		TestGpu.RequireAdapter();
		using var owner = new ThreadFrameTimer();
		var frames = 0;
		WebGpuFrameScheduler? scheduler = null;
		owner.Invoke(() =>
		{
			scheduler = new WebGpuFrameScheduler(owner, () => RunBuiltIn(ref frames)) { Driver = WebGpuFrameDriver.OnDemand };
			AttachAt(16, 16);
			scheduler.Attach();
		});
		WaitFor(() => Volatile.Read(ref frames) == 1);

		//Act
		owner.Invoke(() =>
		{
			scheduler!.Invalidate();
			scheduler.Invalidate();
			scheduler.Invalidate();
		});
		WaitFor(() => Volatile.Read(ref frames) == 2);
		Thread.Sleep(150);
		var afterCoalesced = Volatile.Read(ref frames);
		scheduler!.Invalidate(); // from this (non-owner) thread
		WaitFor(() => Volatile.Read(ref frames) == 3);
		Thread.Sleep(150);
		owner.Invoke(scheduler.Detach);

		//Assert
		afterCoalesced.Should().Be(2);
		Volatile.Read(ref frames).Should().Be(3);
		_host.Presented.Should().Be(3);
	}

	[Fact]
	public void continuous_reaches_its_target_rate_without_a_backlog()
	{
		// Deterministic: REAL frames on the real device, scheduled by the real scheduler, but on a virtual clock (1 tick
		// = 1 microsecond) and a hand-run timer, so the machine's load cannot change the outcome. Every timed tick the
		// scheduler asks for lands exactly when it asked; the clock advances only by what the scheduler requested (or by
		// a frame's simulated cost). The wall-clock twin below adds a live, load-proof bound.
		//Arrange
		TestGpu.RequireAdapter();
		var timer = new FakeFrameTimer();
		long now = 1_000_000;
		var frames = 0;
		var frameCost = 0L;
		var scheduler = new WebGpuFrameScheduler(timer, () =>
		{
			var outcome = RunBuiltIn(ref frames);
			now += frameCost;
			return outcome;
		}, () => now, 1_000_000) { TargetFrameRate = 60 };
		AttachAt(128, 128);
		scheduler.Attach();
		timer.RunPosted().Should().Be(1); // the first frame: the device is created and the first picture presented
		frames.Should().Be(1);

		//Act 1: two virtual seconds at 60 frames a second, every frame taking a quarter of a period.
		frameCost = 4_000;
		var deadlines = 0;
		while (now < 3_000_000)
		{
			timer.PendingDelay.Should().NotBeNull("Continuous always has exactly one timed tick waiting");
			timer.PostedCount.Should().Be(0, "an on-time frame never queues an extra tick");
			now += (long)(timer.PendingDelay!.Value.Ticks / 10); // the timer runs its callback exactly when asked
			timer.RunTimed().Should().BeTrue();
			deadlines++;
		}

		//Assert 1: one real frame per period - 120 periods in two seconds - and never more.
		deadlines.Should().Be(120);
		frames.Should().Be(121);
		_host.Presented.Should().Be(121);

		//Act 2: one frame takes three and a half periods (a stall); then frames are cheap again.
		frameCost = 58_000;
		now += (long)(timer.PendingDelay!.Value.Ticks / 10);
		timer.RunTimed().Should().BeTrue();
		frameCost = 1_000;

		//Assert 2: the late frame queues ONE tick to run at once (latest wins: the missed frames are dropped, not
		// replayed), and the rate then resumes from the new deadline with one timed tick at a time.
		frames.Should().Be(122);
		timer.PostedCount.Should().Be(1);
		timer.PendingDelay.Should().BeNull();
		timer.RunPosted().Should().Be(1);
		frames.Should().Be(123);
		timer.PostedCount.Should().Be(0);
		timer.PendingDelay.Should().Be(TimeSpan.FromTicks((16_667 - 1_000) * 10));

		scheduler.Detach();
		scheduler.IsTickPending.Should().BeFalse();
	}

	[Fact]
	public void continuous_on_a_real_timer_never_runs_above_its_target_rate()
	{
		// The live twin of the test above, on a real thread and a real timer against the wall clock. Load can only
		// SLOW a frame loop, so the one bound a busy machine cannot break is the upper one: never above the target
		// (a catch-up policy would burst past it). The measured rate is written to the output for the record.
		//Arrange
		TestGpu.RequireAdapter();
		using var owner = new ThreadFrameTimer();
		var frames = 0;
		WebGpuFrameScheduler? scheduler = null;
		owner.Invoke(() =>
		{
			scheduler = new WebGpuFrameScheduler(owner, () => RunBuiltIn(ref frames)) { TargetFrameRate = 60 };
			AttachAt(128, 128);
		});
		owner.Invoke(scheduler!.Attach);
		WaitFor(() => Volatile.Read(ref frames) >= 3); // the device exists and frames flow

		//Act
		var start = Volatile.Read(ref frames);
		var clock = Stopwatch.StartNew();
		Thread.Sleep(2000);
		var counted = Volatile.Read(ref frames) - start;
		var seconds = clock.Elapsed.TotalSeconds;
		owner.Invoke(scheduler.Detach);
		var rate = counted / seconds;
		TestContext.Current.TestOutputHelper?.WriteLine($"Continuous at TargetFrameRate 60: {counted} frames in {seconds:F2} s = {rate:F1} fps (128x128 Readback).");

		//Assert
		counted.Should().BeGreaterThan(0);
		rate.Should().BeLessThanOrEqualTo(61.5);
		scheduler.IsTickPending.Should().BeFalse();
	}

	[Fact]
	public void a_recorded_picture_keeps_its_pixels_while_later_frames_render()
	{
		// The desktop heads record the window frame (with the view's picture in it) on the user-interface thread and
		// replay the recording later on the render thread. Later frames must not read back into the pixels that
		// recording still draws (they used to: two alternating bitmaps, so the third frame rewrote the first's).
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		_engine.ClearColor = SKColors.Red;
		RenderOne(drawTriangle: false);
		using var recording = _host.RecordLatestPicture()!;

		//Act
		foreach (var colour in new[] { SKColors.Lime, SKColors.Blue, SKColors.Yellow, SKColors.Blue })
		{
			_engine.ClearColor = colour;
			RenderOne(drawTriangle: false);
		}

		//Assert
		Replay(recording, 16, 16).GetPixel(8, 8).Should().Be(SKColors.Red);
		_host.PixelAt(8, 8).Should().Be(SKColors.Blue);
	}

	[Fact]
	public void a_picture_recorded_before_a_resize_and_a_teardown_still_plays_after_them()
	{
		// A recording can outlive a resize (a new pixel size: the old buffers are let go) and the view leaving the tree
		// (the presenter is disposed). Its pixels must stay alive until the recording is gone - they used to be freed
		// underneath it, which crashed the render thread's replay (a use-after-free).
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(16, 16);
		_engine.ClearColor = SKColors.Red;
		RenderOne(drawTriangle: false);
		using var recording = _host.RecordLatestPicture()!;

		//Act
		_engine.SetPixelSize(40, 30);
		_engine.ClearColor = SKColors.Blue;
		RenderOne(drawTriangle: false);
		RenderOne(drawTriangle: false);
		_engine.Detach();
		GC.Collect();
		GC.WaitForPendingFinalizers();

		//Assert
		Replay(recording, 16, 16).GetPixel(8, 8).Should().Be(SKColors.Red);
	}

	[Fact]
	public void steady_state_reads_back_into_two_buffers_and_allocates_no_more()
	{
		//Arrange
		TestGpu.RequireAdapter();
		AttachAt(32, 32);

		//Act: the host holds only the newest picture, as the surface does when nothing recorded the older ones.
		for (var i = 0; i < 30; i++)
		{
			RenderOne(drawTriangle: false);
		}

		//Assert
		((WebGpuReadbackPresenter)_engine.Presenter!).BufferCount.Should().Be(2);
	}

	private static SKBitmap Replay(SKPicture recording, int width, int height)
	{
		var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(SKColors.Transparent);
		canvas.DrawPicture(recording);
		return bitmap;
	}

	// The built-in drivers' frame, as the view runs it (minus the events).
	private WebGpuFrameOutcome RunBuiltIn(ref int frames)
	{
		var outcome = _engine.OpenFrame(wait: false);
		if (outcome != WebGpuFrameOutcome.Rendered)
		{
			return outcome;
		}

		_engine.EndFrame();
		Interlocked.Increment(ref frames);
		return outcome;
	}

	private static void WaitFor(Func<bool> condition)
	{
		var clock = Stopwatch.StartNew();
		while (!condition())
		{
			if (clock.Elapsed > TimeSpan.FromSeconds(15))
			{
				throw new TimeoutException("The condition was not met within 15 s.");
			}

			Thread.Sleep(5);
		}
	}
}
