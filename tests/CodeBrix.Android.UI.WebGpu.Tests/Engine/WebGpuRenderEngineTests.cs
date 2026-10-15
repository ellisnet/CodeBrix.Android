// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/WebGpuRenderEngineTests.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly; (2) Attach takes no provider probe.

#nullable enable

using System;
using System.Linq;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Skia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// The engine's lifetime rules that hold with or without a GPU: nothing before attach, the head's gate, a machine
/// without an adapter, and protocol misuse.
/// </summary>
public class WebGpuRenderEngineTests
{
	private readonly RecordingEngineHost _host = new();
	private int _devicesCreated;

	private WebGpuRenderEngine Create(Func<WebGpuSkiaDevice> factory) => new(_host, _ =>
	{
		_devicesCreated++;
		return factory();
	});

	[Fact]
	public void no_frame_and_no_device_before_attach()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		engine.SetPixelSize(64, 64);

		//Act
		var outcome = engine.OpenFrame(wait: true);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.NotReady);
		_devicesCreated.Should().Be(0);
		engine.RenderPath.Should().Be(WebGpuRenderPath.None);
		engine.Frame.IsOpen.Should().BeFalse();
	}

	[Fact]
	public void a_gate_that_says_no_device_reports_its_reason_once_and_creates_nothing()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		engine.Attach(new WebGpuDeviceGateDecision(false, "PlayTestOptions.WebGpu is Unavailable for this launch.", null, false)); // ANDROID PORT (2)
		engine.SetPixelSize(64, 64);

		//Act
		var first = engine.OpenFrame(wait: true);
		var second = engine.OpenFrame(wait: true);

		//Assert
		first.Should().Be(WebGpuFrameOutcome.Unavailable);
		second.Should().Be(WebGpuFrameOutcome.Unavailable);
		_devicesCreated.Should().Be(0);
		_host.Events.Should().Equal("DeviceLost:PlayTestOptions.WebGpu is Unavailable for this launch.");
		engine.RenderPath.Should().Be(WebGpuRenderPath.None);
		engine.IsDeviceReady.Should().BeFalse();
	}

	[Fact]
	public void a_failed_gate_is_swallowed_into_no_device()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		engine.Attach(new WebGpuDeviceGateDecision(false, "No WebGPU provider is registered.", null, true)); // ANDROID PORT (2)
		engine.SetPixelSize(8, 8);

		//Act
		var outcome = engine.OpenFrame(wait: true);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Unavailable);
		_host.LastLostReason.Should().Be("No WebGPU provider is registered.");
	}

	[Fact]
	public void no_adapter_reports_the_engines_reason_once_until_the_next_attach()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)
		engine.SetPixelSize(64, 64);

		//Act
		var first = engine.OpenFrame(wait: true);
		var second = engine.OpenFrame(wait: true);
		var lostAfterTwoFrames = _host.Events.Count(e => e.StartsWith("DeviceLost:", StringComparison.Ordinal));
		engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)
		var third = engine.OpenFrame(wait: true);

		//Assert
		first.Should().Be(WebGpuFrameOutcome.Unavailable);
		second.Should().Be(WebGpuFrameOutcome.Unavailable);
		third.Should().Be(WebGpuFrameOutcome.Unavailable);
		lostAfterTwoFrames.Should().Be(1);
		_devicesCreated.Should().Be(2);
		_host.LastLostReason.Should().NotBeNullOrEmpty();
		engine.RenderPath.Should().Be(WebGpuRenderPath.None);
	}

	[Fact]
	public void a_device_factory_that_throws_is_reported_as_no_device()
	{
		//Arrange
		var engine = Create(() => throw new InvalidOperationException("boom"));
		engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)

		//Act
		var outcome = engine.OpenFrame(wait: true);

		//Assert
		outcome.Should().Be(WebGpuFrameOutcome.Unavailable);
		_host.LastLostReason.Should().Contain("boom");
	}

	[Fact]
	public void ending_a_frame_that_was_never_opened_is_refused()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);

		//Act
		Action act = engine.EndFrame;

		//Assert
		act.Should().Throw<InvalidOperationException>().WithMessage("*without BeginFrame*");
	}

	[Fact]
	public void detach_without_a_device_only_settles_the_path()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		engine.Attach(WebGpuDeviceGateDecision.Proceed); // ANDROID PORT (2)

		//Act
		engine.Detach();

		//Assert
		engine.IsLive.Should().BeFalse();
		_host.Events.Should().BeEmpty();
	}

	[Fact]
	public void the_texture_target_is_empty_outside_a_frame()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		CodeBrix.WebGpu.Native.IWebGpuTextureTarget target = engine;

		//Act
		var encoder = target.BeginWebGpuPass();

		//Assert
		encoder.IsNull.Should().BeTrue();
		target.Texture.IsNull.Should().BeTrue();
		target.Width.Should().Be(0);
		target.SampleCount.Should().Be(1);
		target.Format.Should().Be(CodeBrix.WebGpu.Native.TextureFormat.BGRA8Unorm);
		target.IsInWebGpuPass.Should().BeFalse();
	}

	[Fact]
	public unsafe void the_view_hand_off_is_filled_once_with_the_thunks_and_a_context()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);

		//Act
		var handoff = engine.ViewHandoff;

		//Assert
		handoff->Size.Should().Be(40u);
		handoff->Version.Should().Be(WebGpuRenderViewHandoff.CurrentVersion);
		((nint)handoff->Context).Should().NotBe(0);
		((nint)handoff->BeginFrame).Should().NotBe(0);
		((nint)handoff->EndFrame).Should().NotBe(0);
		(engine.DeviceHandoff == handoff->Device).Should().BeTrue();
		(handoff->Device != null).Should().BeTrue();
		((nint)handoff->Device % 8).Should().Be(0);
		handoff->Device->Device.IsNull.Should().BeTrue();
		((byte*)handoff->Device >= (byte*)handoff + sizeof(WebGpuRenderViewHandoff)).Should().BeTrue();
	}

	[Fact]
	public unsafe void begin_frame_through_the_hand_off_returns_null_when_the_view_cannot_render()
	{
		//Arrange
		var engine = Create(TestGpu.CreateAbsentDevice);
		var handoff = engine.ViewHandoff;

		//Act
		var frame = handoff->BeginFrame(handoff->Context);

		//Assert
		(frame == null).Should().BeTrue();
	}
}
