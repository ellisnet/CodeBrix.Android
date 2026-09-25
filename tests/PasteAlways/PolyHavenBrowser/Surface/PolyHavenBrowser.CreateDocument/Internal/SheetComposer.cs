using CodeBrix.PdfDocuments.Drawing;

namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>
/// Draws the complete marketing one-sheet onto a page's <see cref="XGraphics"/>: the
/// accent-colored header, the hero beauty shot with the catalog inset and first-person
/// pull quote, the factual and persuasive copy columns, the four-shot gallery, the
/// specification grid, and the CC0 footer. All placement is absolute (a poster, not a
/// flowing document); every region clamps or truncates its content so the sheet is always
/// exactly one page.
/// </summary>
internal sealed class SheetComposer
{
    //Vertical anchors of the poster's bands (points from the top of the page).
    private const double KickerY = 38;
    private const double TitleY = 50;
    private const double TaglineY = 90;
    private const double AuthorY = 106;
    private const double AccentBarY = 120;
    private const double HeroTop = 132;
    private const double HeroHeight = 280;
    private const double HeroWidth = 336;
    private const double SideGutter = 16;
    private const double InsetMaxHeight = 150;
    private const double CopyTop = 424;
    private const double CopyBottom = 556;
    private const double TagsY = 560;
    private const double GalleryTop = 576;
    private const double GalleryHeight = 104;
    private const double SpecsRuleY = 706;
    private const double FooterY = 764;

    private const double CornerRadius = 10;
    private const double InsetCornerRadius = 8;

    //The pull quote's type: its preferred size, how small it may shrink to fit the room the
    //  catalog inset leaves it, and its leading as a multiple of the size.
    private const double QuoteFontSize = 14.5;
    private const double QuoteMinFontSize = 10.5;
    private const double QuoteLineRatio = 21.0 / QuoteFontSize;

    private readonly MarketingSheetRequest _request;
    private readonly SheetTheme _theme;

    public SheetComposer(MarketingSheetRequest request, SheetTheme theme)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <summary>Draws the whole sheet.</summary>
    public void Compose(XGraphics gfx)
    {
        ArgumentNullException.ThrowIfNull(gfx);

        DrawHeader(gfx);
        DrawHeroRow(gfx);
        DrawCopyColumns(gfx);
        DrawTags(gfx);
        DrawGallery(gfx);
        DrawSpecs(gfx);
        DrawFooter(gfx);
    }

    // ── Header: kicker, display name, tagline, author, accent bar ─────────

    private void DrawHeader(XGraphics gfx)
    {
        var accentBrush = new XSolidBrush(_theme.Accent);

        var kickerFont = new XFont(SheetFonts.SansMediumFamily, 8.5);
        gfx.DrawString(
            SheetTheme.Letterspace(SalesCopyBuilder.BuildKicker(_request)),
            kickerFont, accentBrush,
            new XRect(SheetTheme.MarginX, KickerY, SheetTheme.ContentWidth, 12),
            XStringFormats.TopLeft);

        //Display name, shrunk to fit the content width when a long name demands it.
        var titleSize = 34.0;
        XFont titleFont;
        while (true)
        {
            titleFont = new XFont(SheetFonts.SansHeavyFamily, titleSize);
            if (titleSize <= 22 || gfx.MeasureString(_request.ModelName, titleFont).Width <= SheetTheme.ContentWidth)
            {
                break;
            }
            titleSize -= 1;
        }
        gfx.DrawString(_request.ModelName, titleFont, new XSolidBrush(SheetTheme.Ink),
            new XRect(SheetTheme.MarginX, TitleY, SheetTheme.ContentWidth, 40),
            XStringFormats.TopLeft);

        gfx.DrawString(SalesCopyBuilder.BuildTagline(_request),
            new XFont(SheetFonts.SansFamily, 11.5), new XSolidBrush(SheetTheme.Secondary),
            new XRect(SheetTheme.MarginX, TaglineY, SheetTheme.ContentWidth, 15),
            XStringFormats.TopLeft);

        if (!string.IsNullOrWhiteSpace(_request.AuthorLine))
        {
            gfx.DrawString(_request.AuthorLine,
                new XFont(SheetFonts.SansMediumFamily, 8.5), new XSolidBrush(SheetTheme.Tertiary),
                new XRect(SheetTheme.MarginX, AuthorY, SheetTheme.ContentWidth, 11),
                XStringFormats.TopLeft);
        }

        gfx.DrawRectangle(accentBrush, SheetTheme.MarginX, AccentBarY, 56, 3);
    }

