using SkiaSharp;
using System;

namespace WebcamPainter.Painting;

/// <summary>
/// The SkiaSharp canvas control that displays the captured still being painted - a plain
/// SKXamlCanvas subclass so the XAML can say <c>&lt;painting:PaintCanvas /&gt;</c>; the
/// hosting page's code-behind wires PaintSurface to <see cref="PaintCanvasHelper.Render"/>.
/// </summary>
public class PaintCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }

/// <summary>
/// Renders a <see cref="PaintingSession"/> (the still plus its highlighter strokes) and the
/// hand-position crosshair cursor onto a Skia surface. Called from the canvas PaintSurface
/// handler, always on the UI thread.
/// </summary>
public static class PaintCanvasHelper
{
    /// <summary>
    /// Renders the painting and, when a hand position is known, an unobtrusive crosshair
    /// ring sized to the brush.
    /// </summary>
    /// <param name="surface">The Skia surface to render onto.</param>
    /// <param name="info">The image info describing the surface.</param>
    /// <param name="session">The painting session; nothing renders when null.</param>
    /// <param name="crosshairNormX">The hand's horizontal position, 0..1 across the still; null hides the crosshair.</param>
    /// <param name="crosshairNormY">The hand's vertical position, 0..1 down the still; null hides the crosshair.</param>
    /// <param name="isPainting"><c>true</c> while the open palm is actively painting (the crosshair shows in the active ink color).</param>
    public static void Render(SKSurface surface, SKImageInfo info, PaintingSession session,
        float? crosshairNormX, float? crosshairNormY, bool isPainting)
    {
        if (surface == null || session == null) { return; }

        session.Session.Render(surface, info);

        if (!crosshairNormX.HasValue || !crosshairNormY.HasValue) { return; }

        CodeBrix.Imaging.PointF center = session.NormalizedToView(
            crosshairNormX.Value, crosshairNormY.Value, info.Width, info.Height);
        float radius = session.GetBrushRadiusInView(info.Width, info.Height);
        if (radius <= 0) { return; }

        SKCanvas canvas = surface.Canvas;
        SKColor ringColor = isPainting ? new SKColor(80, 255, 80) : SKColors.White;

        //A dark halo behind the ring keeps the cursor visible over any photo content
        using (var halo = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            Color = new SKColor(0, 0, 0, 140),
            IsAntialias = true,
        })
        {
            canvas.DrawCircle(center.X, center.Y, radius, halo);
        }

        using (var ring = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = ringColor,
            IsAntialias = true,
        })
        {
            canvas.DrawCircle(center.X, center.Y, radius, ring);

            float arm = Math.Max(8f, radius * 0.35f);
            canvas.DrawLine(center.X - arm, center.Y, center.X + arm, center.Y, ring);
            canvas.DrawLine(center.X, center.Y - arm, center.X, center.Y + arm, ring);
        }
    }
}
