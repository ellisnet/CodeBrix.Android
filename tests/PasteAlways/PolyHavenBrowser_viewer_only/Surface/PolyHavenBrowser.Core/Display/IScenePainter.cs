using System;
using SkiaSharp;

namespace PolyHavenBrowser.Display;

/// <summary>
/// Draws the current preview (a lit textured cube, a 3D model, or an HDRI panorama) onto
/// the hosting page's Skia canvas, and translates pointer/scroll input into camera motion.
/// The hosting page calls <see cref="Paint"/> from the canvas's <c>PaintSurface</c> handler
/// and the pointer methods from its pointer handlers, all on the UI thread.
/// </summary>
public interface IScenePainter : IDisposable
{
    /// <summary>Paints the current view into the canvas surface at the given pixel size.</summary>
    void Paint(SKSurface surface, SKImageInfo info);

    /// <summary>Begins a drag at the given canvas position.</summary>
    void PointerDown(double x, double y);

    /// <summary>Continues a drag to the given canvas position (no-op when no drag is in progress).</summary>
    void PointerDrag(double x, double y);

    /// <summary>
    /// Advances the drag anchor to the given position without moving the camera, used to
    /// discard a stale (backlogged) pointer frame while staying in sync with the cursor.
    /// </summary>
    void PointerSkip(double x, double y);

    /// <summary>Ends the current drag.</summary>
    void PointerUp();

    /// <summary>Zooms by a mouse-wheel delta (positive zooms in / narrows).</summary>
    void Zoom(double wheelDelta);
}
