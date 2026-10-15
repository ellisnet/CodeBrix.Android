#nullable enable

using System;
using CodeBrix.WebGpu.Native;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>What the DirectSurface presenter does with the status of a frame it tried to acquire.</summary>
internal enum WebGpuSurfaceAcquireAction
{
	/// <summary>The frame is usable: draw onto it and present it.</summary>
	Present,

	/// <summary>The surface no longer matches its window (outdated, lost): reconfigure it and skip this present.</summary>
	Reconfigure,

	/// <summary>No frame this time (timed out): skip this present, try again with the next frame.</summary>
	Skip,

	/// <summary>The surface cannot present any more: the view falls back to Readback.</summary>
	Fail,
}

/// <summary>What the render engine does about the DirectSurface path when it is about to create a device.</summary>
internal enum WebGpuDirectSurfaceDecision
{
	/// <summary>Readback: direct GPU rendering is off, the view requires its own Readback device, or the direct path
	/// already failed since the view was loaded.</summary>
	Readback,

	/// <summary>Readback, with one log line: the application asked for direct GPU rendering but the view has no window
	/// surface under it (no platform view that hosts one).</summary>
	ReadbackNoWindowHost,

	/// <summary>Readback, with one log line: the window surface did not appear within <see cref="WebGpuSurfaceRules.WindowWaitMilliseconds"/>.</summary>
	ReadbackWindowNeverCame,

	/// <summary>Render no frame yet: the platform view hosts a window surface that has not been created yet.</summary>
	Wait,

	/// <summary>DirectSurface: present straight to the window surface.</summary>
	DirectSurface,
}

/// <summary>
/// The rules of the Android DirectSurface render path, with no WebGPU object in them: which configuration a window
/// surface gets, what an acquire status means, and when the path is taken. The presenter and the engine apply them;
/// the host tests check them.
/// </summary>
internal static class WebGpuSurfaceRules
{
	/// <summary>
	/// How long, from the moment the view is loaded (or its window surface host appeared), its first device waits for the
	/// window surface before settling for Readback. A SurfaceView's surface is created on the frame after it is attached,
	/// so this is normally a few milliseconds.
	/// </summary>
	internal const double WindowWaitMilliseconds = 2000.0;

	/// <summary>
	/// The window surface formats the presenter can draw the view's frames onto, best first. The frames are BGRA8Unorm (the
	/// format every render path renders in) and hold display-ready (sRGB-encoded) values, so the surface must store values
	/// unchanged: a UNORM format, never an *Srgb one (which would encode them a second time) nor a float one (which the
	/// compositor reads as linear).
	/// </summary>
	internal static readonly TextureFormat[] PreferredFormats =
	{
		TextureFormat.BGRA8Unorm,
		TextureFormat.RGBA8Unorm,
		TextureFormat.RGB10A2Unorm,
	};

	/// <summary>
	/// Chooses the window surface's configuration from its capabilities: the first of <see cref="PreferredFormats"/> it
	/// supports, Fifo (synchronised with the display, always supported where any mode is) else its first mode, Opaque
	/// when listed else Auto, and RenderAttachment as the frames' usage (all the presenter's pass needs).
	/// </summary>
	/// <param name="capabilities">The surface's capabilities.</param>
	/// <param name="configuration">The configuration (default when there is none).</param>
	/// <param name="reason">Why the surface cannot be used, or null.</param>
	/// <returns>True when a configuration the capabilities allow exists.</returns>
	internal static bool TryChooseConfiguration(WebGpuSurfaceCapabilities capabilities, out WebGpuSurfaceConfiguration configuration, out string? reason)
	{
		configuration = default;
		if (capabilities.Formats.Length == 0)
		{
			reason = "the window surface reports no formats on the device's adapter";
			return false;
		}

		var format = TextureFormat.Undefined;
		foreach (var candidate in PreferredFormats)
		{
			if (Array.IndexOf(capabilities.Formats, candidate) >= 0)
			{
				format = candidate;
				break;
			}
		}

		if (format == TextureFormat.Undefined)
		{
			reason = "the window surface offers no 8- or 10-bit UNORM format (it offers " + string.Join(", ", capabilities.Formats) + ")";
			return false;
		}

		if (capabilities.PresentModes.Length == 0)
		{
			reason = "the window surface reports no present modes";
			return false;
		}

		if ((capabilities.Usages & TextureUsage.RenderAttachment) == 0)
		{
			reason = "the window surface's frames cannot be render attachments";
			return false;
		}

		var presentMode = Array.IndexOf(capabilities.PresentModes, PresentMode.Fifo) >= 0 ? PresentMode.Fifo : capabilities.PresentModes[0];
		var alphaMode = Array.IndexOf(capabilities.AlphaModes, CompositeAlphaMode.Opaque) >= 0 ? CompositeAlphaMode.Opaque : CompositeAlphaMode.Auto;
		configuration = new WebGpuSurfaceConfiguration(format, presentMode, alphaMode, TextureUsage.RenderAttachment);
		reason = null;
		return true;
	}

