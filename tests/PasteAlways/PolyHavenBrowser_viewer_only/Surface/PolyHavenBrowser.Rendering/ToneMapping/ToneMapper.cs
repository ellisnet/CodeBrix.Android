using SkiaSharp;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Converts linear-light HDR pixels to displayable sRGB: exposure multiply → tone-map
/// operator → sRGB transfer function.
/// </summary>
public static class ToneMapper
{
    /// <summary>
    /// Tone-maps a whole <see cref="FloatImage"/> into a new RGBA <see cref="SKBitmap"/>.
    /// </summary>
    /// <param name="image">The linear-light image to map.</param>
    /// <param name="exposure">A linear exposure multiplier applied before the operator (1 = unchanged).</param>
    /// <param name="toneMapOperator">The compression operator to apply.</param>
    public static unsafe SKBitmap ToBitmap(
        FloatImage image, float exposure = 1f, ToneMapOperator toneMapOperator = ToneMapOperator.AcesFilmic)
    {
        ArgumentNullException.ThrowIfNull(image);

        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = (byte*)bitmap.GetPixels();
        var source = image.Pixels;
        var width = image.Width;

        Parallel.For(0, image.Height, y =>
        {
            var src = y * width * 3;
            var dst = pixels + (long)y * width * 4;
            for (var x = 0; x < width; x++)
            {
                MapPixel(source[src], source[src + 1], source[src + 2], exposure, toneMapOperator,
                    out var r, out var g, out var b);
                dst[0] = r;
                dst[1] = g;
                dst[2] = b;
                dst[3] = 255;
                src += 3;
                dst += 4;
            }
        });

        return bitmap;
    }

    /// <summary>Tone-maps a single linear-light RGB value to sRGB bytes.</summary>
    public static void MapPixel(
        float r, float g, float b, float exposure, ToneMapOperator toneMapOperator,
        out byte rOut, out byte gOut, out byte bOut)
    {
        rOut = ToSrgbByte(ApplyOperator(r * exposure, toneMapOperator));
        gOut = ToSrgbByte(ApplyOperator(g * exposure, toneMapOperator));
        bOut = ToSrgbByte(ApplyOperator(b * exposure, toneMapOperator));
    }

    /// <summary>Applies the tone-map operator to one linear channel value, returning a value in [0, 1].</summary>
    internal static float ApplyOperator(float value, ToneMapOperator toneMapOperator)
    {
        if (value <= 0f)
        {
            return 0f;
        }

        return toneMapOperator switch
        {
            // Krzysztof Narkowicz's ACES filmic approximation.
            ToneMapOperator.AcesFilmic =>
                Math.Clamp(value * (2.51f * value + 0.03f) / (value * (2.43f * value + 0.59f) + 0.14f), 0f, 1f),
            ToneMapOperator.Reinhard => value / (1f + value),
            ToneMapOperator.Clamp => Math.Min(value, 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(toneMapOperator), toneMapOperator, "Unknown tone-map operator."),
        };
    }

    /// <summary>Applies the sRGB transfer function to a linear [0, 1] value and quantizes to a byte.</summary>
    internal static byte ToSrgbByte(float linear)
    {
        var srgb = linear <= 0.0031308f
            ? 12.92f * linear
            : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)(srgb * 255f + 0.5f), 0, 255);
    }
}
