#nullable enable

using System;
using CodeBrix.WebGpu.Native;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// A native window handed to the render engine for the DirectSurface path (on Android: the SurfaceView's
/// <c>ANativeWindow</c>, which the platform view acquired and which <see cref="Release"/> gives back). Immutable: a new
/// size is a new instance with the same handle and a higher <see cref="Version"/>.
/// </summary>
internal sealed class WebGpuNativeWindow
{
	/// <summary>Creates the description of a window.</summary>
	/// <param name="handle">The native window pointer (never 0).</param>
	/// <param name="width">Its buffer width in pixels (0 while unknown).</param>
	/// <param name="height">Its buffer height in pixels (0 while unknown).</param>
	/// <param name="version">Grows with every change the platform view reported (created, changed).</param>
	/// <param name="release">Gives the platform's reference on the window back; called once, after the WebGPU surface
	/// over it has been released (null: nothing to give back).</param>
	internal WebGpuNativeWindow(nint handle, int width, int height, int version, Action<nint>? release)
	{
		if (handle == 0)
		{
			throw new ArgumentException("A native window handle is required.", nameof(handle));
		}

		Handle = handle;
		Width = Math.Max(0, width);
		Height = Math.Max(0, height);
		Version = version;
		Release = release;
	}

	/// <summary>The native window pointer.</summary>
	internal nint Handle { get; }

	/// <summary>The buffer width in pixels the platform reported (0 while unknown).</summary>
	internal int Width { get; }

	/// <summary>The buffer height in pixels the platform reported (0 while unknown).</summary>
	internal int Height { get; }

	/// <summary>Grows with every change the platform view reported; a presenter reconfigures when it changes.</summary>
	internal int Version { get; }

	/// <summary>Gives the platform's reference on the window back (null: nothing to give back).</summary>
	internal Action<nint>? Release { get; }

	/// <summary>The same window at a new size (a higher version).</summary>
	/// <param name="width">The new width in pixels.</param>
	/// <param name="height">The new height in pixels.</param>
	/// <returns>The new description.</returns>
	internal WebGpuNativeWindow Resized(int width, int height) => new(Handle, width, height, Version + 1, Release);
}

/// <summary>What a window surface supports (its WebGPU capabilities, copied out once when the surface is created).</summary>
/// <param name="Formats">The colour formats it can be configured with.</param>
/// <param name="PresentModes">The present modes.</param>
/// <param name="AlphaModes">The composite alpha modes.</param>
/// <param name="Usages">The texture usages its frames can have.</param>
internal sealed record WebGpuSurfaceCapabilities(TextureFormat[] Formats, PresentMode[] PresentModes, CompositeAlphaMode[] AlphaModes, TextureUsage Usages);

/// <summary>How a window surface is configured (chosen from its capabilities by <see cref="WebGpuSurfaceRules"/>).</summary>
/// <param name="Format">The colour format of its frames.</param>
/// <param name="PresentMode">The present mode (Fifo: synchronised with the display).</param>
/// <param name="AlphaMode">The composite alpha mode.</param>
/// <param name="Usage">The usage of its frames.</param>
internal readonly record struct WebGpuSurfaceConfiguration(TextureFormat Format, PresentMode PresentMode, CompositeAlphaMode AlphaMode, TextureUsage Usage);

/// <summary>
/// One WebGPU surface over one native window, as the DirectSurface presenter drives it. The production implementation is
/// <see cref="WebGpuNativeWindowSurface"/>; the host tests substitute one whose frames are ordinary textures.
/// </summary>
/// <remarks>
/// The engine ENDS THE PROCESS when a surface is configured with something its capabilities do not allow, or when a
/// frame is acquired from a surface that is not configured: the presenter validates every configuration against
/// <see cref="Capabilities"/> first and never acquires unless <see cref="Configure"/> ran since the last
/// <see cref="Unconfigure"/>. Every member runs on the thread that holds the view's frame gate.
/// </remarks>
internal interface IWebGpuWindowSurface : IDisposable
{
	/// <summary>What the surface supports on the device's adapter.</summary>
	WebGpuSurfaceCapabilities Capabilities { get; }

	/// <summary>Configures (or reconfigures) the surface.</summary>
	/// <param name="device">The device its frames belong to.</param>
	/// <param name="configuration">A configuration its capabilities allow.</param>
	/// <param name="width">The width in pixels (1 to the device's 2D texture limit).</param>
	/// <param name="height">The height in pixels (1 to the device's 2D texture limit).</param>
	void Configure(Device device, WebGpuSurfaceConfiguration configuration, int width, int height);

	/// <summary>Unconfigures the surface (no frame may be acquired until the next <see cref="Configure"/>).</summary>
	void Unconfigure();

	/// <summary>Acquires the next frame's texture (a reference the caller releases).</summary>
	/// <param name="texture">The texture on success; may also be non-null on failure (released by the caller).</param>
	/// <returns>The engine's status.</returns>
	SurfaceGetCurrentTextureStatus Acquire(out Texture texture);