    // ── Hero row: beauty shot left; catalog inset + pull quote right ──────

    private void DrawHeroRow(XGraphics gfx)
    {
        var heroRect = new XRect(SheetTheme.MarginX, HeroTop, HeroWidth, HeroHeight);
        var heroBytes = _request.HeroShotBytes ?? _request.CatalogThumbnailBytes;
        DrawCoverImage(gfx, heroBytes, heroRect, CornerRadius);

        var sideX = SheetTheme.MarginX + HeroWidth + SideGutter;
        var sideWidth = SheetTheme.ContentWidth - HeroWidth - SideGutter;

        //The catalog preview inset ("as seen in the catalog"). Catalog thumbnails arrive in
        //  wildly different shapes — Poly Haven trims them to the model, so a bust comes back
        //  taller than it is wide — so the whole image is fitted inside its box rather than
        //  cropped to fill it: nothing is ever cut off and nothing is ever stretched. The
        //  part of the box the image does not cover is simply left alone. Everything below
        //  hangs off where the image really ends, so a short image leaves no gap.
        var insetBox = new XRect(sideX, HeroTop, sideWidth, InsetMaxHeight);
        var insetRect = DrawContainedImage(
            gfx, _request.CatalogThumbnailBytes, insetBox, InsetCornerRadius);

        var captionFont = new XFont(SheetFonts.SansMediumFamily, 6.3);
        gfx.DrawString(SheetTheme.Letterspace("AS SEEN IN THE CATALOG"),
            captionFont, new XSolidBrush(SheetTheme.Tertiary),
            new XRect(sideX, insetRect.Bottom + 6, sideWidth, 9),
            XStringFormats.TopLeft);

        //The model speaks for itself: the first-person pull quote, signed with the full name.
        //  How much room is left depends on how tall the inset image turned out, so the quote
        //  steps down a half point at a time until it fits — losing the end of the line to an
        //  ellipsis would throw away the punch line.
        var quoteText = SalesCopyBuilder.BuildPullQuote(_request);
        var quoteTop = insetRect.Bottom + 30;
        var quoteMaxBottom = HeroTop + HeroHeight - 18;

        var quoteSize = QuoteFontSize;
        XFont quoteFont;
        while (true)
        {
            quoteFont = new XFont(SheetFonts.SerifFamily, quoteSize, XFontStyle.Italic);
            var lines = WrapLines(gfx, quoteText, quoteFont, sideWidth).Count;
            if (quoteSize <= QuoteMinFontSize ||
                quoteTop + lines * quoteSize * QuoteLineRatio <= quoteMaxBottom)
            {
                break;
            }
            quoteSize -= 0.5;
        }

        var quoteBottom = DrawWrapped(gfx, quoteText,
            quoteFont, new XSolidBrush(SheetTheme.Ink),
            sideX, quoteTop, sideWidth, lineHeight: quoteSize * QuoteLineRatio,
            maxBottom: quoteMaxBottom);

        gfx.DrawString(SalesCopyBuilder.BuildPullQuoteSignature(_request),
            new XFont(SheetFonts.SansMediumFamily, 8.5), new XSolidBrush(_theme.Accent),
            new XRect(sideX, quoteBottom + 8, sideWidth, 11),
            XStringFormats.TopLeft);
    }

    // ── Copy columns: the factual ABOUT text and the sales paragraph ──────

    private void DrawCopyColumns(XGraphics gfx)
    {
        var columnWidth = (SheetTheme.ContentWidth - SideGutter) / 2;
        var leftX = SheetTheme.MarginX;
        var rightX = SheetTheme.MarginX + columnWidth + SideGutter;

        DrawCopyColumn(gfx, leftX, columnWidth, "ABOUT THIS MODEL", _request.Description);
        DrawCopyColumn(gfx, rightX, columnWidth, "WHY YOU WANT IT",
            SalesCopyBuilder.BuildSalesParagraph(_request));
    }

