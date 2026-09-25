using SkiaSharp;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// Renders a font file (TTF/OTF) into a specimen sheet bitmap — family name, alphabet,
/// digits and a pangram at a ramp of sizes — for display in the 2D asset viewer.
/// </summary>
public static class FontSpecimenRenderer
{
    private const string Pangram = "The quick brown fox jumps over the lazy dog";

    private static readonly SKColor TitleColor = new(0xF2, 0xF4, 0xF8);
    private static readonly SKColor BodyColor = new(0xC8, 0xCE, 0xD9);
    private static readonly SKColor CaptionColor = new(0x6E, 0x76, 0x86);

    /// <summary>
    /// Renders the specimen sheet. The caller owns the returned bitmap.
    /// </summary>
    /// <param name="fontBytes">The raw bytes of the font file.</param>
    /// <returns>The specimen bitmap (transparent background, light text).</returns>
    /// <exception cref="InvalidDataException">The bytes are not a loadable font.</exception>
    public static SKBitmap Render(byte[] fontBytes)
    {
        ArgumentNullException.ThrowIfNull(fontBytes);

        using var data = SKData.CreateCopy(fontBytes);
        var typeface = SKTypeface.FromData(data)
            ?? throw new InvalidDataException("The data is not a loadable font.");

        using (typeface)
        {
            var bitmap = new SKBitmap(new SKImageInfo(1100, 760, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            using var paint = new SKPaint { Color = TitleColor, IsAntialias = true };
            var y = 84f;

            //Family name as the headline, in the font itself
            using (var titleFont = new SKFont(typeface, 52f))
            {
                canvas.DrawText(typeface.FamilyName, 48f, y, SKTextAlign.Left, titleFont, paint);
            }

            y += 64f;
            paint.Color = BodyColor;
            using (var alphabetFont = new SKFont(typeface, 30f))
            {
                canvas.DrawText("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 48f, y, SKTextAlign.Left, alphabetFont, paint);
                y += 44f;
                canvas.DrawText("abcdefghijklmnopqrstuvwxyz", 48f, y, SKTextAlign.Left, alphabetFont, paint);
                y += 44f;
                canvas.DrawText("0123456789  !?@#$%&*()[]{}<>+-=/\\", 48f, y, SKTextAlign.Left, alphabetFont, paint);
            }

            //The pangram at a ramp of sizes, each labeled with its point size
            y += 40f;
            using var captionFont = new SKFont(SKTypeface.Default, 12f);
            foreach (var size in new[] { 14f, 18f, 24f, 32f, 44f })
            {
                y += size + 22f;
                paint.Color = CaptionColor;
                canvas.DrawText($"{size:0}", 48f, y, SKTextAlign.Left, captionFont, paint);
                paint.Color = BodyColor;
                using var rampFont = new SKFont(typeface, size);
                canvas.DrawText(Pangram, 84f, y, SKTextAlign.Left, rampFont, paint);
            }

            return bitmap;
        }
    }
}
