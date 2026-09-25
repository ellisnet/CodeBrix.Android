// ImageSurface.cs
//
// Pinta.Brix drawing-layer bitmap surface: a premultiplied-BGRA32 pixel
// buffer with span access, mirroring the surface API the upstream Pinta code
// used. Backed by an SKBitmap so drawing Contexts and SkiaSharp interop share
// the same pixel memory with no copies.

using System;
using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

public sealed class ImageSurface : Surface
{
	private readonly SKBitmap bitmap;
	private bool disposed;

	public ImageSurface (Format format, int width, int height)
	{
		if (width < 0 || height < 0)
			throw new ArgumentOutOfRangeException (nameof (width), "Surface dimensions must be non-negative");

		Format = format;

		// The engine always draws in premultiplied BGRA32; an A8 surface is
		// stored in the same layout for simplicity (alpha in every channel).
		bitmap = new SKBitmap (new SKImageInfo (
			Math.Max (width, 1),
			Math.Max (height, 1),
			SKColorType.Bgra8888,
			SKAlphaType.Premul));
		bitmap.Erase (SKColors.Transparent);

		Width = width;
		Height = height;
	}

	public Format Format { get; }

	public override int Width { get; }

	public override int Height { get; }

	public int Stride => bitmap.RowBytes;

	public override Status Status => disposed ? Status.SurfaceFinished : Status.Success;

	/// <summary>
	/// The surface's pixel memory as raw bytes (premultiplied BGRA, row-major,
	/// no padding between rows).
	/// </summary>
	public unsafe Span<byte> GetData ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return new Span<byte> ((void*) bitmap.GetPixels (), Width * Height * 4);
	}

	/// <summary>
	/// Signals that pixel memory was modified outside a drawing Context, so
	/// cached snapshots of the surface are invalidated.
	/// </summary>
	public override void MarkDirty ()
		=> bitmap.NotifyPixelsChanged ();

	public void MarkDirty (int x, int y, int width, int height)
		=> bitmap.NotifyPixelsChanged ();

	/// <summary>
	/// Flushes pending drawing. Raster drawing completes synchronously, so
	/// this only needs to invalidate cached snapshots.
	/// </summary>
	public override void Flush ()
		=> bitmap.NotifyPixelsChanged ();

	/// <summary>The SkiaSharp bitmap sharing this surface's pixel memory.</summary>
	public override SKBitmap Bitmap {
		get {
			ObjectDisposedException.ThrowIf (disposed, this);
			return bitmap;
		}
	}

	public override void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		bitmap.Dispose ();
	}
}
