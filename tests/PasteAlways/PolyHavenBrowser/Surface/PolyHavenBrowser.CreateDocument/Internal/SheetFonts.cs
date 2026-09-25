using CodeBrix.Imaging.PixelFormats;
using CodeBrix.PdfDocuments.Drawing;
using CodeBrix.PdfDocuments.Fonts;
using CodeBrix.PdfDocuments.Utils;

namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>
/// Registers the sheet's embedded OFL-licensed fonts (and the imaging back-end) with the
/// CodeBrix.PdfDocuments font system, so the one-sheet looks identical on every platform
/// regardless of the fonts installed on the OS. Weights that must be addressed exactly
/// (Medium for kickers, ExtraBold for display type) are registered as their own family
/// names, because the resolver's bold/italic matching cannot distinguish them.
/// </summary>
internal static class SheetFonts
{
    /// <summary>Family of the sheet sans (body, specs) — Roboto Regular, with a real Bold face.</summary>
    public const string SansFamily = "Roboto";

    /// <summary>Family of the kicker/caption weight — Roboto Medium.</summary>
    public const string SansMediumFamily = "Roboto Medium";

    /// <summary>Family of the display weight (the big model name) — Roboto ExtraBold.</summary>
    public const string SansHeavyFamily = "Roboto Heavy";

    /// <summary>Family of the pull-quote serif — Merriweather (request italic for the quote).</summary>
    public const string SerifFamily = "Merriweather";

    private const string ResourcePrefix = "PolyHavenBrowser.CreateDocument.Fonts.";

    private static readonly object _locker = new();
    private static bool _registered;

    /// <summary>Registers the fonts exactly once per process.</summary>
    public static void EnsureRegistered()
    {
        lock (_locker)
        {
            if (_registered) { return; }

            //The PDF image pipeline needs an imaging implementation before any image can be placed
            ImageSource.ImageSourceImpl ??= new ImagingImageSource<Rgba32>();

            var assembly = typeof(SheetFonts).Assembly;

            var sansFaces = new[] { "Roboto-Regular", "Roboto-Bold" };
            var sansResolver = new EmbeddedFontResolver(
                fontFamilyName: SansFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "Roboto-Regular", EmbeddedResourceName: $"{ResourcePrefix}Roboto-Regular.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "Roboto-Bold", EmbeddedResourceName: $"{ResourcePrefix}Roboto-Bold.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            var mediumFaces = new[] { "Roboto-Medium" };
            var mediumResolver = new EmbeddedFontResolver(
                fontFamilyName: SansMediumFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "Roboto-Medium", EmbeddedResourceName: $"{ResourcePrefix}Roboto-Medium.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            //The face is deliberately named "Roboto-Heavy" (not "-ExtraBold"): the resolver
            //  matches faces by looking for "bold"/"italic" in their names, so a non-bold
            //  request for this single-face family must not see "bold" in the face name.
            var heavyFaces = new[] { "Roboto-Heavy" };
            var heavyResolver = new EmbeddedFontResolver(
                fontFamilyName: SansHeavyFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "Roboto-Heavy", EmbeddedResourceName: $"{ResourcePrefix}Roboto-ExtraBold.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            var serifFaces = new[] { "Merriweather-Regular", "Merriweather-Italic" };
            var serifResolver = new EmbeddedFontResolver(
                fontFamilyName: SerifFamily,
                fontFaceResources:
                [
                    new EmbeddedResourceFontFace(FaceName: "Merriweather-Regular", EmbeddedResourceName: $"{ResourcePrefix}Merriweather-Regular.ttf"),
                    new EmbeddedResourceFontFace(FaceName: "Merriweather-Italic", EmbeddedResourceName: $"{ResourcePrefix}Merriweather-Italic.ttf")
                ],
                fontEmbeddedResourceAssembly: assembly);

            //MetaFontResolver routes family-name lookups (ResolveTypeface) via any registered
            //  resolver whose DefaultFontName matches, but face-name lookups (GetFont) require
            //  a registration per face name.
            foreach (var face in sansFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, sansResolver);
            }
            foreach (var face in mediumFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, mediumResolver);
            }
            foreach (var face in heavyFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, heavyResolver);
            }
            foreach (var face in serifFaces)
            {
                MetaFontResolver.Instance.RegisterFontResolver(face, serifResolver);
            }

            _registered = true;
        }
    }
}
