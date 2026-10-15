#nullable enable

using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.WebGpu.Native;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests;

/// <summary>
/// The rules of the Android DirectSurface render path: the window surface's configuration chosen from its capabilities
/// (never one they do not allow - the engine ends the process on that), what an acquire status means, when the path is
/// taken or waited for, and when the platform view hosts a window surface.
/// </summary>
public sealed class WebGpuSurfaceRulesTests
{
	private const TextureUsage AllUsages = TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc | TextureUsage.CopyDst;

	private static WebGpuSurfaceCapabilities Capabilities(TextureFormat[] formats, PresentMode[]? modes = null, CompositeAlphaMode[]? alpha = null, TextureUsage usages = AllUsages) =>
		new(formats, modes ?? new[] { PresentMode.Mailbox, PresentMode.Fifo }, alpha ?? new[] { CompositeAlphaMode.Opaque }, usages);

	[Fact]
	public void the_phones_formats_without_bgra_give_rgba8_unorm_fifo_opaque_render_attachment()
	{
		//Arrange - what the Pixel 4 XL (Adreno 640) reported for its window surface
		var capabilities = Capabilities(new[] { TextureFormat.RGBA8UnormSrgb, TextureFormat.RGBA8Unorm, TextureFormat.RGBA16Float, TextureFormat.RGB10A2Unorm });

		//Act
		var chosen = WebGpuSurfaceRules.TryChooseConfiguration(capabilities, out var configuration, out var reason);

		//Assert
		chosen.Should().BeTrue();
		reason.Should().BeNull();
		configuration.Format.Should().Be(TextureFormat.RGBA8Unorm);
		configuration.PresentMode.Should().Be(PresentMode.Fifo);
		configuration.AlphaMode.Should().Be(CompositeAlphaMode.Opaque);
		configuration.Usage.Should().Be(TextureUsage.RenderAttachment);
	}

	[Fact]
	public void bgra8_unorm_is_preferred_when_the_surface_offers_it()
	{
		//Arrange
		var capabilities = Capabilities(new[] { TextureFormat.RGBA8Unorm, TextureFormat.BGRA8Unorm });

		//Act
		WebGpuSurfaceRules.TryChooseConfiguration(capabilities, out var configuration, out _).Should().BeTrue();

		//Assert
		configuration.Format.Should().Be(TextureFormat.BGRA8Unorm);
	}

	[Fact]
	public void a_ten_bit_unorm_format_is_used_when_no_eight_bit_one_is_offered()
	{
		//Arrange
		var capabilities = Capabilities(new[] { TextureFormat.RGBA16Float, TextureFormat.RGB10A2Unorm });

		//Act
		WebGpuSurfaceRules.TryChooseConfiguration(capabilities, out var configuration, out _).Should().BeTrue();

		//Assert
		configuration.Format.Should().Be(TextureFormat.RGB10A2Unorm);
	}

	[Fact]
	public void srgb_and_float_formats_alone_are_refused_with_the_formats_named()
	{
		//Arrange
		var capabilities = Capabilities(new[] { TextureFormat.RGBA8UnormSrgb, TextureFormat.RGBA16Float });

		//Act
		var chosen = WebGpuSurfaceRules.TryChooseConfiguration(capabilities, out _, out var reason);

		//Assert
		chosen.Should().BeFalse();
		reason.Should().Contain("RGBA8UnormSrgb");
		reason.Should().Contain("RGBA16Float");
	}

	[Fact]
	public void a_surface_with_no_formats_is_refused()
	{
		//Act
		var chosen = WebGpuSurfaceRules.TryChooseConfiguration(Capabilities(new TextureFormat[0]), out _, out var reason);

		//Assert
		chosen.Should().BeFalse();
		reason.Should().Contain("no formats");
	}

	[Fact]
	public void a_surface_with_no_present_modes_is_refused()
	{
		//Act
		var chosen = WebGpuSurfaceRules.TryChooseConfiguration(Capabilities(new[] { TextureFormat.RGBA8Unorm }, new PresentMode[0]), out _, out var reason);

		//Assert
		chosen.Should().BeFalse();
		reason.Should().Contain("present modes");
	}

	[Fact]
	public void a_surface_whose_frames_cannot_be_render_attachments_is_refused()
	{
		//Act
		var chosen = WebGpuSurfaceRules.TryChooseConfiguration(Capabilities(new[] { TextureFormat.RGBA8Unorm }, usages: TextureUsage.CopyDst), out _, out var reason);

		//Assert
		chosen.Should().BeFalse();
		reason.Should().Contain("render attachments");
	}

	[Fact]
	public void without_fifo_the_first_mode_is_used_and_without_opaque_auto()
	{
		//Act
		WebGpuSurfaceRules.TryChooseConfiguration(
			Capabilities(new[] { TextureFormat.RGBA8Unorm }, new[] { PresentMode.Mailbox, PresentMode.Immediate }, new[] { CompositeAlphaMode.Premultiplied }),
			out var configuration, out _).Should().BeTrue();

		//Assert
		configuration.PresentMode.Should().Be(PresentMode.Mailbox);
		configuration.AlphaMode.Should().Be(CompositeAlphaMode.Auto);
	}

