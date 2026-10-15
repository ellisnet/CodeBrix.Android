#nullable enable

using System;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The Android DirectSurface render path's GPU side: the view's OWN WebGPU device (as on Readback), the frame texture
/// every render path renders into (an off-screen BGRA8Unorm target the size of the element - so the frame protocol, the
/// format and the usage are exactly Readback's), and a WebGPU surface over the native window of a SurfaceView the
/// platform view places under the element. Each finished frame is drawn onto the surface's next frame by one GPU pass
/// (<see cref="WebGpuSurfaceBlit"/>: the surfaces offer RGBA, not BGRA) and presented: nothing is read back to processor
/// memory, and the operating system composites the surface with the window.
/// </summary>
/// <remarks>
/// <para>
/// <b>The window.</b> The platform view hands the engine its SurfaceView's window when Android creates it, a new size
/// when Android changes it, and takes it back when Android destroys it (the activity going to the background, the view
/// leaving the window). The presenter reads the engine's current window at every frame: no window means no frame (the
/// view waits; it does not fall back), a new window means a new surface, a new size or a reported change means a
/// reconfiguration. The surface is created on the frame's thread, so the engine releases it under the frame gate before
/// the platform view gives the window back.
/// </para>
/// <para>
/// <b>Failures.</b> The engine ends the process when a surface is configured with something its capabilities do not
/// allow, when a frame is acquired from an unconfigured surface, or when a command buffer that failed validation is
/// submitted - so every configuration is chosen from the capabilities (<see cref="WebGpuSurfaceRules"/>), nothing is
/// acquired unless configured, and the pass is finished inside a validation error scope. Anything that still goes wrong
/// sets <see cref="FailureReason"/>: one log line, DeviceLost, and the view continues on Readback.
/// </para>
/// <para>
/// <b>A rejected frame</b> (its commands failed validation) is never presented: the surface keeps showing the previous
/// frame, as Readback keeps the previous picture. Every member runs on the thread that holds the view's frame gate.
/// </para>
/// </remarks>
internal sealed class WebGpuSurfacePresenter : WebGpuPresenter
{
	private static readonly WebGpuSkiaTargetOptions TargetOptions = new() { Format = Format };

	private readonly Func<WebGpuNativeWindow?> _window;
	private readonly IWebGpuWindowSurfaceFactory _factory;
	private WebGpuSkiaTarget? _target;
	private IWebGpuWindowSurface? _surface;
	private nint _surfaceWindow;
	private WebGpuSurfaceConfiguration _configuration;
	private bool _configured;
	private int _configuredWidth;
	private int _configuredHeight;
	private int _configuredVersion;
	private WebGpuSurfaceBlit? _blit;
	private string? _failure;
	private bool _waiting;
	private bool _configurationLogged;
	private bool _disposed;

	/// <summary>Creates the presenter over an AVAILABLE device, which it then owns.</summary>
	/// <param name="device">The device (checked available by the caller).</param>
	/// <param name="window">Reads the engine's current window (null while there is none).</param>
	/// <param name="factory">Creates the WebGPU surface over a window.</param>
	internal WebGpuSurfacePresenter(WebGpuSkiaDevice device, Func<WebGpuNativeWindow?> window, IWebGpuWindowSurfaceFactory factory)
	{
		Device = device ?? throw new ArgumentNullException(nameof(device));
		_window = window ?? throw new ArgumentNullException(nameof(window));
		_factory = factory ?? throw new ArgumentNullException(nameof(factory));
	}

	/// <inheritdoc/>
	internal override WebGpuRenderPath Path => WebGpuRenderPath.DirectSurface;

	/// <summary>The view's own device.</summary>
	internal override WebGpuSkiaDevice Device { get; }

	/// <summary>The frame texture (BGRA8Unorm, the element's size), or null before the first sized frame.</summary>
	internal override WebGpuSkiaTarget? Target => _target;

	/// <inheritdoc/>
	internal override string? FailureReason => _failure;

	/// <inheritdoc/>
	internal override bool IsWaiting => _waiting;