	/// <summary>Whether a size can be configured: 1 to the device's 2D texture limit on both sides.</summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="maxTextureDimension">The device's 2D texture limit (0: the WebGPU default, 8192).</param>
	/// <returns>True when the size is valid.</returns>
	internal static bool IsConfigurableSize(int width, int height, uint maxTextureDimension)
	{
		var max = maxTextureDimension == 0 ? 8192u : maxTextureDimension;
		return width > 0 && height > 0 && (uint)width <= max && (uint)height <= max;
	}

	/// <summary>What the presenter does with an acquire status.</summary>
	/// <param name="status">The status.</param>
	/// <returns>The action.</returns>
	internal static WebGpuSurfaceAcquireAction ActionFor(SurfaceGetCurrentTextureStatus status) => status switch
	{
		SurfaceGetCurrentTextureStatus.SuccessOptimal or SurfaceGetCurrentTextureStatus.SuccessSuboptimal => WebGpuSurfaceAcquireAction.Present,
		SurfaceGetCurrentTextureStatus.Outdated or SurfaceGetCurrentTextureStatus.Lost => WebGpuSurfaceAcquireAction.Reconfigure,
		SurfaceGetCurrentTextureStatus.Timeout => WebGpuSurfaceAcquireAction.Skip,
		_ => WebGpuSurfaceAcquireAction.Fail,
	};

	/// <summary>
	/// What a device creation does about the DirectSurface path. The direct path is attempted only with the application's
	/// latch on, the view not requiring its own Readback device, no direct failure since the view was loaded, and a platform
	/// view that hosts a window surface; it waits for that surface for at most <see cref="WindowWaitMilliseconds"/>.
	/// </summary>
	/// <param name="directGpuRenderingEnabled">The application-wide latch (<c>DirectGpuRenderingMode.IsEnabled</c>).</param>
	/// <param name="requireOwnedDevice">The view's <c>RequireOwnedDevice</c>.</param>
	/// <param name="directFailed">Whether the direct path failed since the view was loaded.</param>
	/// <param name="windowHostConnected">Whether the view's platform view hosts a window surface.</param>
	/// <param name="windowAvailable">Whether that window surface exists now.</param>
	/// <param name="millisecondsWaited">The time since the view was loaded or its window surface host connected, whichever is later.</param>
	/// <returns>The decision.</returns>
	internal static WebGpuDirectSurfaceDecision Decide(bool directGpuRenderingEnabled, bool requireOwnedDevice, bool directFailed,
		bool windowHostConnected, bool windowAvailable, double millisecondsWaited)
	{
		if (!directGpuRenderingEnabled || requireOwnedDevice || directFailed)
		{
			return WebGpuDirectSurfaceDecision.Readback;
		}

		if (!windowHostConnected)
		{
			return WebGpuDirectSurfaceDecision.ReadbackNoWindowHost;
		}

		if (windowAvailable)
		{
			return WebGpuDirectSurfaceDecision.DirectSurface;
		}

		return millisecondsWaited >= 0.0 && millisecondsWaited < WindowWaitMilliseconds
			? WebGpuDirectSurfaceDecision.Wait
			: WebGpuDirectSurfaceDecision.ReadbackWindowNeverCame;
	}

	/// <summary>
	/// Whether the view's platform view should host a window surface: only while the direct path may be taken (latch on,
	/// no RequireOwnedDevice, no failure since the view was loaded). The platform view removes it otherwise, and the canvas
	/// path shows the frames.
	/// </summary>
	/// <param name="directGpuRenderingEnabled">The application-wide latch.</param>
	/// <param name="requireOwnedDevice">The view's <c>RequireOwnedDevice</c>.</param>
	/// <param name="directFailed">Whether the direct path failed since the view was loaded.</param>
	/// <returns>True to host a window surface.</returns>
	internal static bool WantsWindowSurface(bool directGpuRenderingEnabled, bool requireOwnedDevice, bool directFailed) =>
		directGpuRenderingEnabled && !requireOwnedDevice && !directFailed;

	/// <summary>
	/// Whether a <c>DeviceOptions</c> change replaces the device that exists. On Android both Readback and DirectSurface
	/// run on the view's OWN device, created from the options (the Core's rule knows only Readback as owned).
	/// </summary>
	/// <param name="path">The render path running now.</param>
	/// <returns>True to replace the device at the next frame.</returns>
	internal static bool DeviceOptionsChangeRecreatesDevice(WebGpuRenderPath path) =>
		path is WebGpuRenderPath.Readback or WebGpuRenderPath.DirectSurface;

	/// <summary>The log line written once when direct GPU rendering is on but the view hosts no window surface.</summary>
	internal const string NoWindowHostReason = "the view's platform view hosts no window surface";

	/// <summary>The reason logged when the window surface did not appear in time.</summary>
	internal static string WindowNeverCameReason =>
		$"no window surface appeared within {WindowWaitMilliseconds:0} ms of the view being loaded";
}
