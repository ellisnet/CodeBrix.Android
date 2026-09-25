using System;

namespace CodeBrix.Android.SkiaSharp.Views.Portable;

/// <summary>
/// The sizes of one SKXamlCanvas paint (pure arithmetic, host-free tested): the pixel buffer is the
/// native view's pixel size (what Core's layout replay gave it); the size the PaintSurface handler
/// is told about is that buffer, or - with IgnorePixelScaling - the element's size in DIPs, as the
/// Skia twin computes it.
/// </summary>
internal static class CanvasSurfaceMath
{
    /// <summary>The user-visible surface size of a paint.</summary>
    /// <param name="pixelWidth">The pixel buffer's width.</param>
    /// <param name="pixelHeight">The pixel buffer's height.</param>
    /// <param name="actualWidth">The element's ActualWidth in DIPs.</param>
    /// <param name="actualHeight">The element's ActualHeight in DIPs.</param>
    /// <param name="ignorePixelScaling">The element's IgnorePixelScaling.</param>
    /// <returns>The width and height the handler is told about.</returns>
    internal static (int Width, int Height) UserVisibleSize(int pixelWidth, int pixelHeight, double actualWidth, double actualHeight, bool ignorePixelScaling) =>
        ignorePixelScaling
            ? ((int)Math.Max(0, actualWidth), (int)Math.Max(0, actualHeight))
            : (Math.Max(0, pixelWidth), Math.Max(0, pixelHeight));

    /// <summary>True when an element of this size can be painted at all (both sides positive and finite).</summary>
    /// <param name="width">A width in DIPs or pixels.</param>
    /// <param name="height">A height in DIPs or pixels.</param>
    /// <returns>True when both are positive and finite.</returns>
    internal static bool IsPaintable(double width, double height) =>
        width > 0 && height > 0 && !double.IsInfinity(width) && !double.IsInfinity(height) && !double.IsNaN(width) && !double.IsNaN(height);

    /// <summary>
    /// The scale from DIPs to canvas units for the canvas-host seam's paint callback: one canvas unit is
    /// one DIP, so the pixel canvas is scaled by the density (never by less than a sane minimum).
    /// </summary>
    /// <param name="density">Physical pixels per DIP of the element's XamlRoot.</param>
    /// <returns>The canvas scale.</returns>
    internal static float CanvasScale(double density) =>
        density > 0 && !double.IsNaN(density) && !double.IsInfinity(density) ? (float)density : 1f;
}
