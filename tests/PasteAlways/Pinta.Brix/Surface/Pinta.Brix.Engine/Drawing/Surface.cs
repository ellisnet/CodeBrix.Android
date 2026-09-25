// Surface.cs
//
// Base type for drawing-layer surfaces, mirroring the surface hierarchy the
// upstream Pinta code used (helpers accept any surface; ImageSurface is the
// pixel-backed implementation and the only one the engine creates).

using System;
using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

public abstract class Surface : IDisposable
{
	public abstract int Width { get; }

	public abstract int Height { get; }

	public abstract Status Status { get; }

	/// <summary>The SkiaSharp bitmap sharing this surface's pixel memory.</summary>
	public abstract SKBitmap Bitmap { get; }

	public abstract void Flush ();

	public abstract void MarkDirty ();

	public abstract void Dispose ();
}
