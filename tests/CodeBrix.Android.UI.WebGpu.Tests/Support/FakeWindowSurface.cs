#nullable enable

using System;
using System.Collections.Generic;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Android.UI.WebGpu.Tests;

/// <summary>
/// Creates <see cref="FakeWindowSurface"/>s - the DirectSurface path's window surface on a machine with no Android window:
/// its "frames" are an ordinary off-screen texture on the view's own device, which a test reads back to see exactly what
/// the path presented.
/// </summary>
internal sealed class FakeWindowSurfaceFactory : IWebGpuWindowSurfaceFactory
{
	private readonly Func<WebGpuSkiaDevice> _device;

	/// <summary>Creates the factory.</summary>
	/// <param name="device">The view's device at the time a surface is created (the engine's current device).</param>
	internal FakeWindowSurfaceFactory(Func<WebGpuSkiaDevice> device)
	{
		_device = device;
	}

	/// <summary>The capabilities the next surfaces report (default: what both test phones report - no BGRA).</summary>
	internal WebGpuSurfaceCapabilities Capabilities { get; set; } = new(
		new[] { TextureFormat.RGBA8UnormSrgb, TextureFormat.RGBA8Unorm, TextureFormat.RGBA16Float, TextureFormat.RGB10A2Unorm },
		new[] { PresentMode.Mailbox, PresentMode.Fifo },
		new[] { CompositeAlphaMode.Opaque, CompositeAlphaMode.Inherit },
		TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc | TextureUsage.CopyDst);

	/// <summary>True: the next Create fails (no surface for the window).</summary>
	internal bool FailCreate { get; set; }

	/// <summary>Every surface created so far.</summary>
	internal List<FakeWindowSurface> Created { get; } = new();

	/// <summary>The newest surface, or null.</summary>
	internal FakeWindowSurface? Latest => Created.Count == 0 ? null : Created[^1];

	public IWebGpuWindowSurface? Create(Instance instance, CodeBrix.WebGpu.Native.Adapter adapter, WebGpuNativeWindow window, out string? reason)
	{
		if (FailCreate)
		{
			reason = "test: no surface for this window";
			return null;
		}

		reason = null;
		var surface = new FakeWindowSurface(_device(), window, Capabilities);
		Created.Add(surface);
		return surface;
	}
}

/// <summary>A window surface whose frames are one off-screen texture of the configured size and format.</summary>
internal sealed class FakeWindowSurface : IWebGpuWindowSurface
{
	private readonly WebGpuSkiaDevice _device;
	private readonly Queue<SurfaceGetCurrentTextureStatus> _statuses = new();
	private WebGpuSkiaTarget? _frame;
	private bool _configured;

	internal FakeWindowSurface(WebGpuSkiaDevice device, WebGpuNativeWindow window, WebGpuSurfaceCapabilities capabilities)
	{
		_device = device;
		Window = window;
		Capabilities = capabilities;
	}

	/// <summary>The window it was created over.</summary>
	internal WebGpuNativeWindow Window { get; }

	public WebGpuSurfaceCapabilities Capabilities { get; }

	/// <summary>Each configuration, in order.</summary>
	internal List<(WebGpuSurfaceConfiguration Configuration, int Width, int Height)> Configurations { get; } = new();

	/// <summary>How many frames were presented.</summary>
	internal int PresentCount { get; private set; }

	/// <summary>How many frames were acquired.</summary>
	internal int AcquireCount { get; private set; }

	/// <summary>Whether the surface was released.</summary>
	internal bool IsDisposed { get; private set; }

	/// <summary>Whether a frame was ever acquired while the surface was not configured (the engine would end the process).</summary>
	internal bool AcquiredUnconfigured { get; private set; }

	/// <summary>Makes the next acquires return these statuses (then success again).</summary>
	/// <param name="statuses">The statuses.</param>
	internal void ScriptAcquire(params SurfaceGetCurrentTextureStatus[] statuses)
	{
		foreach (var status in statuses)
		{
			_statuses.Enqueue(status);
		}
	}

	public void Configure(Device device, WebGpuSurfaceConfiguration configuration, int width, int height)
	{
		if (Array.IndexOf(Capabilities.Formats, configuration.Format) < 0
			|| Array.IndexOf(Capabilities.PresentModes, configuration.PresentMode) < 0
			|| (configuration.AlphaMode != CompositeAlphaMode.Auto && Array.IndexOf(Capabilities.AlphaModes, configuration.AlphaMode) < 0)
			|| (configuration.Usage & ~Capabilities.Usages) != 0)
		{
			throw new InvalidOperationException("Configured with something the capabilities do not allow (the engine would end the process).");
		}

		Configurations.Add((configuration, width, height));
		_frame?.Dispose();
		_frame = WebGpuSkiaTarget.CreateHeadless(_device, width, height, new WebGpuSkiaTargetOptions { Format = configuration.Format });
		_configured = true;
	}

	public void Unconfigure() => _configured = false;

	public SurfaceGetCurrentTextureStatus Acquire(out Texture texture)
	{
		AcquireCount++;
		texture = Texture.Null;
		if (!_configured || _frame is null)
		{
			AcquiredUnconfigured = true;
			return SurfaceGetCurrentTextureStatus.Error;
		}

		if (_statuses.Count > 0)
		{
			return _statuses.Dequeue();
		}

		texture = _frame.Texture;
		Wgpu.TextureAddRef(texture);
		return SurfaceGetCurrentTextureStatus.SuccessOptimal;
	}

	public Status Present()
	{
		PresentCount++;
		return Status.Success;
	}

	/// <summary>Reads the last presented frame (tight RGBA rows of the configured format), or null.</summary>
	/// <returns>The pixels and the frame's width.</returns>
	internal (byte[] Pixels, int Width)? ReadFrame()
	{
		if (_frame is not { } frame)
		{
			return null;
		}

		var pixels = new byte[frame.Width * frame.Height * 4];
		return frame.ReadPixels(pixels) ? (pixels, frame.Width) : null;
	}

	public void Dispose()
	{
		IsDisposed = true;
		_configured = false;
		_frame?.Dispose();
		_frame = null;
	}
}
