using CodeBrix.SkiaSvg;
using SkiaSharp;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// Rasterizes SVG vector art (via CodeBrix.SkiaSvg) into an <see cref="SKBitmap"/> for the
/// 2D asset viewer, and into PNG bytes for grid thumbnails.
/// </summary>
public static class SvgImageDecoder
{
    /// <summary>
    /// Rasterizes an SVG so its longer side lands on maxDimension (small icons are scaled up —
    /// vectors stay crisp). The caller owns the returned bitmap.
    /// </summary>
    /// <param name="svgBytes">The raw bytes of the .svg file.</param>
    /// <param name="maxDimension">The target size of the longer side, in pixels.</param>
    /// <returns>The rasterized bitmap (transparent background).</returns>
    /// <exception cref="InvalidDataException">The bytes are not a renderable SVG.</exception>
    public static SKBitmap Render(byte[] svgBytes, int maxDimension = 1024)
    {
        ArgumentNullException.ThrowIfNull(svgBytes);

        using var stream = new MemoryStream(svgBytes, writable: false);
        SKSvg svg;
        try
        {
            svg = SKSvg.CreateFromStream(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("The data is not a renderable SVG.", ex);
        }

        using var _ = svg;
        var picture = svg.Picture;
        var rect = picture?.CullRect ?? SKRect.Empty;
        if (picture == null || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidDataException("The data is not a renderable SVG.");
        }

        var scale = Math.Min(maxDimension / rect.Width, maxDimension / rect.Height);
        var width = Math.Max(1, (int)MathF.Round(rect.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(rect.Height * scale));

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        //Drawn by hand rather than through SKPictureExtensions.ToBitmap, which does not
        //translate by CullRect's origin — an SVG whose content does not start at (0,0)
        //would come out clipped
        canvas.Scale(scale);
        canvas.Translate(-rect.Left, -rect.Top);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    /// <summary>
    /// Rasterizes an SVG to PNG bytes — the form the grid's thumbnail images want.
    /// </summary>
    /// <param name="svgBytes">The raw bytes of the .svg file.</param>
    /// <param name="maxDimension">The target size of the longer side, in pixels.</param>
    /// <returns>The encoded PNG bytes.</returns>
    /// <exception cref="InvalidDataException">The bytes are not a renderable SVG.</exception>
    public static byte[] RenderToPngBytes(byte[] svgBytes, int maxDimension = 256)
    {
        using var bitmap = Render(svgBytes, maxDimension);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
