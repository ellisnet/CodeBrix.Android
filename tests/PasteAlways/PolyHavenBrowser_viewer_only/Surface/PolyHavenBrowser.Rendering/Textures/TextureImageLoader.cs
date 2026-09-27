using SkiaSharp;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// One-stop loader that turns any downloaded Poly Haven texture map file into a
/// displayable <see cref="SKBitmap"/>: JPEG/PNG/WebP decode via CodeBrix.Imaging, and EXR
/// data maps via TinyEXR with min–max normalization (data maps such as displacement are
/// not photographs — normalizing shows their full value range).
/// </summary>
public static class TextureImageLoader
{
    /// <summary>Loads a texture image file for display, dispatching on the file extension.</summary>
    /// <param name="path">The path of the image file (.jpg, .png, .webp, .exr, .hdr, ...).</param>
    public static SKBitmap LoadForDisplay(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadForDisplay(File.ReadAllBytes(path), Path.GetExtension(path));
    }

    /// <summary>Loads a texture image for display from bytes, dispatching on the given extension.</summary>
    /// <param name="data">The raw file bytes.</param>
    /// <param name="fileExtension">The file extension (with or without the leading dot), e.g. <c>.exr</c>.</param>
    public static SKBitmap LoadForDisplay(byte[] data, string fileExtension)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        switch (NormalizeExtension(fileExtension))
        {
            case "exr":
                return NormalizeToBitmap(ExrDecoder.Decode(data));
            case "hdr":
                return ToneMapper.ToBitmap(RadianceHdrDecoder.Decode(data));
            default:
                return LdrImageDecoder.Decode(data);
        }
    }

    /// <summary>
    /// Loads a high-dynamic-range image (.exr or .hdr) as linear floats, for callers that
    /// want to tone-map or inspect values themselves.
    /// </summary>
    public static FloatImage LoadFloatImage(byte[] data, string fileExtension)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        return NormalizeExtension(fileExtension) switch
        {
            "exr" => ExrDecoder.Decode(data),
            "hdr" => RadianceHdrDecoder.Decode(data),
            var other => throw new NotSupportedException(
                $"'{other}' is not a float-image format; use {nameof(LoadForDisplay)} for LDR formats."),
        };
    }

    /// <summary>
    /// Maps a float image to a bitmap by normalizing its full value range to [0, 1]
    /// (no tone mapping) — the right presentation for non-photographic data maps.
    /// </summary>
    internal static unsafe SKBitmap NormalizeToBitmap(FloatImage image)
    {
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        foreach (var value in image.Pixels)
        {
            if (float.IsNaN(value))
            {
                continue;
            }
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        var range = max - min;
        if (!float.IsFinite(range) || range <= 0f)
        {
            min = 0f;
            range = 1f;
        }

        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = (byte*)bitmap.GetPixels();
        var source = image.Pixels;
        var width = image.Width;
        var localMin = min;
        var localRange = range;

        Parallel.For(0, image.Height, y =>
        {
            var src = y * width * 3;
            var dst = pixels + (long)y * width * 4;
            for (var x = 0; x < width; x++)
            {
                dst[0] = ToByte((source[src] - localMin) / localRange);
                dst[1] = ToByte((source[src + 1] - localMin) / localRange);
                dst[2] = ToByte((source[src + 2] - localMin) / localRange);
                dst[3] = 255;
                src += 3;
                dst += 4;
            }
        });

        return bitmap;

        static byte ToByte(float normalized) => (byte)Math.Clamp((int)(normalized * 255f + 0.5f), 0, 255);
    }

    private static string NormalizeExtension(string fileExtension) =>
        fileExtension.TrimStart('.').ToLowerInvariant();
}
