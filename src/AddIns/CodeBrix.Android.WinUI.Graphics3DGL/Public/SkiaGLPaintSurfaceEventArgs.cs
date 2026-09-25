using System;
using SkiaSharp;

namespace CodeBrix.Platform.WinUI.Graphics3DGL;

/// <summary>
/// The arguments of <see cref="SkiaGLCanvasElement.PaintSurface"/>: the GPU surface to draw on, the GRContext it
/// belongs to and the surface's image info. (The same public type as the CodeBrix.Platform flavor of the add-in.)
/// </summary>
public class SkiaGLPaintSurfaceEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="surface">The GPU surface.</param>
    /// <param name="context">The GRContext the surface belongs to.</param>
    /// <param name="info">The surface's image info.</param>
    public SkiaGLPaintSurfaceEventArgs(SKSurface surface, GRContext context, SKImageInfo info)
    {
        Surface = surface;
        Context = context;
        Info = info;
    }

    /// <summary>The GPU surface to draw on (its canvas origin is the top left corner of the element).</summary>
    public SKSurface Surface { get; }

    /// <summary>The GRContext the surface belongs to.</summary>
    public GRContext Context { get; }

    /// <summary>The surface's image info (its size is the element's arranged size).</summary>
    public SKImageInfo Info { get; }
}
