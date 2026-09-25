// STUB (paste always): SkiaSharp.Views.Windows.SKXamlCanvas and SKPaintSurfaceEventArgs (package
// CodeBrix.Platform.SkiaSharp.Views.MitLicenseForever, a CodeBrix.Platform repo add-in); it has no Android
// flavor yet. Same namespace and type names; the members Pinta.Brix uses (PaintSurface, Invalidate).
using System;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

namespace SkiaSharp.Views.Windows;

/// <summary>Stand-in for the Skia drawing canvas element (compile-only).</summary>
public partial class SKXamlCanvas : Canvas
{
    /// <summary>Raised when the canvas surface has to be painted.</summary>
    public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;

    /// <summary>Whether the canvas ignores the display scale.</summary>
    public bool IgnorePixelScaling { get; set; }

    /// <summary>The current canvas size in pixels.</summary>
    public SKSize CanvasSize { get; private set; }

    /// <summary>Requests a repaint.</summary>
    public void Invalidate() { }

    /// <summary>Raises <see cref="PaintSurface"/>.</summary>
    protected virtual void OnPaintSurface(SKPaintSurfaceEventArgs e) => PaintSurface?.Invoke(this, e);
}

/// <summary>Stand-in for the paint-surface event arguments (compile-only).</summary>
public class SKPaintSurfaceEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SKPaintSurfaceEventArgs(SKSurface surface, SKImageInfo info)
    {
        Surface = surface;
        Info = info;
    }

    /// <summary>The surface to draw on.</summary>
    public SKSurface Surface { get; }

    /// <summary>The surface's image info.</summary>
    public SKImageInfo Info { get; }
}
