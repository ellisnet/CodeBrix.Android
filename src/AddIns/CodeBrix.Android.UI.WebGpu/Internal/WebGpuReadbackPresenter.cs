// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuReadbackPresenter.skia.cs
// (branch main working tree of 2026-10-17 - round 2, UNCOMMITTED at the time of the port, on top of 7a64364e606320e6322bc598f74ec352e99be9db; the commit hash can be added later).
// Verbatim: the Readback render path (reference-counted read-back buffers, as the Platform's B2 fix left it; the asynchronous, double-buffered read-back under the Continuous driver of round 2).

#nullable enable

using System;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using SkiaSharp;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The Readback render path's GPU side: the view's OWN WebGPU device, an off-screen (headless) target the size of the
/// element, and reference-counted BGRA buffers each finished frame is read back into - one GPU-to-processor copy per
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
/// UNDER THE CONTINUOUS DRIVER the read-back is asynchronous and double-buffered (<see cref="Show"/> with
/// allowDeferred): a frame's copy is only BEGUN when it ends (<c>BeginReadPixels</c>, which does not wait), and it is
/// completed - and its picture presented - when the NEXT frame ends, just before that frame's own copy is begun. The
/// GPU copy of frame N therefore overlaps the recording of frame N+1, the wait in <c>EndReadPixels</c> normally finds
/// the map complete, and the Readback path runs one frame behind. A frame that is not followed by another must never
/// sit unpresented, so every other frame (OnDemand, Manual, a native module's, any frame ended through the public
/// <c>EndFrame</c>) is read back and presented synchronously inside its own <c>EndFrame</c>, and the view flushes the
/// pending frame (<see cref="FlushPendingFrame"/>) when continuous ticking stops. A pending read-back is cancelled -
/// never presented - when its frame can no longer be the newest picture: a newer synchronous frame, a new size (a new
/// target: the frame is gone with its texture), teardown and device loss.
/// </para>
/// <para>
/// Every method runs on the thread that holds the view's frame (one at a time, never concurrently). Disposal order is
/// the WebGpu.Skia rule: targets before their device.
/// </para>
/// </remarks>
internal sealed class WebGpuReadbackPresenter : WebGpuPresenter
{
	private static readonly WebGpuSkiaTargetOptions TargetOptions = new() { Format = Format };

	/// <summary>
	/// The longest wait for a deferred frame's read-back to complete (the engine's own bounded wait, the same as its
	/// detach wait). In steady state the map is complete long before; a GPU that does not finish a frame's copy in
	/// this time loses that frame (nothing presented, the target's LastError says why), as a failed read-back always did.
	/// </summary>
	internal static readonly TimeSpan DeferredReadbackWait = TimeSpan.FromSeconds(2);

	// Skia calls this when a picture is destroyed (any thread): the picture's reference on its buffer goes.
	private static readonly SKImageRasterReleaseDelegate ReleasePicture = (_, context) => ((ReadbackBuffer)context!).Release();

	// The buffers this presenter owns a reference to. Small (two or three in steady state); a list, not a pool type,
	// because it is only walked on the frame's thread.
	private readonly System.Collections.Generic.List<ReadbackBuffer> _buffers = new(4);
	private WebGpuSkiaTarget? _target;
	private WebGpuSkiaReadback _pending;   // the deferred frame's read-back on _target, or default
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

		// A frame still being read back on the old target is gone with that target's texture: cancelled, not shown.
		CancelPendingFrame();
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

	/// <summary>
	/// Synchronous (allowDeferred false): reads the finished frame back and presents its picture before returning
	/// (nothing when the read failed); a deferred frame still pending is older than this one and is cancelled.
	/// Deferred: completes and presents the PREVIOUS frame's read-back, then begins this frame's and leaves it pending.
	/// </summary>
	/// <param name="host">The engine's owner.</param>
	/// <param name="allowDeferred">True under the Continuous driver (another frame is sure to follow).</param>
	internal override void Show(IWebGpuRenderEngineHost host, bool allowDeferred)
	{
		if (!allowDeferred)
		{
			CancelPendingFrame();
			if (ReadFrame() is { } picture)
			{
				host.Present(picture);
			}

			return;
		}

		CompletePendingFrame(host);
		if (Target is { IsAvailable: true } target)
		{
			// Default (invalid) when the copy could not be issued (target.LastError): this frame is then lost, as a
			// failed synchronous read-back loses it.
			_pending = target.BeginReadPixels();
		}
	}

	/// <summary>Completes and presents the deferred frame, if one is pending.</summary>
	/// <param name="host">The engine's owner.</param>
	internal override void FlushPendingFrame(IWebGpuRenderEngineHost host) => CompletePendingFrame(host);

	/// <summary>True while a deferred frame's read-back is pending (tests).</summary>
	internal bool HasPendingFrame => _pending.IsValid;

	// Ends the pending read-back into a free buffer (a bounded wait, normally none) and presents the picture. A failure
	// (timeout, map failed, device lost) presents nothing; the read-back is cancelled so its slot is free again.
	private unsafe void CompletePendingFrame(IWebGpuRenderEngineHost host)
	{
		if (!_pending.IsValid)
		{
			return;
		}

		var pending = _pending;
		_pending = default;
		if (_target is not { } target || !ReferenceEquals(pending.Target, target))
		{
			return;
		}

		if (!target.IsAvailable)
		{
			target.CancelReadPixels(pending);
			return;
		}

		var width = target.Width;
		var height = target.Height;
		var buffer = TakeFreeBuffer(width, height);
		var pixels = new Span<byte>((void*)buffer.Pixels, buffer.RowBytes * height);
		if (!target.EndReadPixels(pending, pixels, buffer.RowBytes, DeferredReadbackWait))
		{
			// A timeout leaves it pending; every other failure has already freed the slot (cancelling is then a no-op).
			target.CancelReadPixels(pending);
			return;
		}

		if (WrapPicture(buffer, width, height) is { } picture)
		{
			host.Present(picture);
		}
	}

	// Frame thread. Drops the pending read-back (its slot is freed; nothing is presented).
	private void CancelPendingFrame()
	{
		if (!_pending.IsValid)
		{
			return;
		}

		var pending = _pending;
		_pending = default;
		if (_target is { } target && ReferenceEquals(pending.Target, target))
		{
			target.CancelReadPixels(pending);
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

		return WrapPicture(buffer, width, height);
	}

	// A picture over the buffer's pixels (no copy). The picture takes its own reference on the buffer; Skia's release
	// callback gives it back.
	private static SKImage? WrapPicture(ReadbackBuffer buffer, int width, int height)
	{
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
		CancelPendingFrame();
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