	[Theory]
	[InlineData(1, 1, 0u, true)]
	[InlineData(1328, 2371, 0u, true)]
	[InlineData(8192, 8192, 0u, true)]
	[InlineData(8193, 10, 0u, false)]
	[InlineData(4097, 10, 4096u, false)]
	[InlineData(0, 10, 0u, false)]
	[InlineData(10, 0, 0u, false)]
	[InlineData(-1, 10, 0u, false)]
	public void sizes_are_configurable_from_one_pixel_to_the_texture_limit(int width, int height, uint limit, bool expected)
	{
		//Act
		var configurable = WebGpuSurfaceRules.IsConfigurableSize(width, height, limit);

		//Assert
		configurable.Should().Be(expected);
	}

	[Theory]
	[InlineData(SurfaceGetCurrentTextureStatus.SuccessOptimal, (int)WebGpuSurfaceAcquireAction.Present)]
	[InlineData(SurfaceGetCurrentTextureStatus.SuccessSuboptimal, (int)WebGpuSurfaceAcquireAction.Present)]
	[InlineData(SurfaceGetCurrentTextureStatus.Outdated, (int)WebGpuSurfaceAcquireAction.Reconfigure)]
	[InlineData(SurfaceGetCurrentTextureStatus.Lost, (int)WebGpuSurfaceAcquireAction.Reconfigure)]
	[InlineData(SurfaceGetCurrentTextureStatus.Timeout, (int)WebGpuSurfaceAcquireAction.Skip)]
	[InlineData(SurfaceGetCurrentTextureStatus.Error, (int)WebGpuSurfaceAcquireAction.Fail)]
	public void each_acquire_status_has_its_action(SurfaceGetCurrentTextureStatus status, int expected)
	{
		//Act
		var action = WebGpuSurfaceRules.ActionFor(status);

		//Assert
		action.Should().Be((WebGpuSurfaceAcquireAction)expected);
	}

	[Theory]
	// latch, requireOwned, failed, host, window, waited ms -> decision
	[InlineData(false, false, false, true, true, 0.0, (int)WebGpuDirectSurfaceDecision.Readback)]
	[InlineData(true, true, false, true, true, 0.0, (int)WebGpuDirectSurfaceDecision.Readback)]
	[InlineData(true, false, true, true, true, 0.0, (int)WebGpuDirectSurfaceDecision.Readback)]
	[InlineData(true, false, false, false, false, 0.0, (int)WebGpuDirectSurfaceDecision.ReadbackNoWindowHost)]
	[InlineData(true, false, false, true, true, 0.0, (int)WebGpuDirectSurfaceDecision.DirectSurface)]
	[InlineData(true, false, false, true, true, 99999.0, (int)WebGpuDirectSurfaceDecision.DirectSurface)]
	[InlineData(true, false, false, true, false, 0.0, (int)WebGpuDirectSurfaceDecision.Wait)]
	[InlineData(true, false, false, true, false, 1999.0, (int)WebGpuDirectSurfaceDecision.Wait)]
	[InlineData(true, false, false, true, false, 2000.0, (int)WebGpuDirectSurfaceDecision.ReadbackWindowNeverCame)]
	[InlineData(true, false, false, true, false, -1.0, (int)WebGpuDirectSurfaceDecision.ReadbackWindowNeverCame)]
	public void the_direct_surface_decision(bool latch, bool requireOwned, bool failed, bool host, bool window, double waited, int expected)
	{
		//Act
		var decision = WebGpuSurfaceRules.Decide(latch, requireOwned, failed, host, window, waited);

		//Assert
		decision.Should().Be((WebGpuDirectSurfaceDecision)expected);
	}

	[Theory]
	[InlineData(false, false, false, false)]
	[InlineData(true, false, false, true)]
	[InlineData(true, true, false, false)]
	[InlineData(true, false, true, false)]
	public void a_window_surface_is_hosted_only_while_the_direct_path_may_be_taken(bool latch, bool requireOwned, bool failed, bool expected)
	{
		//Act
		var wants = WebGpuSurfaceRules.WantsWindowSurface(latch, requireOwned, failed);

		//Assert
		wants.Should().Be(expected);
	}

	[Theory]
	[InlineData(WebGpuRenderPath.None, false)]
	[InlineData(WebGpuRenderPath.Readback, true)]
	[InlineData(WebGpuRenderPath.DirectSurface, true)]
	[InlineData(WebGpuRenderPath.DirectVulkan, false)]
	public void a_device_options_change_replaces_every_device_the_view_owns(WebGpuRenderPath path, bool expected)
	{
		//Act
		var recreates = WebGpuSurfaceRules.DeviceOptionsChangeRecreatesDevice(path);

		//Assert
		recreates.Should().Be(expected);
	}

	[Fact]
	public void the_window_wait_is_two_seconds_and_its_reason_says_so()
	{
		//Assert
		WebGpuSurfaceRules.WindowWaitMilliseconds.Should().Be(2000.0);
		WebGpuSurfaceRules.WindowNeverCameReason.Should().Contain("2000 ms");
		WebGpuRenderViewRules.DirectPathFallbackMessage(WebGpuRenderPath.DirectSurface, WebGpuSurfaceRules.NoWindowHostReason)
			.Should().Contain("DirectSurface").And.Contain("Readback");
	}
}