    private void DrawCopyColumn(XGraphics gfx, double x, double width, string kicker, string body)
    {
        gfx.DrawString(SheetTheme.Letterspace(kicker),
            new XFont(SheetFonts.SansMediumFamily, 8), new XSolidBrush(_theme.Accent),
            new XRect(x, CopyTop, width, 11), XStringFormats.TopLeft);

        DrawWrapped(gfx, body,
            new XFont(SheetFonts.SansFamily, 8.4), new XSolidBrush(SheetTheme.Secondary),
            x, CopyTop + 16, width, lineHeight: 12.0, maxBottom: CopyBottom);
    }

    // ── Tags ──────────────────────────────────────────────────────────────

    private void DrawTags(XGraphics gfx)
    {
        if (_request.Tags.Count == 0) { return; }

        //Poly Haven tag lists sometimes repeat a tag; the sheet shows each once.
        var tags = _request.Tags.Distinct(StringComparer.OrdinalIgnoreCase);
        var tagsLine = string.Join("   ", tags.Select(t => "#" + t));
        var tagsFont = new XFont(SheetFonts.SansMediumFamily, 7.5);

        //Single line only: drop trailing tags that would overflow the content width.
        while (tagsLine.Length > 0 && gfx.MeasureString(tagsLine, tagsFont).Width > SheetTheme.ContentWidth)
        {
            var lastBreak = tagsLine.LastIndexOf("   ", StringComparison.Ordinal);
            if (lastBreak <= 0) { return; }
            tagsLine = tagsLine[..lastBreak];
        }

        gfx.DrawString(tagsLine, tagsFont, new XSolidBrush(_theme.AccentSoft),
            new XRect(SheetTheme.MarginX, TagsY, SheetTheme.ContentWidth, 10),
            XStringFormats.TopLeft);
    }

    // ── Gallery: the four angle shots with captions ───────────────────────

    private void DrawGallery(XGraphics gfx)
    {
        if (_request.GalleryShots.Count == 0) { return; }

        const double gap = 12;
        var count = Math.Min(_request.GalleryShots.Count, 4);
        var cellWidth = (SheetTheme.ContentWidth - gap * (count - 1)) / count;
        var captionFont = new XFont(SheetFonts.SansMediumFamily, 6.3);
        var captionBrush = new XSolidBrush(SheetTheme.Tertiary);

        for (var i = 0; i < count; i++)
        {
            var shot = _request.GalleryShots[i];
            var x = SheetTheme.MarginX + i * (cellWidth + gap);
            var rect = new XRect(x, GalleryTop, cellWidth, GalleryHeight);
            DrawCoverImage(gfx, shot.ImageBytes, rect, InsetCornerRadius);

            gfx.DrawString(SheetTheme.Letterspace(shot.Caption.ToUpperInvariant()),
                captionFont, captionBrush,
                new XRect(x, GalleryTop + GalleryHeight + 6, cellWidth, 9),
                XStringFormats.TopCenter);
        }
    }

    // ── Specs: the DETAILS facts as a compact grid ────────────────────────

