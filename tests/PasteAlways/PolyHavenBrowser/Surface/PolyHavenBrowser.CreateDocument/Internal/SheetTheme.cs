using CodeBrix.PdfDocuments.Drawing;

namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>
/// The one-sheet's design constants: US Letter portrait metrics and the print-light
/// palette (dark ink on white paper), plus the per-model accent and its derived tints.
/// </summary>
internal sealed class SheetTheme
{
    /// <summary>Creates the theme around the model's sampled accent color.</summary>
    public SheetTheme(AccentColor accent)
    {
        Accent = XColor.FromArgb(accent.R, accent.G, accent.B);
        AccentSoft = Blend(accent, toward: (255, 255, 255), amount: 0.42f);
    }

    // ── Page metrics (points; US Letter portrait) ─────────────────────────

    /// <summary>The page width.</summary>
    public const double PageWidth = 612;

    /// <summary>The page height.</summary>
    public const double PageHeight = 792;

    /// <summary>The left/right page margin.</summary>
    public const double MarginX = 40;

    /// <summary>The content width between the margins.</summary>
    public const double ContentWidth = PageWidth - 2 * MarginX;

    // ── Palette ───────────────────────────────────────────────────────────

    /// <summary>The near-black body ink.</summary>
    public static readonly XColor Ink = XColor.FromArgb(0x1C, 0x1F, 0x24);

    /// <summary>The secondary text gray (paragraphs).</summary>
    public static readonly XColor Secondary = XColor.FromArgb(0x43, 0x48, 0x4F);

    /// <summary>The tertiary text gray (captions, labels).</summary>
    public static readonly XColor Tertiary = XColor.FromArgb(0x87, 0x8D, 0x95);

    /// <summary>The hairline rule/border gray.</summary>
    public static readonly XColor Hairline = XColor.FromArgb(0xE3, 0xE6, 0xEA);

    /// <summary>The paper white.</summary>
    public static readonly XColor Paper = XColor.FromArgb(0xFF, 0xFF, 0xFF);

    /// <summary>The model's accent color (kickers, rules, spec highlights).</summary>
    public XColor Accent { get; }

    /// <summary>The accent lightened toward paper (tags, quiet accents).</summary>
    public XColor AccentSoft { get; }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loosely letterspaces an ALL-CAPS kicker by interleaving thin spaces (U+2009) —
    /// XGraphics has no character-spacing property, so the spacing is put into the string
    /// itself. Word gaps keep their real space and read wider than the letter gaps.
    /// </summary>
    public static string Letterspace(string text) =>
        string.Join('\u2009', text.ToCharArray());

    private static XColor Blend(AccentColor from, (byte R, byte G, byte B) toward, float amount)
    {
        return XColor.FromArgb(
            (byte)(from.R + (toward.R - from.R) * amount),
            (byte)(from.G + (toward.G - from.G) * amount),
            (byte)(from.B + (toward.B - from.B) * amount));
    }
}
