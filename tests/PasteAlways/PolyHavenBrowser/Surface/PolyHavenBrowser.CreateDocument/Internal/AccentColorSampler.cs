using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Imaging.Processing;

namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>A plain sRGB color triple, decoupled from any imaging/PDF color type.</summary>
internal readonly record struct AccentColor(byte R, byte G, byte B);

/// <summary>
/// Picks the sheet's per-model accent color: the dominant saturated hue of the model's
/// catalog thumbnail, deepened until it reads as display type on white paper. Essentially
/// grayscale thumbnails (and undecodable ones) fall back to a fixed slate blue, so every
/// sheet gets a deliberate accent. Deterministic: the same thumbnail always yields the
/// same accent.
/// </summary>
internal static class AccentColorSampler
{
    /// <summary>The fallback accent (a quiet slate blue) for grayscale or missing thumbnails.</summary>
    public static readonly AccentColor Fallback = new(0x3D, 0x6A, 0x8F);

    //Sampling size: enough pixels to find the dominant hue, cheap to scan.
    private const int SampleSize = 64;

    //A pixel must be at least this saturated (and not blown out / crushed) to vote on hue.
    private const float MinSaturation = 0.22f;
    private const float MinValue = 0.12f;
    private const float MaxValue = 0.97f;

    //When fewer than this fraction of sampled pixels are colorful, the image is treated as
    //  grayscale and the fallback accent is used.
    private const float MinColorfulFraction = 0.02f;

    private const int HueBins = 24;

    /// <summary>Samples the accent color from an encoded thumbnail image.</summary>
    public static AccentColor Sample(byte[]? thumbnailBytes)
    {
        if (thumbnailBytes is not { Length: > 0 }) { return Fallback; }

        try
        {
            using var image = Image.Load<Rgba32>(thumbnailBytes);
            image.Mutate(x => x.Resize(SampleSize, SampleSize));
            return SamplePixels(image);
        }
        catch
        {
            //An undecodable thumbnail must never block sheet creation.
            return Fallback;
        }
    }

    private static AccentColor SamplePixels(Image<Rgba32> image)
    {
        //Vote each colorful pixel's hue into a histogram, weighted by how vivid it is, then
        //  average the color of the winning bin's voters.
        var binWeights = new float[HueBins];
        var binR = new float[HueBins];
        var binG = new float[HueBins];
        var binB = new float[HueBins];

        var totalPixels = 0;
        var colorfulPixels = 0;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                totalPixels++;

                var (hue, saturation, value) = ToHsv(pixel);
                if (saturation < MinSaturation || value < MinValue || value > MaxValue)
                {
                    continue;
                }

                colorfulPixels++;
                var bin = Math.Clamp((int)(hue / 360f * HueBins), 0, HueBins - 1);
                var weight = saturation * value;
                binWeights[bin] += weight;
                binR[bin] += pixel.R * weight;
                binG[bin] += pixel.G * weight;
                binB[bin] += pixel.B * weight;
            }
        }

        if (totalPixels == 0 || colorfulPixels < totalPixels * MinColorfulFraction)
        {
            return Fallback;
        }

        var winner = 0;
        for (var bin = 1; bin < HueBins; bin++)
        {
            if (binWeights[bin] > binWeights[winner]) { winner = bin; }
        }
        if (binWeights[winner] <= 0f) { return Fallback; }

        var average = new Rgba32(
            (byte)Math.Clamp(binR[winner] / binWeights[winner], 0f, 255f),
            (byte)Math.Clamp(binG[winner] / binWeights[winner], 0f, 255f),
            (byte)Math.Clamp(binB[winner] / binWeights[winner], 0f, 255f));

        return DeepenForPaper(average);
    }

    //The accent is used for kickers, rules and spec highlights on white paper, so it must be
    //  saturated enough to feel deliberate and dark enough to pass as text.
    private static AccentColor DeepenForPaper(Rgba32 color)
    {
        var (hue, saturation, value) = ToHsv(color);
        saturation = Math.Clamp(saturation, 0.42f, 0.85f);
        value = Math.Clamp(value, 0.30f, 0.62f);
        return FromHsv(hue, saturation, value);
    }

    private static (float Hue, float Saturation, float Value) ToHsv(Rgba32 pixel)
    {
        var r = pixel.R / 255f;
        var g = pixel.G / 255f;
        var b = pixel.B / 255f;

        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        float hue;
        if (delta <= 0f) { hue = 0f; }
        else if (max == r) { hue = 60f * (((g - b) / delta) % 6f); }
        else if (max == g) { hue = 60f * ((b - r) / delta + 2f); }
        else { hue = 60f * ((r - g) / delta + 4f); }
        if (hue < 0f) { hue += 360f; }

        var saturation = max <= 0f ? 0f : delta / max;
        return (hue, saturation, max);
    }

    private static AccentColor FromHsv(float hue, float saturation, float value)
    {
        var c = value * saturation;
        var x = c * (1f - MathF.Abs(hue / 60f % 2f - 1f));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x),
        };

        return new AccentColor(
            (byte)Math.Clamp((r + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((g + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((b + m) * 255f, 0f, 255f));
    }
}
