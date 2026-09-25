using CodeBrix.Imaging.PixelFormats;
using CodeBrix.PdfDocuments.Drawing;
using CodeBrix.PdfDocuments.Fonts;
using CodeBrix.PdfDocuments.Utils;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Registers the book's embedded OFL-licensed fonts (and the imaging back-end)
/// with the CodeBrix.PdfDocuments font system, so rendered PDFs look identical
/// on every platform regardless of the fonts installed on the OS.
/// </summary>
internal static class BookFonts
{
    /// <summary>Family name of the book serif (body text, headings) — EB Garamond.</summary>
    public const string SerifFamily = "EB Garamond";

    /// <summary>Family name of the book sans (captions, labels, tables) — Source Sans 3.</summary>
    public const string SansFamily = "Source Sans 3";

    private const string ResourcePrefix = "WikipediaPublisher.RenderArticle.Fonts.";

    private static readonly object _locker = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (_locker)
        {
            if (_registered) { return; }

            //The PDF image pipeline needs an imaging implementation before any image can be placed
            ImageSource.ImageSourceImpl ??= new ImagingImageSource<Rgba32>();

            var assembly = typeof(BookFonts).Assembly;

            var serifFaces = new[]
            {
                "EBGaramond-Regular", "EBGaramond-Italic", "EBGaramond-Bold", "EBGaramond-BoldItalic"
            };
            var serifResolver = new EmbeddedFontResolver(
                fontFamilyName: SerifFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "EBGaramond-Regular", EmbeddedResourceName: $"{ResourcePrefix}EBGaramond-Regular.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "EBGaramond-Italic", EmbeddedResourceName: $"{ResourcePrefix}EBGaramond-Italic.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "EBGaramond-Bold", EmbeddedResourceName: $"{ResourcePrefix}EBGaramond-Bold.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "EBGaramond-BoldItalic", EmbeddedResourceName: $"{ResourcePrefix}EBGaramond-BoldItalic.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            //The 'Semibold' face intentionally contains 'bold' in its name, so the
            //  resolver serves it for bold requests — semibold reads better than a
            //  heavy bold at caption sizes.
            var sansFaces = new[]
            {
                "SourceSans3-Regular", "SourceSans3-Italic", "SourceSans3-Semibold"
            };
            var sansResolver = new EmbeddedFontResolver(
                fontFamilyName: SansFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "SourceSans3-Regular", EmbeddedResourceName: $"{ResourcePrefix}SourceSans3-Regular.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "SourceSans3-Italic", EmbeddedResourceName: $"{ResourcePrefix}SourceSans3-Italic.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "SourceSans3-Semibold", EmbeddedResourceName: $"{ResourcePrefix}SourceSans3-Semibold.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            //MetaFontResolver routes family-name lookups (ResolveTypeface) via any registered
            //  resolver whose DefaultFontName matches, but face-name lookups (GetFont) require
            //  a registration per face name.
            foreach (var face in serifFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, serifResolver);
            }
            foreach (var face in sansFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, sansResolver);
            }

            _registered = true;
        }
    }
}
