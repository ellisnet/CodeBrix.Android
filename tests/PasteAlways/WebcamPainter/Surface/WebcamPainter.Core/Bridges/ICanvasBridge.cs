using System;
using SkiaSharp;

namespace WebcamPainter.Bridges;

/// <summary>
/// The main and self-view canvas contract between the hosting page and the view model, in
/// both directions. The page hands the view model the invalidate (repaint) delegates for the
/// two Skia canvases; frames and tracking results arrive on capture/worker threads, so the
/// page's delegates are responsible for marshalling their invalidates onto the UI thread.
/// The other direction is <see cref="RenderMainCanvas"/>: the page's paint handler forwards
/// the surface, so what the main canvas shows stays the view model's decision.
/// </summary>
public interface ICanvasBridge
{
    /// <summary>Invalidates the main canvas (live preview in Capture Mode; the painting in Paint Mode).</summary>
    Action InvalidateMainCanvas { get; set; }

    /// <summary>Invalidates the small self-view canvas shown beside the painting in Paint Mode.</summary>
    Action InvalidateSelfView { get; set; }

    /// <summary>
    /// Draws whatever the main canvas should be showing right now - the mirrored live preview
    /// in Capture Mode, the painting and its crosshair in Paint Mode. Called from the page's
    /// <c>PaintSurface</c> handler, so always on the UI thread.
    /// </summary>
    /// <param name="surface">The Skia surface to render onto.</param>
    /// <param name="info">The image info describing the surface.</param>
    void RenderMainCanvas(SKSurface surface, SKImageInfo info);
}