	/// <summary>Whether the surface is configured now (tests).</summary>
	internal bool IsConfigured => _configured;

	/// <summary>The configuration the surface got (tests, logging).</summary>
	internal WebGpuSurfaceConfiguration Configuration => _configuration;

	/// <summary>How many frames reached the window surface (tests).</summary>
	internal long PresentedCount { get; private set; }

	/// <summary>How many times the surface was configured (tests: reconfiguration after a change).</summary>
	internal int ConfigureCount { get; private set; }

	/// <summary>
	/// Makes sure the frame texture is <paramref name="width"/> x <paramref name="height"/> and the window surface exists
	/// and is configured for it. No window: false with <see cref="IsWaiting"/> set (the frame is skipped quietly).
	/// </summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="reason">Why there is no usable target, or null.</param>
	/// <returns>True when a frame can be rendered and presented.</returns>
	internal override bool EnsureTarget(int width, int height, out string? reason)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_waiting = false;
		reason = _failure;
		if (_failure is not null)
		{
			return false;
		}

		if (_window() is not { } window)
		{
			ReleaseWindowSurface();
			_waiting = true;
			reason = "The window surface does not exist now.";
			return false;
		}

		if (!EnsureFrameTexture(width, height, out reason))
		{
			return false;
		}

		if (!EnsureSurface(window, width, height))
		{
			reason = _failure;
			return false;
		}

