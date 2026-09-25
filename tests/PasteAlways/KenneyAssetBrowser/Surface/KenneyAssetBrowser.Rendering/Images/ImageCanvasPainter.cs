using SkiaSharp;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// Paints a 2D asset image onto a Skia canvas for preview: dark backdrop, a checkerboard
/// behind the image to reveal transparency, fit-or-zoom scaling, and an optional highlight
/// rectangle for the selected spritesheet region. Pure Skia — no UI framework types — so the
/// layout math is unit-testable.
/// </summary>
public sealed class ImageCanvasPainter
{
    private static readonly SKColor Backdrop = new(0x17, 0x1A, 0x20);
    private static readonly SKColor CheckerLight = new(0x3A, 0x3F, 0x4A);
    private static readonly SKColor CheckerDark = new(0x2A, 0x2F, 0x39);
    private static readonly SKColor HighlightStroke = new(0xFF, 0xB0, 0x2E);
    private static readonly SKColor HighlightDim = new(0x00, 0x00, 0x00, 0x99);

    private const float CheckerTileSize = 8f;

    /// <summary>Gets or sets the image to paint; the caller owns the bitmap's lifetime.</summary>
    public SKBitmap? Bitmap { get; set; }

    /// <summary>
    /// Gets or sets the zoom applied on top of the fit-to-canvas scale:
    /// <c>1</c> fits the image inside the canvas, <c>2</c> doubles that, and so on.
    /// </summary>
    public float ZoomFactor { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the image-space rectangle to spotlight (a spritesheet region), or
    /// <c>null</c> for no highlight. Everything outside the rectangle is dimmed.
    /// </summary>
    public SKRectI? HighlightRegion { get; set; }

    /// <summary>Gets or sets whether the transparency checkerboard is painted behind the image.</summary>
    public bool ShowCheckerboard { get; set; } = true;

    /// <summary>
    /// Paints the current image state onto a canvas of the given size.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="width">The canvas width, in pixels.</param>
    /// <param name="height">The canvas height, in pixels.</param>
    public void Paint(SKCanvas canvas, float width, float height)
    {
        canvas.Clear(Backdrop);

        var bitmap = Bitmap;
        if (bitmap == null || width <= 0 || height <= 0) { return; }

        var imageRect = GetImageRect(width, height);

        if (ShowCheckerboard)
        {
            PaintCheckerboard(canvas, imageRect);
        }

        //Nearest-neighbor above 2x keeps low-resolution pixel art crisp instead of smearing it
        var scale = imageRect.Width / bitmap.Width;
        var sampling = new SKSamplingOptions(scale >= 2f ? SKFilterMode.Nearest : SKFilterMode.Linear);
        canvas.DrawBitmap(bitmap, imageRect, sampling);

        if (HighlightRegion is { } region)
        {
            PaintHighlight(canvas, imageRect, region, bitmap);
        }
    }

    /// <summary>
    /// Computes the canvas-space rectangle the image is painted into for a canvas of the
    /// given size: centered, scaled to fit, then multiplied by <see cref="ZoomFactor"/>.
    /// </summary>
    /// <param name="width">The canvas width, in pixels.</param>
    /// <param name="height">The canvas height, in pixels.</param>
    public SKRect GetImageRect(float width, float height)
    {
        var bitmap = Bitmap;
        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0 || width <= 0 || height <= 0)
        {
            return SKRect.Empty;
        }

        var fitScale = Math.Min(width / bitmap.Width, height / bitmap.Height);

        //Leave small images at their natural size until the user zooms in
        if (fitScale > 1f) { fitScale = 1f; }

        var scale = fitScale * Math.Max(0.01f, ZoomFactor);
        var drawWidth = bitmap.Width * scale;
        var drawHeight = bitmap.Height * scale;
        var left = (width - drawWidth) / 2f;
        var top = (height - drawHeight) / 2f;
        return new SKRect(left, top, left + drawWidth, top + drawHeight);
    }

    /// <summary>
    /// Maps a canvas-space point to image pixel coordinates, or <c>null</c> when the point
    /// falls outside the painted image.
    /// </summary>
    /// <param name="point">The canvas-space point.</param>
    /// <param name="width">The canvas width, in pixels.</param>
    /// <param name="height">The canvas height, in pixels.</param>
    public SKPointI? CanvasToImage(SKPoint point, float width, float height)
    {
        var bitmap = Bitmap;
        var imageRect = GetImageRect(width, height);
        if (bitmap == null || imageRect.IsEmpty || !imageRect.Contains(point.X, point.Y))
        {
            return null;
        }

        var x = (point.X - imageRect.Left) / imageRect.Width * bitmap.Width;
        var y = (point.Y - imageRect.Top) / imageRect.Height * bitmap.Height;
        return new SKPointI((int)x, (int)y);
    }

    private static void PaintCheckerboard(SKCanvas canvas, SKRect imageRect)
    {
        canvas.Save();
        canvas.ClipRect(imageRect);

        using var lightPaint = new SKPaint { Color = CheckerLight };
        using var darkPaint = new SKPaint { Color = CheckerDark };
        canvas.DrawRect(imageRect, lightPaint);

        for (var row = 0; row * CheckerTileSize < imageRect.Height; row++)
        {
            for (var column = row % 2; column * CheckerTileSize < imageRect.Width; column += 2)
            {
                canvas.DrawRect(
                    new SKRect(
                        imageRect.Left + (column * CheckerTileSize),
                        imageRect.Top + (row * CheckerTileSize),
                        Math.Min(imageRect.Left + ((column + 1) * CheckerTileSize), imageRect.Right),
                        Math.Min(imageRect.Top + ((row + 1) * CheckerTileSize), imageRect.Bottom)),
                    darkPaint);
            }
        }

        canvas.Restore();
    }

    private void PaintHighlight(SKCanvas canvas, SKRect imageRect, SKRectI region, SKBitmap bitmap)
    {
        var scaleX = imageRect.Width / bitmap.Width;
        var scaleY = imageRect.Height / bitmap.Height;
        var regionRect = new SKRect(
            imageRect.Left + (region.Left * scaleX),
            imageRect.Top + (region.Top * scaleY),
            imageRect.Left + (region.Right * scaleX),
            imageRect.Top + (region.Bottom * scaleY));

        //Dim everything except the spotlighted region, then outline it
        canvas.Save();
        canvas.ClipRect(regionRect, SKClipOperation.Difference);
        using var dimPaint = new SKPaint { Color = HighlightDim };
        canvas.DrawRect(imageRect, dimPaint);
        canvas.Restore();

        using var strokePaint = new SKPaint
        {
            Color = HighlightStroke,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
        canvas.DrawRect(regionRect, strokePaint);
    }
}
