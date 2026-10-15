// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuReadbackPresenter.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the Readback render path (reference-counted read-back buffers, as the Platform's B2 fix left it).

#nullable enable

using System;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using SkiaSharp;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The Readback render path's GPU side: the view's OWN WebGPU device, an off-screen (headless) target the size of the
/// element, and two alternating BGRA bitmaps each finished frame is read back into - one GPU-to-processor copy per
/// frame - and wrapped, with no further copy, as the SKImage the surface paints.
/// </summary>
/// <remarks>
/// <para>
/// The pixels live in reference-counted buffers (<see cref="ReadbackBuffer"/>), and each picture handed out holds a
/// reference to its buffer until Skia lets go of the picture (its raster release callback). A frame reads back only
/// into a buffer NO picture references any more, so a picture is never overwritten while anything can still draw it -
/// and the head may draw it well after the surface has moved on: the desktop heads RECORD the window frame on the
/// user-interface thread and replay that recording later on the render thread, so a recording can outlive the next
/// frames, a resize and the view's teardown. Freeing or rewriting those pixels underneath it crashed the replay
/// (a use-after-free) or tore the picture. In steady state two or three buffers circulate and nothing is allocated;
/// a buffer of an old size is freed once its last picture is gone.
/// </para>
/// <para>
/// Every method runs on the thread that holds the view's frame (one at a time, never concurrently). Disposal order is
/// the WebGpu.Skia rule: targets before their device.
/// </para>
/// </remarks>
internal sealed class WebGpuReadbackPresenter : WebGpuPresenter
{
	private static readonly WebGpuSkiaTargetOptions TargetOptions = new() { Format = Format };

	// Skia calls this when a picture is destroyed (any thread): the picture's reference on its buffer goes.
	private static readonly SKImageRasterReleaseDelegate ReleasePicture = (_, context) => ((ReadbackBuffer)context!).Release();

	// The buffers this presenter owns a reference to. Small (two or three in steady state); a list, not a pool type,
	// because it is only walked on the frame's thread.
	private readonly System.Collections.Generic.List<ReadbackBuffer> _buffers = new(4);
	private WebGpuSkiaTarget? _target;
	private bool _disposed;

	/// <summary>Creates the presenter over an AVAILABLE device, which it then owns.</summary>
	/// <param name="device">The device (checked available by the caller).</param>
	internal WebGpuReadbackPresenter(WebGpuSkiaDevice device)
	{
		Device = device ?? throw new ArgumentNullException(nameof(device));
	}

	/// <inheritdoc/>
	internal override WebGpuRenderPath Path => WebGpuRenderPath.Readback;

	/// <summary>The view's own device.</summary>
	internal override WebGpuSkiaDevice Device { get; }

	/// <summary>The current target, or null before the first sized frame (and after a size the device refused).</summary>
	internal override WebGpuSkiaTarget? Target => _target;

	/// <summary>
	/// Makes sure the target is <paramref name="width"/> x <paramref name="height"/>: a new size is a new target (and new
	/// texture handles); the old one is released first.
	/// </summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="reason">Why there is no usable target, or null.</param>
	/// <returns>True when a usable target of that size exists.</returns>
	internal override bool EnsureTarget(int width, int height, out string? reason)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		reason = null;
		if (Target is { } current && current.Width == width && current.Height == height && current.IsAvailable)
		{
			return true;
		}

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

	/// <inheritdoc/>
	internal override void EndPass(CommandEncoder encoder) => _target!.EndWebGpuPass();

	/// <summary>Reads the finished frame back once and presents the picture (nothing when the read failed).</summary>
	/// <param name="host">The engine's owner.</param>
	internal override void Show(IWebGpuRenderEngineHost host)
	{
		if (ReadFrame() is { } picture)
		{
			host.Present(picture);
		}
	}

	/// <summary>The owned device's uncaptured errors (its handler collects them), joined; null when there were none.</summary>
	/// <returns>The message, or null.</returns>
	internal override string? TakeErrors()
	{
		var errors = Device.TakeUncapturedErrors();
		return errors.Length > 0 ? string.Join(" | ", errors) : null;
	}

	/// <summary>
	/// Reads the target's finished frame into the write-side bitmap and returns a wrapper of it (no copy), or null when
	/// the read failed (the target's LastError says why).
	/// </summary>
	/// <returns>The picture; ownership passes to the caller (the surface disposes it when it is replaced).</returns>
	internal unsafe SKImage? ReadFrame()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (Target is not { } target)
		{
			return null;
		}

		var width = target.Width;
		var height = target.Height;
		var buffer = TakeFreeBuffer(width, height);
		var pixels = new Span<byte>((void*)buffer.Pixels, buffer.RowBytes * height);
		if (!target.ReadPixels(pixels))
		{
			return null;
		}

		// The picture takes its own reference on the buffer; Skia's release callback gives it back.
		var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
		buffer.AddRef();
		using var pixmap = new SKPixmap(info, buffer.Pixels, buffer.RowBytes);
		var image = SKImage.FromPixels(pixmap, ReleasePicture, buffer);
		if (image is null)
		{
			buffer.Release();
		}

		return image;
	}

	/// <summary>How many read-back buffers this presenter holds (tests: no growth in steady state).</summary>
	internal int BufferCount => _buffers.Count;

	// A buffer of this size that no picture references any more (only this presenter's own reference is left), or a new
	// one. Buffers of another size that nothing references are freed on the way; those a picture still holds are let
	// go of here and freed by their last picture.
	private ReadbackBuffer TakeFreeBuffer(int width, int height)
	{
		ReadbackBuffer? free = null;
		for (var i = _buffers.Count - 1; i >= 0; i--)
		{
			var candidate = _buffers[i];
			if (candidate.Width != width || candidate.Height != height)
			{
				_buffers.RemoveAt(i);
				candidate.Release();
				continue;
			}

			if (free is null && candidate.IsUnreferenced)
			{
				free = candidate;
			}
		}

		if (free is null)
		{
			free = new ReadbackBuffer(width, height);
			_buffers.Add(free);
		}

		return free;
	}

	/// <summary>Releases the target, then the device, then this presenter's references on the read-back buffers (a
	/// buffer a picture still holds is freed by that picture).</summary>
	public override void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_target?.Dispose();
		_target = null;
		Device.Dispose();
		foreach (var buffer in _buffers)
		{
			buffer.Release();
		}

		_buffers.Clear();
	}
}

/// <summary>
/// One read-back pixel buffer (BGRA, tight rows) in native memory, freed when the last reference goes: the presenter
/// holds one while the buffer is in its list, and every picture made over it holds one until Skia releases the
/// picture. Thread-safe counting: pictures are released on whatever thread drops them.
/// </summary>
internal sealed class ReadbackBuffer
{
	private int _references = 1;

	internal unsafe ReadbackBuffer(int width, int height)
	{
		Width = width;
		Height = height;
		RowBytes = width * 4;
		Pixels = (nint)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)RowBytes * (nuint)height);
	}

	internal int Width { get; }

	internal int Height { get; }

	internal int RowBytes { get; }

	internal nint Pixels { get; private set; }

	/// <summary>True when only the presenter's own reference is left: no picture can draw these pixels.</summary>
	internal bool IsUnreferenced => System.Threading.Volatile.Read(ref _references) == 1;

	internal void AddRef() => System.Threading.Interlocked.Increment(ref _references);

	internal unsafe void Release()
	{
		if (System.Threading.Interlocked.Decrement(ref _references) == 0)
		{
			System.Runtime.InteropServices.NativeMemory.Free((void*)Pixels);
			Pixels = 0;
		}
	}
}
