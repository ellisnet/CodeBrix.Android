using CodeBrix.PdfDocCreate.DocumentObjectModel;
using CodeBrix.PdfDocCreate.DocumentObjectModel.Tables;
using CodeBrix.PdfDocuments.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Composes a <see cref="ParsedArticle"/> into a book-designed MigraDoc document:
/// cover page, table of contents, running heads and folios, classic book body
/// typography (justified EB Garamond with first-line indents and a raised initial),
/// framed and numbered figures, booktabs-style tables, pull quotes, and a colophon.
/// </summary>
internal sealed class BookComposer
{
    private readonly ParsedArticle _article;
    private readonly BookTheme _theme;
    private readonly DateTime _generatedAt;

    private Section _content;
    private int _figureNumber;
    private int _tableNumber;
    private bool _previousWasBodyParagraph;
    private bool _raisedCapPlaced;

    public BookComposer(ParsedArticle article, BookTheme theme, DateTime generatedAt)
    {
        _article = article ?? throw new ArgumentNullException(nameof(article));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _generatedAt = generatedAt;
    }

    /// <summary>The number of images actually placed in the book.</summary>
    public int PlacedImageCount { get; private set; }

    public Document Compose()
    {
        BookFonts.EnsureRegistered();

        var document = new Document();
        document.Info.Title = _article.Title;
        document.Info.Subject = string.IsNullOrWhiteSpace(_article.ShortDescription)
            ? $"Wikipedia article: {_article.Title}"
            : _article.ShortDescription;
        document.Info.Author = "Wikipedia contributors";

        DefineStyles(document);

        ComposeFrontMatter(document);
        ComposeContent(document);
        ComposeColophon();

        return document;
    }

    // ── Styles ───────────────────────────────────────────────────────────

