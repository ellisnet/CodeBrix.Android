// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoFrameReadback.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from; the WP8a fix, uncommitted there).
// One change, marked ANDROID PORT (1): the pictures are RGBA (OpenGL ES guarantees an RGBA read-back only; see VideoRenderDriver).

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;

/// <summary>
/// The one readback per frame: copies a composed picture into processor memory and wraps those pixels, with no further
/// copy, as the immutable SKImage the surface element paints.
/// </summary>
/// <remarks>
/// <para>
/// The pixels live in reference-counted buffers (<see cref="VideoReadbackBuffer"/>), and each picture handed out holds a
/// reference to its buffer until Skia lets go of the picture (its raster release callback). A frame reads back only into
/// a buffer NO picture references any more, so a picture is never overwritten while anything can still draw it - and
/// the head may draw it well after the surface element has moved on: the desktop heads RECORD the window frame on the
/// user-interface thread and replay that recording later on the render thread, so a recording can outlive the next
/// frames, a resize and the player's teardown. Two alternating bitmaps (what this replaced) rewrote the pixels of a
/// recorded picture on the third frame and freed them underneath it on a resize or a teardown.
/// </para>
/// <para>
/// In steady state two buffers circulate (three while a recording holds one) and nothing is allocated; a buffer of an
/// old size is freed by its last picture. Every method runs on the user-interface thread; the release callback runs on
/// whatever thread drops the picture.
/// </para>
/// </remarks>
internal sealed class VideoFrameReadback : IDisposable
{
	// Skia calls this when a picture is destroyed (any thread): the picture's reference on its buffer goes.
	private static readonly SKImageRasterReleaseDelegate ReleasePicture =
		(_, context) => ((VideoReadbackBuffer)context!).Release();

	// The buffers this object holds a reference to: two or three in steady state, only walked on the UI thread.
	private readonly List<VideoReadbackBuffer> _buffers = new(4);
	private bool _disposed;

	/// <summary>How many read-back buffers are held (tests: no growth in steady state).</summary>
	internal int BufferCount => _buffers.Count;

	/// <summary>
	/// Reads <paramref name="image"/> back into a buffer no picture is using and returns a picture over it, or null when
	/// the image is empty or the read failed.
	/// </summary>
	/// <param name="image">The composed frame (graphics-backed images need their context current).</param>
	/// <returns>The picture; ownership passes to the caller (the surface element disposes it when it is replaced).</returns>
	internal SKImage? Read(SKImage image)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var width = image.Width;
		var height = image.Height;
		if (width <= 0 || height <= 0)
		{
			return null;
		}

		// ANDROID PORT (1): RGBA, as the driver always read back on Android (a BGRA read of a GPU-composed frame fails on
		// OpenGL ES with GL_INVALID_ENUM); the surface draws either.
		var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
		var buffer = TakeFreeBuffer(width, height);
		if (!image.ReadPixels(info, buffer.Pixels, buffer.RowBytes, 0, 0))
		{
			return null;
		}

		// The picture takes its own reference on the buffer; Skia's release callback gives it back.
		buffer.AddRef();
		using var pixmap = new SKPixmap(info, buffer.Pixels, buffer.RowBytes);
		var picture = SKImage.FromPixels(pixmap, ReleasePicture, buffer);
		if (picture is null)
		{
			buffer.Release();
		}

		return picture;
	}

	// A buffer of this size that no picture references any more (only this object's own reference is left), or a new
	// one. Buffers of another size are let go of on the way: freed now when nothing references them, otherwise by their
	// last picture.
	private VideoReadbackBuffer TakeFreeBuffer(int width, int height)
	{
		VideoReadbackBuffer? free = null;
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
			free = new VideoReadbackBuffer(width, height);
			_buffers.Add(free);
		}

		return free;
	}

	/// <summary>
	/// Lets go of this object's references on the buffers: one nothing draws any more is freed now, one a picture still
	/// holds is freed by that picture.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		foreach (var buffer in _buffers)
		{
			buffer.Release();
		}

		_buffers.Clear();
	}
}

/// <summary>
/// One read-back pixel buffer (4 bytes a pixel - RGBA on Android, ANDROID PORT (1) - tight rows) in native memory, freed when the last reference goes: the readback
/// holds one while the buffer is in its list, and every picture made over it holds one until Skia releases the picture.
/// Thread-safe counting: pictures are released on whatever thread drops them.
/// </summary>
internal sealed class VideoReadbackBuffer
{
	private int _references = 1;

	/// <summary>Allocates the pixels of a <paramref name="width"/> x <paramref name="height"/> 4-byte-a-pixel buffer.</summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	internal VideoReadbackBuffer(int width, int height)
	{
		Width = width;
		Height = height;
		RowBytes = width * 4;
		Pixels = Marshal.AllocHGlobal((nint)RowBytes * height);
	}

	internal int Width { get; }

	internal int Height { get; }

	internal int RowBytes { get; }

	internal nint Pixels { get; private set; }

	/// <summary>True when only the owner's own reference is left: no picture can draw these pixels.</summary>
	internal bool IsUnreferenced => Volatile.Read(ref _references) == 1;

	internal void AddRef() => Interlocked.Increment(ref _references);

	internal void Release()
	{
		if (Interlocked.Decrement(ref _references) == 0)
		{
			Marshal.FreeHGlobal(Pixels);
			Pixels = 0;
		}
	}
}