	/// <summary>Presents the acquired frame.</summary>
	/// <returns>The engine's status.</returns>
	Status Present();
}

/// <summary>Creates the WebGPU surface over a native window.</summary>
internal interface IWebGpuWindowSurfaceFactory
{
	/// <summary>Creates the surface on the device's instance and reads its capabilities on the device's adapter.</summary>
	/// <param name="instance">The device's instance.</param>
	/// <param name="adapter">The device's adapter.</param>
	/// <param name="window">The native window.</param>
	/// <param name="reason">Why there is no surface, or null.</param>
	/// <returns>The surface, or null (with <paramref name="reason"/>).</returns>
	IWebGpuWindowSurface? Create(Instance instance, CodeBrix.WebGpu.Native.Adapter adapter, WebGpuNativeWindow window, out string? reason);
}

/// <summary>
/// The production window surface: <c>wgpuInstanceCreateSurface</c> with a <c>SurfaceSourceAndroidNativeWindow</c>
/// chained struct over the window's <c>ANativeWindow*</c>, then the WebGPU surface calls.
/// </summary>
internal sealed unsafe class WebGpuNativeWindowSurface : IWebGpuWindowSurface
{
	/// <summary>The factory the engine uses on the devices.</summary>
	internal static readonly IWebGpuWindowSurfaceFactory Factory = new NativeFactory();

	private Surface _surface;
	private bool _configured;

	private WebGpuNativeWindowSurface(Surface surface, WebGpuSurfaceCapabilities capabilities)
	{
		_surface = surface;
		Capabilities = capabilities;
	}

	/// <inheritdoc/>
	public WebGpuSurfaceCapabilities Capabilities { get; }

	/// <inheritdoc/>
	public void Configure(Device device, WebGpuSurfaceConfiguration configuration, int width, int height)
	{
		ObjectDisposedException.ThrowIf(_surface.IsNull, this);
		var native = new SurfaceConfiguration
		{
			Device = device,
			Format = configuration.Format,
			Usage = configuration.Usage,
			Width = (uint)width,
			Height = (uint)height,
			ViewFormatCount = 0,
			ViewFormats = null,
			AlphaMode = configuration.AlphaMode,
			PresentMode = configuration.PresentMode,
		};
		Wgpu.SurfaceConfigure(_surface, &native);
		_configured = true;
	}

	/// <inheritdoc/>
	public void Unconfigure()
	{
		if (_configured && !_surface.IsNull)
		{
			Wgpu.SurfaceUnconfigure(_surface);
		}

		_configured = false;
	}

	/// <inheritdoc/>
	public SurfaceGetCurrentTextureStatus Acquire(out Texture texture)
	{
		SurfaceTexture frame = default;
		Wgpu.SurfaceGetCurrentTexture(_surface, &frame);
		texture = frame.Texture;
		return frame.Status;
	}

	/// <inheritdoc/>
	public Status Present() => Wgpu.SurfacePresent(_surface);

	/// <summary>Unconfigures and releases the surface (the native window itself is the platform's).</summary>
	public void Dispose()
	{
		if (_surface.IsNull)
		{
			return;
		}

		Unconfigure();
		Wgpu.SurfaceRelease(_surface);
		_surface = Surface.Null;
	}

	private sealed class NativeFactory : IWebGpuWindowSurfaceFactory
	{
		public IWebGpuWindowSurface? Create(Instance instance, CodeBrix.WebGpu.Native.Adapter adapter, WebGpuNativeWindow window, out string? reason)
		{
			reason = null;
			var source = new SurfaceSourceAndroidNativeWindow { Window = (void*)window.Handle };
			source.Chain.SType = SurfaceSourceAndroidNativeWindow.StructType;
			var descriptor = new SurfaceDescriptor { NextInChain = &source.Chain };
			var surface = Wgpu.InstanceCreateSurface(instance, &descriptor);
			if (surface.IsNull)
			{
				reason = "wgpuInstanceCreateSurface returned no surface for the window";
				return null;
			}

			SurfaceCapabilities native = default;
			if (Wgpu.SurfaceGetCapabilities(surface, adapter, &native) != Status.Success)
			{
				Wgpu.SurfaceRelease(surface);
				reason = "the window surface's capabilities could not be read on the device's adapter";
				return null;
			}

			var formats = new TextureFormat[(int)native.FormatCount];
			for (var i = 0; i < formats.Length; i++)
			{
				formats[i] = native.Formats[i];
			}

			var presentModes = new PresentMode[(int)native.PresentModeCount];
			for (var i = 0; i < presentModes.Length; i++)
			{
				presentModes[i] = native.PresentModes[i];
			}

			var alphaModes = new CompositeAlphaMode[(int)native.AlphaModeCount];
			for (var i = 0; i < alphaModes.Length; i++)
			{
				alphaModes[i] = native.AlphaModes[i];
			}

			var usages = native.Usages;
			Wgpu.SurfaceCapabilitiesFreeMembers(native);
			return new WebGpuNativeWindowSurface(surface, new WebGpuSurfaceCapabilities(formats, presentModes, alphaModes, usages));
		}
	}
}