		return true;
	}

	/// <inheritdoc/>
	internal override void EndPass(CommandEncoder encoder) => _target!.EndWebGpuPass();

	/// <summary>
	/// Draws the finished frame onto the window surface's next frame and presents it. Nothing when the window went away
	/// during the frame, or the surface asked to be reconfigured (the next frame does it).
	/// </summary>
	/// <param name="host">The engine's owner.</param>
	internal override unsafe void Show(IWebGpuRenderEngineHost host)
	{
		if (_surface is not { } surface || !_configured || _target is not { } target || _blit is not { } blit || _window() is null)
		{
			return;
		}

		var status = surface.Acquire(out var texture);
		var action = WebGpuSurfaceRules.ActionFor(status);
		if (action != WebGpuSurfaceAcquireAction.Present)
		{
			if (!texture.IsNull)
			{
				Wgpu.TextureRelease(texture);
			}

			if (action == WebGpuSurfaceAcquireAction.Reconfigure)
			{
				_configured = false;
			}
			else if (action == WebGpuSurfaceAcquireAction.Fail)
			{
				Fail("a frame of the window surface could not be acquired (" + status + ")");
			}

			return;
		}

		var view = Wgpu.TextureCreateView(texture, null);
		var encoder = Wgpu.DeviceCreateCommandEncoder(Device.Device, null);
		string? failure = null;
		CommandBuffer commands = default;
		if (view.IsNull || encoder.IsNull)
		{
			failure = "the window surface's frame could not be drawn on";
		}
		else if (!blit.Record(encoder, target.TextureView, view, out failure))
		{
			// failure says why.
		}
		else
		{
			WebGpuErrorScope.Push(Device.Device);
			commands = Wgpu.CommandEncoderFinish(encoder, null);
			var error = WebGpuErrorScope.Pop(Device.Instance, Device.Device);
			if (error is not null || commands.IsNull)
			{
				failure = "the window surface pass failed validation: " + (error ?? "no command buffer");
			}
		}

		if (!encoder.IsNull)
		{
			Wgpu.CommandEncoderRelease(encoder);
		}

		if (failure is null)
		{
			Wgpu.QueueSubmit(Device.Queue, 1, &commands);
		}

		if (!commands.IsNull)
		{
			Wgpu.CommandBufferRelease(commands);
		}

		// The acquired frame is presented whenever it was drawn on; on a failure the surface is released with the
		// presenter (the view falls back), which also discards the frame.
		var presented = failure is null && surface.Present() == Status.Success;
		if (!view.IsNull)
		{
			Wgpu.TextureViewRelease(view);
		}

		Wgpu.TextureRelease(texture);
		if (failure is not null)
		{
			Fail(failure);
			return;
		}

		if (!presented)
		{
			// The window changed under the frame (rotation, a lost buffer queue): reconfigure at the next frame.
			_configured = false;
			return;
		}

		PresentedCount++;
		host.PresentSurfaceFrame();
	}

	/// <summary>The owned device's uncaptured errors (its handler collects them), joined; null when there were none.</summary>
	/// <returns>The message, or null.</returns>
	internal override string? TakeErrors()
	{
		var errors = Device.TakeUncapturedErrors();
		return errors.Length > 0 ? string.Join(" | ", errors) : null;
	}

	/// <summary>
	/// Unconfigures and releases the WebGPU surface over the window (the window itself is the platform's). The engine
	/// calls this under the frame gate when Android destroys the window, BEFORE the platform view gives it back.
	/// </summary>
	internal void ReleaseWindowSurface()
	{
		_configured = false;
		_surfaceWindow = 0;
		if (_surface is { } surface)
		{
			_surface = null;
			surface.Dispose();
		}
	}

	/// <summary>Releases the window surface, the pass, the frame texture, then the device.</summary>
	public override void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		ReleaseWindowSurface();
		_blit?.Dispose();
		_blit = null;
		_target?.Dispose();
		_target = null;
		Device.Dispose();
	}

	private bool EnsureFrameTexture(int width, int height, out string? reason)
	{
		reason = null;
		if (_target is { } current && current.Width == width && current.Height == height && current.IsAvailable)
		{
			return true;
		}

		_blit?.ForgetSource();
		_target?.Dispose();
		_target = null;

		var target = WebGpuSkiaTarget.CreateHeadless(Device, width, height, TargetOptions);
		if (!target.IsAvailable)
		{
			reason = target.TierReason;
			target.Dispose();
			return false;
		}

		_target = target;
		return true;
	}

	private bool EnsureSurface(WebGpuNativeWindow window, int width, int height)
	{
		if (_surface is not null && _surfaceWindow != window.Handle)
		{
			ReleaseWindowSurface();
		}

		if (_surface is null)
		{
			var surface = _factory.Create(Device.Instance, Device.Adapter, window, out var reason);
			if (surface is null)
			{
				Fail(reason ?? "the window surface could not be created");
				return false;
			}

			if (!WebGpuSurfaceRules.TryChooseConfiguration(surface.Capabilities, out var configuration, out reason))
			{
				surface.Dispose();
				Fail(reason!);
				return false;
			}

			if (_blit is null || _blit.DestinationFormat != configuration.Format)
			{
				_blit?.Dispose();
				_blit = WebGpuSurfaceBlit.Create(Device.Instance, Device.Device, configuration.Format, out reason);
				if (_blit is null)
				{
					surface.Dispose();
					Fail(reason!);
					return false;
				}
			}

			_surface = surface;
			_surfaceWindow = window.Handle;
			_configuration = configuration;
			_configured = false;
		}

		if (_configured && _configuredWidth == width && _configuredHeight == height && _configuredVersion == window.Version)
		{
			return true;
		}

		if (!WebGpuSurfaceRules.IsConfigurableSize(width, height, 0))
		{
			Fail($"the window surface cannot be configured at {width}x{height}");
			return false;
		}

		_surface.Configure(Device.Device, _configuration, width, height);
		_configured = true;
		_configuredWidth = width;
		_configuredHeight = height;
		_configuredVersion = window.Version;
		ConfigureCount++;
		var level = _configurationLogged ? LogLevel.Debug : LogLevel.Information;
		_configurationLogged = true;
		if (this.Log().IsEnabled(level))
		{
			var line = $"DirectSurface: presenting to the window surface at {width}x{height} (window {window.Width}x{window.Height}), "
				+ $"{_configuration.Format}, {_configuration.PresentMode}, {_configuration.AlphaMode} - frames render in {Format} and are drawn onto it on the GPU.";
			if (level == LogLevel.Information)
			{
				this.Log().Info(line);
			}
			else
			{
				this.Log().Debug(line);
			}
		}

		return true;
	}

	private void Fail(string reason)
	{
		_failure ??= reason;
		ReleaseWindowSurface();
	}
}
