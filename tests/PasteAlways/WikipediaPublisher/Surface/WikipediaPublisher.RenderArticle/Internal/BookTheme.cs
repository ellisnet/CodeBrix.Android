using CodeBrix.PdfDocCreate.DocumentObjectModel;
using System.Linq;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// All layout metrics and shared colors for one book rendering, derived from the
/// selected trim size. Every dimension is in typographic points.
/// </summary>
internal sealed class BookTheme
{
    // ── Palette (warm ink on paper with an oxblood accent) ──────────────
    public static readonly Color Ink = new(31, 30, 28);
    public static readonly Color Accent = new(122, 44, 38);
    public static readonly Color Muted = new(112, 108, 102);
    public static readonly Color Hairline = new(203, 197, 189);

    public PageSizeInfo Page { get; private init; }

    public double PageWidth { get; private init; }
    public double PageHeight { get; private init; }
    public double InnerMargin { get; private init; }
    public double OuterMargin { get; private init; }
    public double TopMargin { get; private init; }
    public double BottomMargin { get; private init; }

    /// <summary>The width of the text block (page width minus side margins).</summary>
    public double TextWidth => PageWidth - InnerMargin - OuterMargin;

    /// <summary>The height of the text block (page height minus top/bottom margins).</summary>
    public double TextHeight => PageHeight - TopMargin - BottomMargin;

    /// <summary>Body text size in points; every other size in the scale derives from this.</summary>
    public double BodySize { get; private init; }

    /// <summary>Body leading (line spacing) in points.</summary>
    public double Leading => BodySize * 1.47;

    /// <summary>Display size for the cover title.</summary>
    public double CoverTitleSize { get; private init; }

    public double H1Size => BodySize * 1.62;
    public double H2Size => BodySize * 1.27;
    public double H3Size => BodySize * 1.08;
    public double QuoteSize => BodySize * 1.02;
    public double CaptionSize => BodySize * 0.81;
    public double LabelSize => BodySize * 0.70;
    public double TableSize => BodySize * 0.83;
    public double FolioSize => BodySize * 0.88;
    public double RaisedCapSize => BodySize * 2.35;

    public static BookTheme For(PageSizeOption option)
    {
        var page = PageSizeInfo.For(option);

        return option switch
        {
            PageSizeOption.EightByTen => new BookTheme
            {
                Page = page,
                PageWidth = page.WidthPoints,
                PageHeight = page.HeightPoints,
                InnerMargin = 68,
                OuterMargin = 56,
                TopMargin = 60,
                BottomMargin = 78,
                BodySize = 10.5,
                CoverTitleSize = 40
            },
            PageSizeOption.SixByNine => new BookTheme
            {
                Page = page,
                PageWidth = page.WidthPoints,
                PageHeight = page.HeightPoints,
                InnerMargin = 52,
                OuterMargin = 44,
                TopMargin = 50,
                BottomMargin = 66,
                BodySize = 9.75,
                CoverTitleSize = 31
            },
            PageSizeOption.Letter => new BookTheme
            {
                Page = page,
                PageWidth = page.WidthPoints,
                PageHeight = page.HeightPoints,
                InnerMargin = 81,
                OuterMargin = 72,
                TopMargin = 72,
                BottomMargin = 90,
                BodySize = 11,
                CoverTitleSize = 42
            },
            _ => new BookTheme //A4
            {
                Page = page,
                PageWidth = page.WidthPoints,
                PageHeight = page.HeightPoints,
                InnerMargin = 78,
                OuterMargin = 70,
                TopMargin = 74,
                BottomMargin = 92,
                BodySize = 10.75,
                CoverTitleSize = 41
            }
        };
    }

    /// <summary>
    /// Spaces out the characters of a label ("HISTORY" → "H I S T O R Y") for a
    /// small-caps kicker effect, preserving word boundaries with wider gaps.
    /// Non-breaking spaces are used because MigraDoc collapses runs of ordinary
    /// blanks into a single space when laying out text.
    /// </summary>
    public static string Letterspace(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return ""; }

        const char letterGap = '\u00A0';
        const string wordGap = "\u00A0\u00A0\u00A0";

        var words = text.ToUpperInvariant()
            .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(word => string.Join(letterGap, word.ToCharArray()));
        return string.Join(wordGap, words);
    }
}