    private void DrawSpecs(XGraphics gfx)
    {
        gfx.DrawLine(new XPen(SheetTheme.Hairline, 0.6),
            SheetTheme.MarginX, SpecsRuleY, SheetTheme.MarginX + SheetTheme.ContentWidth, SpecsRuleY);

        //The License fact is embodied by the footer's CC0 badge; the grid shows the rest.
        var facts = _request.Facts
            .Where(f => !string.Equals(f.Label, "License", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();
        if (facts.Count == 0) { return; }

        const int columns = 4;
        const double rowHeight = 26;
        var cellWidth = SheetTheme.ContentWidth / columns;
        var labelFont = new XFont(SheetFonts.SansMediumFamily, 6.2);
        var labelBrush = new XSolidBrush(SheetTheme.Tertiary);
        var valueBrush = new XSolidBrush(SheetTheme.Ink);

        for (var i = 0; i < facts.Count; i++)
        {
            var x = SheetTheme.MarginX + (i % columns) * cellWidth;
            var y = SpecsRuleY + 10 + (i / columns) * rowHeight;

            gfx.DrawString(SheetTheme.Letterspace(facts[i].Label.ToUpperInvariant()),
                labelFont, labelBrush, new XRect(x, y, cellWidth - 8, 8), XStringFormats.TopLeft);

            //Values shrink to their cell so a long category list cannot collide with its neighbor.
            var valueSize = 9.5;
            XFont valueFont;
            while (true)
            {
                valueFont = new XFont(SheetFonts.SansFamily, valueSize, XFontStyle.Bold);
                if (valueSize <= 6.5 || gfx.MeasureString(facts[i].Value, valueFont).Width <= cellWidth - 8)
                {
                    break;
                }
                valueSize -= 0.5;
            }
            gfx.DrawString(facts[i].Value, valueFont, valueBrush,
                new XRect(x, y + 9.5, cellWidth - 8, 12), XStringFormats.TopLeft);
        }
    }

    // ── Footer: the CC0 badge and the Poly Haven link ─────────────────────

    private void DrawFooter(XGraphics gfx)
    {
        var accentBrush = new XSolidBrush(_theme.Accent);

        var badgeFont = new XFont(SheetFonts.SansFamily, 7.5, XFontStyle.Bold);
        var badgeText = SheetTheme.Letterspace("CC0 · FREE FOREVER");
        var badgeTextWidth = gfx.MeasureString(badgeText, badgeFont).Width;
        var badgeRect = new XRect(SheetTheme.MarginX, FooterY, badgeTextWidth + 16, 14);
        gfx.DrawRoundedRectangle(accentBrush, badgeRect, new XSize(7, 7));
        gfx.DrawString(badgeText, badgeFont, new XSolidBrush(SheetTheme.Paper),
            badgeRect, XStringFormats.Center);

        if (!string.IsNullOrWhiteSpace(_request.AssetUrl))
        {
            gfx.DrawString(_request.AssetUrl,
                new XFont(SheetFonts.SansMediumFamily, 8), accentBrush,
                new XRect(SheetTheme.MarginX, FooterY, SheetTheme.ContentWidth, 11),
                XStringFormats.TopRight);
        }

        gfx.DrawString("Generated by PolyHavenBrowser · CodeBrix.Platform",
            new XFont(SheetFonts.SansFamily, 6.2), new XSolidBrush(SheetTheme.Tertiary),
            new XRect(SheetTheme.MarginX, FooterY + 13, SheetTheme.ContentWidth, 8),
            XStringFormats.TopRight);
    }

    // ── Drawing helpers ───────────────────────────────────────────────────

    //Draws an encoded image so it covers the rectangle (scaled up and center-cropped),
    //  clipped to rounded corners, with a hairline border. A null/undecodable image leaves
    //  a quiet empty well instead, so a missing shot can never break sheet creation.
    private static void DrawCoverImage(XGraphics gfx, byte[]? imageBytes, XRect rect, double cornerRadius)
    {
        var clipPath = new XGraphicsPath();
        clipPath.AddRoundedRectangle(rect.X, rect.Y, rect.Width, rect.Height, cornerRadius * 2, cornerRadius * 2);

        if (imageBytes is { Length: > 0 })
        {
            try
            {
                var image = XImage.FromStream(() => new MemoryStream(imageBytes, writable: false));

                var scale = Math.Max(rect.Width / image.PixelWidth, rect.Height / image.PixelHeight);
                var drawWidth = image.PixelWidth * scale;
                var drawHeight = image.PixelHeight * scale;
                var drawRect = new XRect(
                    rect.X + (rect.Width - drawWidth) / 2,
                    rect.Y + (rect.Height - drawHeight) / 2,
                    drawWidth, drawHeight);

                var state = gfx.Save();
                gfx.IntersectClip(clipPath);
                gfx.DrawImage(image, drawRect);
                gfx.Restore(state);
            }
            catch (Exception)
            {
                gfx.DrawRoundedRectangle(new XSolidBrush(SheetTheme.Hairline), rect,
                    new XSize(cornerRadius * 2, cornerRadius * 2));
            }
        }
        else
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(SheetTheme.Hairline), rect,
                new XSize(cornerRadius * 2, cornerRadius * 2));
        }

        gfx.DrawRoundedRectangle(new XPen(SheetTheme.Hairline, 0.8), rect,
            new XSize(cornerRadius * 2, cornerRadius * 2));
    }