    private void DefineStyles(Document document)
    {
        var t = _theme;

        var normal = document.Styles["Normal"];
        normal.Font.Name = BookFonts.SerifFamily;
        normal.Font.Size = t.BodySize;
        normal.Font.Color = BookTheme.Ink;
        normal.ParagraphFormat.Alignment = ParagraphAlignment.Justify;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
        normal.ParagraphFormat.LineSpacing = t.Leading;
        normal.ParagraphFormat.SpaceAfter = 0;
        normal.ParagraphFormat.WidowControl = true;

        //Body paragraph that opens a section (no indent), and continuation paragraphs
        //  (classic book first-line indent, no inter-paragraph space)
        var bodyOpen = document.AddStyle("BodyOpen", "Normal");
        bodyOpen.ParagraphFormat.FirstLineIndent = 0;

        var bodyIndented = document.AddStyle("BodyIndented", "Normal");
        bodyIndented.ParagraphFormat.FirstLineIndent = Unit.FromPoint(t.BodySize * 1.55);

        var sectionRule = document.AddStyle("SectionRule", "Normal");
        sectionRule.Font.Size = 2;
        sectionRule.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        sectionRule.ParagraphFormat.Borders.Top.Width = 1.1;
        sectionRule.ParagraphFormat.Borders.Top.Color = BookTheme.Accent;
        sectionRule.ParagraphFormat.LeftIndent = Unit.FromPoint((t.TextWidth - 54) / 2);
        sectionRule.ParagraphFormat.RightIndent = Unit.FromPoint((t.TextWidth - 54) / 2);
        sectionRule.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 2.7);
        sectionRule.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 1.0);
        sectionRule.ParagraphFormat.KeepWithNext = true;

        var heading1 = document.Styles["Heading1"];
        heading1.Font.Name = BookFonts.SerifFamily;
        heading1.Font.Size = t.H1Size;
        heading1.Font.Bold = false;
        heading1.Font.Color = BookTheme.Ink;
        heading1.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        heading1.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        heading1.ParagraphFormat.SpaceBefore = 0;
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 1.25);
        heading1.ParagraphFormat.KeepWithNext = true;
        heading1.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;

        var heading2 = document.Styles["Heading2"];
        heading2.Font.Name = BookFonts.SerifFamily;
        heading2.Font.Size = t.H2Size;
        heading2.Font.Bold = true;
        heading2.Font.Color = BookTheme.Ink;
        heading2.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        heading2.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.7);
        heading2.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 0.5);
        heading2.ParagraphFormat.KeepWithNext = true;
        heading2.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;

        var heading3 = document.Styles["Heading3"];
        heading3.Font.Name = BookFonts.SerifFamily;
        heading3.Font.Size = t.H3Size;
        heading3.Font.Bold = true;
        heading3.Font.Italic = true;
        heading3.Font.Color = BookTheme.Ink;
        heading3.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        heading3.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        heading3.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.3);
        heading3.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 0.35);
        heading3.ParagraphFormat.KeepWithNext = true;
        heading3.ParagraphFormat.OutlineLevel = OutlineLevel.Level3;

        var listItem = document.AddStyle("ListItem", "Normal");
        listItem.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        listItem.ParagraphFormat.LeftIndent = Unit.FromPoint(t.BodySize * 1.6);
        listItem.ParagraphFormat.FirstLineIndent = Unit.FromPoint(-t.BodySize * 1.0);
        listItem.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 0.18);
        listItem.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 0.18);

        var quote = document.AddStyle("Quote", "Normal");
        quote.Font.Italic = true;
        quote.Font.Size = t.QuoteSize;
        quote.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        quote.ParagraphFormat.LeftIndent = Unit.FromPoint(t.TextWidth * 0.085);
        quote.ParagraphFormat.RightIndent = Unit.FromPoint(t.TextWidth * 0.085);
        quote.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.15);
        quote.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 1.15);
        quote.ParagraphFormat.Borders.Top.Width = 0.5;
        quote.ParagraphFormat.Borders.Top.Color = BookTheme.Hairline;
        quote.ParagraphFormat.Borders.Bottom.Width = 0.5;
        quote.ParagraphFormat.Borders.Bottom.Color = BookTheme.Hairline;
        quote.ParagraphFormat.Borders.DistanceFromTop = 6;
        quote.ParagraphFormat.Borders.DistanceFromBottom = 6;

        var figure = document.AddStyle("Figure", "Normal");
        figure.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        figure.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        figure.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.25);
        figure.ParagraphFormat.SpaceAfter = 0;
        figure.ParagraphFormat.KeepWithNext = true;

        var caption = document.AddStyle("Caption", "Normal");
        caption.Font.Name = BookFonts.SansFamily;
        caption.Font.Size = t.CaptionSize;
        caption.Font.Color = BookTheme.Muted;
        caption.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        caption.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        caption.ParagraphFormat.LineSpacing = Unit.FromPoint(t.CaptionSize * 1.35);
        caption.ParagraphFormat.LeftIndent = Unit.FromPoint(t.TextWidth * 0.08);
        caption.ParagraphFormat.RightIndent = Unit.FromPoint(t.TextWidth * 0.08);
        caption.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.CaptionSize * 0.7);
        caption.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 1.35);

        //Image credit: a whisper-small line hugging the image, sitting directly beneath it and
        //  above the figure caption. Exactly (not AtLeast) line spacing keeps the tiny line box
        //  from reserving a full text line's worth of empty space above the credit.
        var creditSize = t.LabelSize * 0.8;
        var credit = document.AddStyle("Credit", "Caption");
        credit.Font.Size = creditSize;
        credit.Font.Italic = true;
        credit.ParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
        credit.ParagraphFormat.LineSpacing = Unit.FromPoint(creditSize * 1.12);
        credit.ParagraphFormat.SpaceBefore = Unit.FromPoint(creditSize * 0.15);
        credit.ParagraphFormat.SpaceAfter = 0;
        credit.ParagraphFormat.KeepWithNext = true;

        var tableCaption = document.AddStyle("TableCaption", "Caption");
        tableCaption.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.25);
        tableCaption.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.CaptionSize * 0.8);
        tableCaption.ParagraphFormat.KeepWithNext = true;

        var tableText = document.AddStyle("TableText", "Normal");
        tableText.Font.Name = BookFonts.SansFamily;
        tableText.Font.Size = t.TableSize;
        tableText.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        tableText.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        tableText.ParagraphFormat.LineSpacing = Unit.FromPoint(t.TableSize * 1.3);

        var tocTitle = document.AddStyle("TocTitle", "Heading1");
        tocTitle.ParagraphFormat.OutlineLevel = OutlineLevel.BodyText;

        var tocEntry = document.AddStyle("TocEntry", "Normal");
        tocEntry.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        tocEntry.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        tocEntry.ParagraphFormat.LineSpacing = Unit.FromPoint(t.Leading * 1.15);
        tocEntry.ParagraphFormat.AddTabStop(Unit.FromPoint(t.TextWidth), TabAlignment.Right, TabLeader.Dots);

        var tocEntry2 = document.AddStyle("TocEntry2", "TocEntry");
        tocEntry2.Font.Size = t.BodySize * 0.92;
        tocEntry2.Font.Italic = true;
        tocEntry2.ParagraphFormat.LeftIndent = Unit.FromPoint(t.BodySize * 1.9);

        var runningHead = document.AddStyle("RunningHead", "Normal");
        runningHead.Font.Name = BookFonts.SansFamily;
        runningHead.Font.Size = t.LabelSize;
        runningHead.Font.Color = BookTheme.Muted;
        runningHead.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        runningHead.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        runningHead.ParagraphFormat.Borders.Bottom.Width = 0.4;
        runningHead.ParagraphFormat.Borders.Bottom.Color = BookTheme.Hairline;
        runningHead.ParagraphFormat.Borders.DistanceFromBottom = 3;

        var folio = document.AddStyle("Folio", "Normal");
        folio.Font.Size = t.FolioSize;
        folio.Font.Color = BookTheme.Muted;
        folio.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        folio.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        //Cover styles
        var coverKicker = document.AddStyle("CoverKicker", "Normal");
        coverKicker.Font.Name = BookFonts.SansFamily;
        coverKicker.Font.Size = t.BodySize * 0.78;
        coverKicker.Font.Color = BookTheme.Muted;
        coverKicker.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        coverKicker.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        var coverTitle = document.AddStyle("CoverTitle", "Normal");
        coverTitle.Font.Size = t.CoverTitleSize;
        coverTitle.Font.Color = BookTheme.Ink;
        coverTitle.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        coverTitle.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        coverTitle.ParagraphFormat.LineSpacing = Unit.FromPoint(t.CoverTitleSize * 1.08);

        var coverSubtitle = document.AddStyle("CoverSubtitle", "Normal");
        coverSubtitle.Font.Italic = true;
        coverSubtitle.Font.Size = t.BodySize * 1.33;
        coverSubtitle.Font.Color = BookTheme.Muted;
        coverSubtitle.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        coverSubtitle.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        coverSubtitle.ParagraphFormat.LineSpacing = Unit.FromPoint(t.BodySize * 1.7);

        var coverRule = document.AddStyle("CoverRule", "SectionRule");
        coverRule.ParagraphFormat.SpaceBefore = Unit.FromPoint(t.BodySize * 1.5);
        coverRule.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 1.5);
        coverRule.ParagraphFormat.KeepWithNext = false;

        var colophonText = document.AddStyle("ColophonText", "Normal");
        colophonText.Font.Size = t.BodySize * 0.86;
        colophonText.Font.Color = BookTheme.Muted;
        colophonText.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        colophonText.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        colophonText.ParagraphFormat.LineSpacing = Unit.FromPoint(t.BodySize * 1.32);
        colophonText.ParagraphFormat.SpaceAfter = Unit.FromPoint(t.BodySize * 0.85);
        colophonText.ParagraphFormat.LeftIndent = Unit.FromPoint(t.TextWidth * 0.1);
        colophonText.ParagraphFormat.RightIndent = Unit.FromPoint(t.TextWidth * 0.1);
    }

    // ── Front matter ─────────────────────────────────────────────────────

    private void ComposeFrontMatter(Document document)
    {
        var t = _theme;

        var cover = document.AddSection();
        cover.PageSetup.PageWidth = Unit.FromPoint(t.PageWidth);
        cover.PageSetup.PageHeight = Unit.FromPoint(t.PageHeight);
        cover.PageSetup.TopMargin = Unit.FromPoint(t.PageHeight * 0.12);
        cover.PageSetup.BottomMargin = Unit.FromPoint(t.BottomMargin);
        cover.PageSetup.LeftMargin = Unit.FromPoint(t.OuterMargin + 6);
        cover.PageSetup.RightMargin = Unit.FromPoint(t.OuterMargin + 6);

        cover.AddParagraph(BookTheme.Letterspace("Wikipedia"), "CoverKicker");
        cover.AddParagraph("", "CoverRule");
        cover.AddParagraph(_article.Title, "CoverTitle");

        if (!string.IsNullOrWhiteSpace(_article.ShortDescription))
        {
            var subtitle = cover.AddParagraph(SentenceCase(_article.ShortDescription), "CoverSubtitle");
            subtitle.Format.SpaceBefore = Unit.FromPoint(t.BodySize * 0.9);
        }

        cover.AddParagraph("", "CoverRule");

        var hero = _article.LeadImage;
        if (hero?.ProcessedBytes is not null && hero.ProcessedWidth > 0 && hero.ProcessedHeight > 0)
        {
            var heroParagraph = cover.AddParagraph("", "Figure");
            heroParagraph.Format.SpaceBefore = Unit.FromPoint(t.BodySize * 1.6);
            heroParagraph.Format.KeepWithNext = false;

            var coverTextWidth = t.PageWidth - 2 * (t.OuterMargin + 6);
            var width = coverTextWidth * 0.74;
            var aspect = (double)hero.ProcessedWidth / hero.ProcessedHeight;
            var maxHeight = t.PageHeight * 0.40;
            if (width / aspect > maxHeight)
            {
                width = maxHeight * aspect;
            }

            var image = heroParagraph.AddImage(CreateImageSource(hero));
            image.LockAspectRatio = true;
            image.Width = Unit.FromPoint(width);
            ApplyKeyline(image, hero);
            PlacedImageCount++;

            if (!string.IsNullOrWhiteSpace(hero.Attribution))
            {
                var heroCredit = cover.AddParagraph(hero.Attribution, "Credit");
                heroCredit.Format.KeepWithNext = false;
            }
        }

        var imprint = cover.AddParagraph(
            BookTheme.Letterspace("From Wikipedia, the free encyclopedia"), "CoverKicker");
        imprint.Format.SpaceBefore = Unit.FromPoint(t.PageHeight * 0.055);

        var dateLine = cover.AddParagraph(_generatedAt.ToString("MMMM yyyy"), "CoverKicker");
        dateLine.Format.SpaceBefore = Unit.FromPoint(t.BodySize * 0.5);

        ComposeToc(cover);
    }

    private void ComposeToc(Section frontMatter)
    {
        var headings = _article.Blocks
            .Where(b => b.Type == ArticleBlockType.Heading && b.HeadingLevel <= 2)
            .ToList();
        var level1Count = headings.Count(h => h.HeadingLevel == 1);
        if (level1Count < 2) { return; }

        //Only include sub-headings when the contents stay comfortably on one page
        var includeLevel2 = headings.Count <= 24;

        frontMatter.AddPageBreak();
        var tocTitle = frontMatter.AddParagraph("Contents", "TocTitle");
        tocTitle.Format.SpaceBefore = Unit.FromPoint(_theme.BodySize * 3.2);

        var rule = frontMatter.AddParagraph("", "SectionRule");
        rule.Format.SpaceBefore = Unit.FromPoint(_theme.BodySize * 0.4);
        rule.Format.SpaceAfter = Unit.FromPoint(_theme.BodySize * 1.8);

        var sectionIndex = 0;
        foreach (var heading in headings)
        {
            if (heading.HeadingLevel == 1) { sectionIndex++; }
            else if (!includeLevel2) { continue; }

            var bookmark = BookmarkNameFor(heading);
            var entry = frontMatter.AddParagraph();
            entry.Style = heading.HeadingLevel == 1 ? "TocEntry" : "TocEntry2";

            var link = entry.AddHyperlink(bookmark, HyperlinkType.Bookmark);
            link.AddText(heading.Text);
            entry.AddTab();
            entry.AddPageRefField(bookmark);
        }
    }

    // ── Content ──────────────────────────────────────────────────────────

    private void ComposeContent(Document document)
    {
        var t = _theme;

        _content = document.AddSection();
        _content.PageSetup.PageWidth = Unit.FromPoint(t.PageWidth);
        _content.PageSetup.PageHeight = Unit.FromPoint(t.PageHeight);
        _content.PageSetup.TopMargin = Unit.FromPoint(t.TopMargin);
        _content.PageSetup.BottomMargin = Unit.FromPoint(t.BottomMargin);
        _content.PageSetup.LeftMargin = Unit.FromPoint(t.InnerMargin);
        _content.PageSetup.RightMargin = Unit.FromPoint(t.OuterMargin);
        _content.PageSetup.MirrorMargins = true;
        _content.PageSetup.DifferentFirstPageHeaderFooter = true;
        _content.PageSetup.StartingNumber = 1;
        _content.PageSetup.HeaderDistance = Unit.FromPoint(t.TopMargin * 0.48);
        _content.PageSetup.FooterDistance = Unit.FromPoint(t.BottomMargin * 0.42);

        var header = _content.Headers.Primary.AddParagraph(BookTheme.Letterspace(_article.Title));
        header.Style = "RunningHead";
        header.Format.SpaceAfter = 0;

        var folio = _content.Footers.Primary.AddParagraph();
        folio.Style = "Folio";
        folio.AddPageField();

        var firstFolio = _content.Footers.FirstPage.AddParagraph();
        firstFolio.Style = "Folio";
        firstFolio.AddPageField();

        var sectionIndex = 0;
        var subIndex = 0;
        var isFirstHeading1 = true;

        foreach (var block in _article.Blocks)
        {
            switch (block.Type)
            {
                case ArticleBlockType.Heading:
                    ComposeHeading(block, ref sectionIndex, ref subIndex, ref isFirstHeading1);
                    _previousWasBodyParagraph = false;
                    break;

                case ArticleBlockType.Paragraph:
                    ComposeParagraph(block);
                    _previousWasBodyParagraph = true;
                    break;

                case ArticleBlockType.BulletList:
                case ArticleBlockType.NumberedList:
                    ComposeList(block, ordered: block.Type == ArticleBlockType.NumberedList);
                    _previousWasBodyParagraph = false;
                    break;

                case ArticleBlockType.DefinitionList:
                    ComposeDefinitionList(block);
                    _previousWasBodyParagraph = false;
                    break;

                case ArticleBlockType.BlockQuote:
                    ComposeQuote(block);
                    _previousWasBodyParagraph = false;
                    break;

                case ArticleBlockType.Image:
                    ComposeFigure(block.Image);
                    _previousWasBodyParagraph = false;
                    break;

                case ArticleBlockType.Table:
                    ComposeTable(block.Table);
                    _previousWasBodyParagraph = false;
                    break;
            }
        }
    }

    private void ComposeHeading(
        ArticleBlock block, ref int sectionIndex, ref int subIndex, ref bool isFirstHeading1)
    {
        if (block.HeadingLevel == 1)
        {
            sectionIndex++;
            subIndex = 0;

            var rule = _content.AddParagraph("", "SectionRule");
            if (isFirstHeading1)
            {
                rule.Format.SpaceBefore = Unit.FromPoint(_theme.BodySize * 1.6);
                isFirstHeading1 = false;
            }

            var heading = _content.AddParagraph(block.Text.ToUpperInvariant(), "Heading1");
            heading.AddBookmark(BookmarkNameFor(block));
        }
        else if (block.HeadingLevel == 2)
        {
            subIndex++;
            var heading = _content.AddParagraph(block.Text, "Heading2");
            heading.AddBookmark(BookmarkNameFor(block));
        }
        else
        {
            _content.AddParagraph(block.Text, "Heading3");
        }
    }

    private string BookmarkNameFor(ArticleBlock heading)
    {
        //Stable, unique bookmark names derived from block identity
        var index = _article.Blocks.IndexOf(heading);
        return $"sec.{index}";
    }

    private void ComposeParagraph(ArticleBlock block)
    {
        var paragraph = _content.AddParagraph();
        paragraph.Style = _previousWasBodyParagraph ? "BodyIndented" : "BodyOpen";

        var runs = block.Runs;

        //A raised initial marks the opening of the book (the first lead paragraph)
        if (!_raisedCapPlaced && runs.Count > 0 && runs[0].Text.Length > 0
            && char.IsLetter(runs[0].Text[0]))
        {
            _raisedCapPlaced = true;
            paragraph.Format.SpaceBefore = Unit.FromPoint(_theme.BodySize * 0.6);

            var first = runs[0];
            var initial = paragraph.AddFormattedText(first.Text[..1]);
            initial.Font.Size = _theme.RaisedCapSize;
            initial.Font.Color = BookTheme.Accent;

            AppendRuns(paragraph, [first with { Text = first.Text[1..] }, .. runs.Skip(1)]);
            return;
        }

        AppendRuns(paragraph, runs);
    }

    private void ComposeList(ArticleBlock block, bool ordered)
    {
        var t = _theme;
        var numberByDepth = new Dictionary<int, int>();

        foreach (var item in block.Items)
        {
            var paragraph = _content.AddParagraph();
            paragraph.Style = "ListItem";
            paragraph.Format.LeftIndent =
                Unit.FromPoint(t.BodySize * 1.6 + item.Depth * t.BodySize * 1.5);

            if (ordered)
            {
                numberByDepth.TryGetValue(item.Depth, out var number);
                numberByDepth[item.Depth] = ++number;
                //Reset deeper levels when a shallower item advances
                foreach (var deeper in numberByDepth.Keys.Where(d => d > item.Depth).ToList())
                {
                    numberByDepth.Remove(deeper);
                }

                var label = paragraph.AddFormattedText($"{number}.  ");
                label.Font.Color = BookTheme.Accent;
            }
            else
            {
                var bullet = paragraph.AddFormattedText(item.Depth == 0 ? "•  " : "–  ");
                bullet.Font.Color = BookTheme.Accent;
            }

            AppendRuns(paragraph, item.Runs);
        }
    }

    private void ComposeDefinitionList(ArticleBlock block)
    {
        var t = _theme;

        foreach (var item in block.Items)
        {
            var paragraph = _content.AddParagraph();
            paragraph.Style = "ListItem";

            if (item.IsTerm)
            {
                paragraph.Format.LeftIndent = Unit.FromPoint(t.BodySize * 0.4);
                paragraph.Format.FirstLineIndent = 0;
                AppendRuns(paragraph, item.Runs.Select(r => r with { Bold = true }).ToList());
            }
            else
            {
                paragraph.Format.LeftIndent = Unit.FromPoint(t.BodySize * 2.4);
                paragraph.Format.FirstLineIndent = 0;
                AppendRuns(paragraph, item.Runs);
            }
        }
    }

    private void ComposeQuote(ArticleBlock block)
    {
        var paragraph = _content.AddParagraph();
        paragraph.Style = "Quote";
        AppendRuns(paragraph, block.Runs);
    }

    private void ComposeFigure(ArticleImage articleImage)
    {
        if (articleImage?.ProcessedBytes is null
            || articleImage.ProcessedWidth <= 0 || articleImage.ProcessedHeight <= 0)
        {
            return;
        }

        var t = _theme;
        _figureNumber++;

        var aspect = (double)articleImage.ProcessedWidth / articleImage.ProcessedHeight;
        var width = aspect switch
        {
            >= 1.25 => t.TextWidth,
            >= 0.85 => t.TextWidth * 0.70,
            _ => t.TextWidth * 0.52
        };

        var maxHeight = t.TextHeight * 0.58;
        if (width / aspect > maxHeight)
        {
            width = maxHeight * aspect;
        }

        var paragraph = _content.AddParagraph("", "Figure");
        var image = paragraph.AddImage(CreateImageSource(articleImage));
        image.LockAspectRatio = true;
        image.Width = Unit.FromPoint(width);
        ApplyKeyline(image, articleImage);
        PlacedImageCount++;

        //A small credit line for the photographer/illustrator and licence, directly under the
        //  image and above the caption, when Wikimedia supplied attribution for the file
        if (!string.IsNullOrWhiteSpace(articleImage.Attribution))
        {
            _content.AddParagraph(articleImage.Attribution, "Credit");
        }

        var caption = _content.AddParagraph();
        caption.Style = "Caption";
        var label = caption.AddFormattedText($"FIG. {_figureNumber}");
        label.Font.Bold = true;
        label.Font.Size = t.LabelSize;
        label.Font.Color = BookTheme.Accent;

        if (!string.IsNullOrWhiteSpace(articleImage.Caption))
        {
            caption.AddText("   ");
            caption.AddText(articleImage.Caption);
        }
    }

    private void ComposeTable(ArticleTable articleTable)
    {
        if (articleTable is null || articleTable.Rows.Count == 0 || articleTable.ColumnCount == 0)
        {
            return;
        }

        var t = _theme;
        _tableNumber++;

        var captionParagraph = _content.AddParagraph();
        captionParagraph.Style = "TableCaption";
        var label = captionParagraph.AddFormattedText($"TABLE {_tableNumber}");
        label.Font.Bold = true;
        label.Font.Size = t.LabelSize;
        label.Font.Color = BookTheme.Accent;
        if (!string.IsNullOrWhiteSpace(articleTable.Caption))
        {
            captionParagraph.AddText("   ");
            captionParagraph.AddText(articleTable.Caption);
        }

        var table = _content.AddTable();
        table.Borders.Visible = false;
        table.TopPadding = Unit.FromPoint(3);
        table.BottomPadding = Unit.FromPoint(3);
        table.LeftPadding = Unit.FromPoint(2);
        table.RightPadding = Unit.FromPoint(4);

        var columnWidth = t.TextWidth / articleTable.ColumnCount;
        for (var c = 0; c < articleTable.ColumnCount; c++)
        {
            table.AddColumn(Unit.FromPoint(columnWidth));
        }

        for (var r = 0; r < articleTable.Rows.Count; r++)
        {
            var sourceRow = articleTable.Rows[r];
            var row = table.AddRow();

            //Booktabs styling: strong top and bottom rules, a light rule under the header,
            //  and no vertical rules at all
            if (r == 0)
            {
                row.Borders.Top.Width = 1.0;
                row.Borders.Top.Color = BookTheme.Ink;
                if (articleTable.HasHeaderRow)
                {
                    row.Borders.Bottom.Width = 0.5;
                    row.Borders.Bottom.Color = BookTheme.Ink;
                    row.HeadingFormat = true;
                }
            }
            if (r == articleTable.Rows.Count - 1)
            {
                row.Borders.Bottom.Width = 1.0;
                row.Borders.Bottom.Color = BookTheme.Ink;
            }

            var columnIndex = 0;
            foreach (var cell in sourceRow.Cells)
            {
                if (columnIndex >= articleTable.ColumnCount) { break; }

                var targetCell = row.Cells[columnIndex];
                var paragraph = targetCell.AddParagraph(cell.Text);
                paragraph.Style = "TableText";
                if (cell.IsHeader)
                {
                    paragraph.Format.Font.Bold = true;
                }

                if (cell.ColumnSpan > 1)
                {
                    targetCell.MergeRight = Math.Min(
                        cell.ColumnSpan - 1, articleTable.ColumnCount - columnIndex - 1);
                }

                columnIndex += cell.ColumnSpan;
            }
        }

        var spacer = _content.AddParagraph();
        spacer.Format.SpaceAfter = Unit.FromPoint(t.BodySize * 0.9);
        spacer.Format.LineSpacingRule = LineSpacingRule.Exactly;
        spacer.Format.LineSpacing = Unit.FromPoint(1);
    }

    // ── Colophon ─────────────────────────────────────────────────────────

    private void ComposeColophon()
    {
        var t = _theme;

        _content.AddPageBreak();

        var spacer = _content.AddParagraph();
        spacer.Format.SpaceBefore = Unit.FromPoint(t.TextHeight * 0.3);

        var rule = _content.AddParagraph("", "SectionRule");
        rule.Format.SpaceBefore = 0;

        var heading = _content.AddParagraph(BookTheme.Letterspace("About this edition"), "CoverKicker");
        heading.Format.SpaceAfter = Unit.FromPoint(t.BodySize * 1.4);

        _content.AddParagraph(
            $"This volume was generated from the Wikipedia article “{_article.Title}”, " +
            $"retrieved on {_generatedAt:MMMM d, yyyy}.",
            "ColophonText");

        _content.AddParagraph(_article.SourceUrl, "ColophonText");

        _content.AddParagraph(
            "Wikipedia text is available under the Creative Commons Attribution–ShareAlike license; " +
            "additional terms may apply. Images appear courtesy of their contributors and remain under " +
            "their respective licenses.",
            "ColophonText");

        if (_article.LeadImage?.ProcessedBytes is not null
            && (!string.IsNullOrWhiteSpace(_article.LeadImage.Caption)))
        {
            _content.AddParagraph($"Cover image: {_article.LeadImage.Caption}", "ColophonText");
        }

        _content.AddParagraph(
            "Typeset in EB Garamond and Source Sans 3, used under the SIL Open Font License.",
            "ColophonText");

        _content.AddParagraph("Produced with WikipediaPublisher · CodeBrix", "ColophonText");
    }

    // ── Shared helpers ───────────────────────────────────────────────────

    private static void AppendRuns(Paragraph paragraph, IReadOnlyList<TextRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.Text.Length == 0) { continue; }

            if (run.Text == "\n")
            {
                paragraph.AddLineBreak();
                continue;
            }

            if (!run.Bold && !run.Italic && !run.Superscript && !run.Subscript)
            {
                paragraph.AddText(run.Text);
                continue;
            }

            var formatted = paragraph.AddFormattedText(run.Text);
            formatted.Font.Bold = run.Bold;
            formatted.Font.Italic = run.Italic;
            if (run.Superscript) { formatted.Font.Superscript = true; }
            else if (run.Subscript) { formatted.Font.Subscript = true; }
        }
    }

    private static ImageSource.IImageSource CreateImageSource(ArticleImage articleImage)
    {
        var bytes = articleImage.ProcessedBytes;
        return ImageSource.FromBinary(
            $"img-{articleImage.FileName}-{Guid.NewGuid():N}",
            () => bytes,
            quality: 90);
    }

    private static void ApplyKeyline(CodeBrix.PdfDocCreate.DocumentObjectModel.Shapes.Image image, ArticleImage articleImage)
    {
        //A hairline keyline flatters photographs; diagrams and transparent
        //  graphics (PNG) read better without a frame
        if (IsJpeg(articleImage.ProcessedBytes))
        {
            image.LineFormat.Width = 0.4;
            image.LineFormat.Color = BookTheme.Hairline;
        }
    }

    private static bool IsJpeg(byte[] bytes) =>
        bytes is { Length: > 2 } && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static string SentenceCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return ""; }
        var trimmed = text.Trim();
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}