    //Draws an encoded image so it fits entirely inside the box — scaled to fit, never
    //  cropped and never stretched — centered across the box, hung from its top and clipped
    //  to rounded corners. It carries no border: the image simply sits on the page. Whatever
    //  part of the box the image does not cover is left untouched, so the page shows through
    //  there. Returns the rectangle the image really occupies (the box itself when there is
    //  nothing drawable, which leaves the same quiet empty well DrawCoverImage would), so the
    //  caller can flow the rest of its column from the true bottom edge.
    private static XRect DrawContainedImage(XGraphics gfx, byte[]? imageBytes, XRect box, double cornerRadius)
    {
        if (imageBytes is { Length: > 0 })
        {
            try
            {
                var image = XImage.FromStream(() => new MemoryStream(imageBytes, writable: false));

                var scale = Math.Min(box.Width / image.PixelWidth, box.Height / image.PixelHeight);
                var drawRect = new XRect(
                    box.X + (box.Width - image.PixelWidth * scale) / 2,
                    box.Y,
                    image.PixelWidth * scale, image.PixelHeight * scale);

                var clipPath = new XGraphicsPath();
                clipPath.AddRoundedRectangle(
                    drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height,
                    cornerRadius * 2, cornerRadius * 2);

                var state = gfx.Save();
                gfx.IntersectClip(clipPath);
                gfx.DrawImage(image, drawRect);
                gfx.Restore(state);

                //The hairline border that used to trace the image reads as a distraction beside
                //  the borderless hero shot, so the preview simply sits on the page:
                //gfx.DrawRoundedRectangle(new XPen(SheetTheme.Hairline, 0.8), drawRect,
                //    new XSize(cornerRadius * 2, cornerRadius * 2));
                return drawRect;
            }
            catch (Exception)
            {
                //An undecodable image falls through to the empty well below.
            }
        }

        gfx.DrawRoundedRectangle(new XSolidBrush(SheetTheme.Hairline), box,
            new XSize(cornerRadius * 2, cornerRadius * 2));
        gfx.DrawRoundedRectangle(new XPen(SheetTheme.Hairline, 0.8), box,
            new XSize(cornerRadius * 2, cornerRadius * 2));
        return box;
    }

    //Greedily word-wraps text to a column width. Shared so that measuring a block (to pick a
    //  type size that fits) and drawing it can never disagree about where the lines break.
    private static List<string> WrapLines(XGraphics gfx, string text, XFont font, double width)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) { return lines; }

        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (gfx.MeasureString(candidate, font).Width <= width)
            {
                line = candidate;
                continue;
            }

            //A single word wider than the column still gets its own line; it simply overhangs.
            lines.Add(line.Length == 0 ? word : line);
            line = line.Length == 0 ? string.Empty : word;
        }

        if (line.Length > 0) { lines.Add(line); }
        return lines;
    }

    //Word-wraps and draws text inside a column, stopping at maxBottom; a truncated last
    //  line gets an ellipsis. Returns the y just below the last drawn line.
    private static double DrawWrapped(
        XGraphics gfx, string text, XFont font, XBrush brush,
        double x, double y, double width, double lineHeight, double maxBottom)
    {
        var lines = WrapLines(gfx, text, font, width);
        var lineY = y;

        for (var i = 0; i < lines.Count; i++)
        {
            //When there is no room for a line after this one, everything still unwritten is
            //  folded into it and truncated with an ellipsis.
            var isLastAffordable = i < lines.Count - 1 && lineY + 2 * lineHeight > maxBottom;
            var line = isLastAffordable
                ? TruncateWithEllipsis(gfx, string.Join(' ', lines.Skip(i)), font, width)
                : lines[i];

            gfx.DrawString(line, font, brush, new XRect(x, lineY, width, lineHeight), XStringFormats.TopLeft);
            lineY += lineHeight;

            if (isLastAffordable) { break; }
        }

        return lineY;
    }

    private static string TruncateWithEllipsis(XGraphics gfx, string text, XFont font, double width)
    {
        var truncated = text;
        while (truncated.Length > 1 && gfx.MeasureString(truncated + "…", font).Width > width)
        {
            var lastSpace = truncated.LastIndexOf(' ');
            truncated = lastSpace > 0 ? truncated[..lastSpace] : truncated[..^1];
        }
        return truncated + "…";
    }
}
