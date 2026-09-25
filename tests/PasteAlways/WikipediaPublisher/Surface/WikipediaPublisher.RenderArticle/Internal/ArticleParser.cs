using CodeBrix.MarkupParse.Dom;
using CodeBrix.MarkupParse.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Parses Wikipedia article HTML into a structured <see cref="ParsedArticle"/> —
/// headings, paragraphs, lists, quotes, images, galleries and simple tables —
/// while stripping citations, edit links, navboxes and other web-only chrome.
/// </summary>
internal sealed class ArticleParser
{
    private static readonly HashSet<string> StopSections =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "References", "See also", "External links", "Notes", "Further reading",
            "Bibliography", "Sources", "Citations", "Footnotes", "Works cited",
            "Explanatory notes", "General references"
        };

    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".bmp", ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    //Divs with any of these classes are web chrome and never book content
    private static readonly string[] SkippedDivClasses =
    [
        "hatnote", "navbox", "vertical-navbox", "toc", "reflist", "refbegin",
        "sistersitebox", "side-box", "ambox", "mbox-small", "asbox", "metadata",
        "printfooter", "mw-empty-elt", "noprint", "mw-authority-control", "portalbox",
        "portal-bar", "navigation-not-searchable", "spoken-wikipedia", "sister-bar"
    ];

    private const int MinimumImagePixelWidth = 120;
    private const int TargetImagePixelWidth = 1800;
    private const int MaxTableColumns = 7;
    private const int MaxTableRows = 60;

    private readonly string _wikiHost;

    //Characters dropped because the embedded book fonts cannot render them
    [ThreadStatic] private static int _strippedGlyphCount;

    public ArticleParser(string sourceUrl)
    {
        _wikiHost = GetHost(sourceUrl);
        SourceUrl = sourceUrl ?? "";
    }

    public string SourceUrl { get; }

    public ParsedArticle Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(html));
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        _strippedGlyphCount = 0;
        var article = new ParsedArticle { SourceUrl = SourceUrl };

        article.Title = GetTitle(document);
        article.ShortDescription = document.QuerySelector("div.shortdescription")?.TextContent?.Trim() ?? "";
        article.LeadImage = GetLeadImage(document);

        //Some articles contain more than one .mw-parser-output element - e.g. a small wrapper
        //  emitted by a transcluded template (coordinates, short description, hatnotes) plus the
        //  real article body. QuerySelector would return whichever comes first in document order,
        //  which can be the near-empty wrapper. Pick the container that actually holds the prose:
        //  the one with the most paragraph descendants.
        var contentRoots = document.QuerySelectorAll(".mw-parser-output").ToList();
        var contentRoot = contentRoots
            .OrderByDescending(e => e.QuerySelectorAll("p").Count())
            .ThenByDescending(e => e.Children.Count())
            .FirstOrDefault();
        if (contentRoot is null)
        {
            article.Warnings.Add("No .mw-parser-output content container was found in the page.");
            return article;
        }
        if (contentRoots.Count > 1)
        {
            article.Warnings.Add(
                $"Page had {contentRoots.Count} .mw-parser-output containers; used the one with the most content.");
        }

        var state = new WalkState();
        WalkChildren(contentRoot, article, state);

        //De-duplicate: don't repeat the cover hero as the first figure
        if (article.LeadImage is not null)
        {
            var firstImage = article.Blocks.FirstOrDefault(b => b.Type == ArticleBlockType.Image);
            if (firstImage?.Image is not null
                && firstImage.Image.FileName.Equals(article.LeadImage.FileName, StringComparison.OrdinalIgnoreCase))
            {
                //Prefer the in-article rendition data (it has caption + file dimensions)
                if (string.IsNullOrWhiteSpace(article.LeadImage.Caption))
                {
                    article.LeadImage.Caption = firstImage.Image.Caption;
                }
                article.Blocks.Remove(firstImage);
            }
        }

        if (_strippedGlyphCount > 0)
        {
            article.Warnings.Add(
                $"Removed {_strippedGlyphCount} characters outside the book fonts' coverage " +
                "(e.g. non-Latin scripts quoted inline) to avoid unprintable glyphs.");
        }

        ReportSkips(article, state);
        return article;
    }

    private sealed class WalkState
    {
        public bool SkippingStopSection;
        public bool SeenFirstHeading;
        public readonly Dictionary<string, int> SkipCounts = new(StringComparer.OrdinalIgnoreCase);

        public void CountSkip(string what)
        {
            SkipCounts.TryGetValue(what, out var count);
            SkipCounts[what] = count + 1;
        }
    }

    private void WalkChildren(IElement container, ParsedArticle article, WalkState state)
    {
        foreach (var child in container.Children)
        {
            var tag = child.LocalName;

            //Parsoid-style output wraps each section's content in <section> elements
            if (tag == "section")
            {
                WalkChildren(child, article, state);
                continue;
            }

            //Modern MediaWiki wraps headings: <div class="mw-heading mw-heading2"><h2>…</h2></div>
            IElement headingElement = null;
            if (tag is "h2" or "h3" or "h4")
            {
                headingElement = child;
            }
            else if (tag == "div" && child.ClassList.Contains("mw-heading"))
            {
                headingElement = child.QuerySelector("h2, h3, h4");
            }

            if (headingElement is not null)
            {
                HandleHeading(headingElement, article, state);
                continue;
            }

            if (state.SkippingStopSection) { continue; }

            switch (tag)
            {
                case "p":
                    HandleParagraph(child, article, state);
                    break;

                case "ul" when child.ClassList.Contains("gallery"):
                    HandleGallery(child, article, state);
                    break;

                case "ul":
                case "ol":
                    HandleList(child, article, state, tag == "ol");
                    break;

                case "dl":
                    HandleDefinitionList(child, article);
                    break;

                case "blockquote":
                    HandleBlockQuote(child, article);
                    break;

                case "figure":
                    HandleFigure(child, article, state);
                    break;

                case "table":
                    HandleTable(child, article, state);
                    break;

                case "div":
                    HandleDiv(child, article, state);
                    break;

                case "h5":
                case "h6":
                    //Treat rare deep headings as level-3
                    HandleHeading(child, article, state);
                    break;

                case "style":
                case "link":
                case "meta":
                case "span":
                    break; //Silently ignore inline/metadata stragglers

                default:
                    state.CountSkip($"<{tag}> element");
                    break;
            }
        }
    }

    // ── Individual block handlers ────────────────────────────────────────

    private void HandleHeading(IElement heading, ParsedArticle article, WalkState state)
    {
        foreach (var editSection in heading.QuerySelectorAll(".mw-editsection").ToList())
        {
            editSection.Remove();
        }

        var text = NormalizeWhitespace(heading.TextContent);
        if (text.Length < 2) { return; }

        var level = heading.LocalName switch
        {
            "h2" => 1,
            "h3" => 2,
            _ => 3
        };

        if (level == 1)
        {
            state.SkippingStopSection = StopSections.Contains(text);
            if (state.SkippingStopSection) { return; }
        }
        else if (state.SkippingStopSection)
        {
            return;
        }

        state.SeenFirstHeading = true;
        article.Blocks.Add(new ArticleBlock
        {
            Type = ArticleBlockType.Heading,
            HeadingLevel = level,
            Text = text
        });
    }

    private void HandleParagraph(IElement paragraph, ParsedArticle article, WalkState state)
    {
        if (paragraph.ClassList.Contains("mw-empty-elt")) { return; }

        RemoveNonContent(paragraph);

        var runs = ExtractTextRuns(paragraph);
        TrimRuns(runs);
        if (runs.Count == 0) { return; }

        article.Blocks.Add(new ArticleBlock
        {
            Type = ArticleBlockType.Paragraph,
            Runs = runs
        });
    }

    private void HandleList(IElement listElement, ParsedArticle article, WalkState state, bool ordered)
    {
        var items = new List<ListItem>();
        CollectListItems(listElement, items, depth: 0);
        if (items.Count == 0) { return; }

        article.Blocks.Add(new ArticleBlock
        {
            Type = ordered ? ArticleBlockType.NumberedList : ArticleBlockType.BulletList,
            Items = items
        });
    }

    private void CollectListItems(IElement listElement, List<ListItem> items, int depth)
    {
        if (depth > 3) { return; }

        foreach (var li in listElement.Children.Where(c => c.LocalName == "li"))
        {
            RemoveNonContent(li);

            //Pull nested lists out before extracting this item's own text
            var nestedLists = li.Children
                .Where(c => c.LocalName is "ul" or "ol")
                .ToList();
            foreach (var nested in nestedLists)
            {
                nested.Remove();
            }

            var runs = ExtractTextRuns(li);
            TrimRuns(runs);
            if (runs.Count > 0)
            {
                items.Add(new ListItem { Runs = runs, Depth = depth });
            }

            foreach (var nested in nestedLists)
            {
                CollectListItems(nested, items, depth + 1);
            }
        }
    }

    private void HandleDefinitionList(IElement dlElement, ParsedArticle article)
    {
        var items = new List<ListItem>();

        foreach (var child in dlElement.Children)
        {
            if (child.LocalName is not ("dt" or "dd")) { continue; }

            RemoveNonContent(child);
            var runs = ExtractTextRuns(child);
            TrimRuns(runs);
            if (runs.Count == 0) { continue; }

            items.Add(new ListItem
            {
                Runs = runs,
                IsTerm = child.LocalName == "dt"
            });
        }

        if (items.Count == 0) { return; }

        article.Blocks.Add(new ArticleBlock
        {
            Type = ArticleBlockType.DefinitionList,
            Items = items
        });
    }

    private void HandleBlockQuote(IElement quote, ParsedArticle article)
    {
        RemoveNonContent(quote);

        var runs = ExtractTextRuns(quote, insertParagraphBreaks: true);
        TrimRuns(runs);
        if (runs.Count == 0) { return; }

        article.Blocks.Add(new ArticleBlock
        {
            Type = ArticleBlockType.BlockQuote,
            Runs = runs
        });
    }

    private void HandleFigure(IElement figure, ParsedArticle article, WalkState state)
    {
        var image = ParseImageContainer(figure,
            figure.QuerySelector("figcaption") ?? figure.QuerySelector(".thumbcaption"));

        if (image is null)
        {
            state.CountSkip("figure (unusable image)");
            return;
        }

        article.Blocks.Add(new ArticleBlock { Type = ArticleBlockType.Image, Image = image });
    }

    private void HandleGallery(IElement gallery, ParsedArticle article, WalkState state)
    {
        foreach (var box in gallery.QuerySelectorAll("li.gallerybox"))
        {
            var image = ParseImageContainer(box, box.QuerySelector(".gallerytext"));
            if (image is null)
            {
                state.CountSkip("gallery image (unusable)");
                continue;
            }

            article.Blocks.Add(new ArticleBlock { Type = ArticleBlockType.Image, Image = image });
        }
    }

    private void HandleDiv(IElement div, ParsedArticle article, WalkState state)
    {
        var classes = div.ClassList;

        if (classes.Contains("thumb") && (!classes.Contains("locmap")))
        {
            var image = ParseImageContainer(div, div.QuerySelector(".thumbcaption"));
            if (image is not null)
            {
                article.Blocks.Add(new ArticleBlock { Type = ArticleBlockType.Image, Image = image });
            }
            else
            {
                state.CountSkip("thumb div (unusable image)");
            }
            return;
        }

        if (classes.Contains("quotebox") || classes.Contains("templatequote"))
        {
            HandleBlockQuote(div, article);
            return;
        }

        if (classes.Contains("poem"))
        {
            //Poems keep their line breaks and read well with quote styling
            HandleBlockQuote(div, article);
            return;
        }

        if (classes.Contains("shortdescription")) { return; } //Captured separately

        foreach (var skipClass in SkippedDivClasses)
        {
            if (classes.Contains(skipClass)) { return; }
        }

        //Content-bearing wrapper divs (columns, centered content, etc.) — walk into them
        if (classes.Contains("div-col") || classes.Contains("center")
            || classes.Contains("mw-parser-output") || div.QuerySelector("p, h2, h3, figure") is not null)
        {
            WalkChildren(div, article, state);
            return;
        }

        state.CountSkip($"div.{div.ClassName?.Split(' ').FirstOrDefault() ?? "(unclassed)"}");
    }

    private void HandleTable(IElement table, ParsedArticle article, WalkState state)
    {
        var classes = table.ClassList;
        if (!classes.Contains("wikitable"))
        {
            state.CountSkip(classes.Contains("infobox") ? "infobox" : "non-content table");
            return;
        }

        var parsed = ParseWikiTable(table, out var skipReason);
        if (parsed is null)
        {
            state.CountSkip($"wikitable ({skipReason})");
            article.Warnings.Add($"A table was skipped: {skipReason}.");
            return;
        }

        article.Blocks.Add(new ArticleBlock { Type = ArticleBlockType.Table, Table = parsed });
    }

    private ArticleTable ParseWikiTable(IElement table, out string skipReason)
    {
        skipReason = "";

        var caption = table.QuerySelector("caption");
        var captionText = caption is null ? "" : NormalizeWhitespace(caption.TextContent);

        var result = new ArticleTable { Caption = captionText };

        foreach (var tr in table.QuerySelectorAll("tr"))
        {
            //Only rows belonging to this table (not nested tables)
            if (tr.Closest("table") != table) { continue; }

            var row = new ArticleTableRow();
            foreach (var cell in tr.Children.Where(c => c.LocalName is "td" or "th"))
            {
                if (int.TryParse(cell.GetAttribute("rowspan"), out var rowSpan) && rowSpan > 1)
                {
                    skipReason = "uses row spans";
                    return null;
                }

                RemoveNonContent(cell);

                var colSpan = 1;
                if (int.TryParse(cell.GetAttribute("colspan"), out var parsedSpan) && parsedSpan > 1)
                {
                    colSpan = Math.Min(parsedSpan, MaxTableColumns);
                }

                row.Cells.Add(new ArticleTableCell
                {
                    Text = NormalizeWhitespace(cell.TextContent),
                    ColumnSpan = colSpan,
                    IsHeader = cell.LocalName == "th"
                });
            }

            if (row.Cells.Count > 0)
            {
                result.Rows.Add(row);
            }
        }

        if (result.Rows.Count < 2)
        {
            skipReason = "not enough rows";
            return null;
        }
        if (result.Rows.Count > MaxTableRows)
        {
            skipReason = $"too long ({result.Rows.Count} rows)";
            return null;
        }

        result.ColumnCount = result.Rows.Max(r => r.Cells.Sum(c => c.ColumnSpan));
        if (result.ColumnCount > MaxTableColumns)
        {
            skipReason = $"too wide ({result.ColumnCount} columns)";
            return null;
        }

        result.HasHeaderRow = result.Rows[0].Cells.All(c => c.IsHeader);
        return result;
    }

    // ── Image parsing ────────────────────────────────────────────────────

    private ArticleImage ParseImageContainer(IElement container, IElement captionElement)
    {
        var img = container.QuerySelector("img");
        if (img is null) { return null; }

        var src = img.GetAttribute("src") ?? img.GetAttribute("data-src");
        if (string.IsNullOrWhiteSpace(src)) { return null; }

        src = NormalizeUrl(src);

        //Skip small icons
        int.TryParse(img.GetAttribute("width"), out var displayWidth);
        int.TryParse(img.GetAttribute("data-file-width"), out var fileWidth);
        int.TryParse(img.GetAttribute("data-file-height"), out var fileHeight);
        if (displayWidth > 0 && displayWidth < MinimumImagePixelWidth
            && (fileWidth == 0 || fileWidth < MinimumImagePixelWidth))
        {
            return null;
        }

        var urlPath = src.Split('?')[0];
        var extension = Path.GetExtension(urlPath);
        if (!SupportedImageExtensions.Contains(extension)) { return null; }

        string captionText = "";
        if (captionElement is not null)
        {
            captionElement.QuerySelector(".magnify")?.Remove();
            RemoveNonContent(captionElement);
            captionText = NormalizeWhitespace(captionElement.TextContent);
        }

        return new ArticleImage
        {
            ThumbUrl = src,
            PrintUrl = DerivePrintUrl(src, fileWidth, urlPath),
            Caption = captionText,
            MediaPageTitle = ExtractMediaPageTitle(container) ?? DeriveMediaTitleFromUrl(urlPath),
            FileName = Path.GetFileName(urlPath),
            FileWidth = fileWidth,
            FileHeight = fileHeight
        };
    }

    /// <summary>
    /// Finds the "File:" page title the image links to (its description page on the wiki
    /// or Commons). Modern MediaWiki wraps the thumbnail in
    /// <c>&lt;a href="/wiki/File:Name.jpg"&gt;</c>; Parsoid output uses <c>href="./File:Name.jpg"</c>.
    /// Returns null when no such link is present.
    /// </summary>
    internal static string ExtractMediaPageTitle(IElement container)
    {
        foreach (var anchor in container.QuerySelectorAll("a[href]"))
        {
            var title = MediaTitleFromHref(anchor.GetAttribute("href"));
            if (title is not null) { return title; }
        }
        return null;
    }

    internal static string MediaTitleFromHref(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) { return null; }

        //Take the portion after "/wiki/" (legacy) or a leading "./" (Parsoid)
        var wikiIndex = href.IndexOf("/wiki/", StringComparison.OrdinalIgnoreCase);
        var candidate = wikiIndex >= 0 ? href[(wikiIndex + 6)..] : href.TrimStart('.', '/');
        candidate = Uri.UnescapeDataString(candidate.Split('?', '#')[0]);

        if (candidate.StartsWith("File:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("Image:", StringComparison.OrdinalIgnoreCase))
        {
            return candidate.Replace('_', ' ').Trim();
        }
        return null;
    }

    /// <summary>
    /// Derives the "File:" page title from an upload URL when no description-page link is
    /// available (e.g. the cover hero taken from the og:image meta tag). Thumbnail URLs look
    /// like …/thumb/6/66/Name.jpg/800px-Name.jpg — the original file name is the segment
    /// before the pixel-sized rendition; full-size URLs end with the file name directly.
    /// </summary>
    internal static string DeriveMediaTitleFromUrl(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath)) { return ""; }

        var segments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) { return ""; }

        //For /thumb/…/Name.ext/NNNpx-Name.ext the original is the second-to-last segment
        var isThumb = segments.Contains("thumb", StringComparer.OrdinalIgnoreCase);
        var fileSegment = isThumb && segments.Length >= 2 ? segments[^2] : segments[^1];

        var name = Uri.UnescapeDataString(fileSegment).Replace('_', ' ').Trim();
        return name.Length == 0 ? "" : $"File:{name}";
    }

    /// <summary>
    /// Derives a print-resolution rendition URL from a Wikimedia thumbnail URL.
    /// Thumbnail URLs look like …/thumb/6/66/Name.jpg/250px-Name.jpg — the pixel
    /// prefix of the final segment selects the rendition size.
    /// </summary>
    internal static string DerivePrintUrl(string src, int fileWidth, string urlPath)
    {
        if (!src.Contains("/thumb/")) { return src; }

        var lastSlash = src.LastIndexOf('/');
        if (lastSlash < 0) { return src; }

        var lastSegment = src[(lastSlash + 1)..];
        var pxMatch = Regex.Match(lastSegment, @"^(\d+)px-");
        if (!pxMatch.Success) { return src; }

        var currentWidth = int.Parse(pxMatch.Groups[1].Value);

        //SVG renditions (….svg/NNNpx-….svg.png) can be rasterized at any size;
        //  raster files cannot be upscaled beyond their true file width.
        var isSvgRendition = urlPath.EndsWith(".svg.png", StringComparison.OrdinalIgnoreCase);
        var target = isSvgRendition
            ? TargetImagePixelWidth
            : (fileWidth > 0 ? Math.Min(TargetImagePixelWidth, fileWidth) : TargetImagePixelWidth);

        if (target <= currentWidth) { return src; }

        var newSegment = $"{target}px-{lastSegment[(pxMatch.Length)..]}";
        return src[..(lastSlash + 1)] + newSegment;
    }

    private ArticleImage GetLeadImage(IDocument document)
    {
        var ogImage = document.QuerySelector("meta[property='og:image']")?.GetAttribute("content");
        if (string.IsNullOrWhiteSpace(ogImage)) { return null; }

        var url = NormalizeUrl(ogImage);
        var urlPath = url.Split('?')[0];
        var extension = Path.GetExtension(urlPath);
        if (!SupportedImageExtensions.Contains(extension)) { return null; }

        int.TryParse(
            document.QuerySelector("meta[property='og:image:width']")?.GetAttribute("content"),
            out var width);
        int.TryParse(
            document.QuerySelector("meta[property='og:image:height']")?.GetAttribute("content"),
            out var height);

        return new ArticleImage
        {
            ThumbUrl = url,
            PrintUrl = url,
            MediaPageTitle = DeriveMediaTitleFromUrl(urlPath),
            FileName = Path.GetFileName(urlPath),
            FileWidth = width,
            FileHeight = height
        };
    }

    // ── Text-run extraction ──────────────────────────────────────────────

    internal static List<TextRun> ExtractTextRuns(
        INode node,
        bool bold = false,
        bool italic = false,
        bool superscript = false,
        bool subscript = false,
        bool insertParagraphBreaks = false)
    {
        var runs = new List<TextRun>();

        foreach (var child in node.ChildNodes)
        {
            if (child is IText textNode)
            {
                var text = CollapseWhitespace(textNode.Data);
                if (text.Length > 0)
                {
                    runs.Add(new TextRun(text, bold, italic, superscript, subscript));
                }
                continue;
            }

            if (child is not IElement element) { continue; }

            var tag = element.LocalName;

            if (tag == "br")
            {
                runs.Add(new TextRun("\n"));
                continue;
            }

            if (ShouldSkipInline(element)) { continue; }

            if (insertParagraphBreaks && tag == "p" && runs.Count > 0)
            {
                runs.Add(new TextRun("\n"));
            }

            var newBold = bold || tag is "b" or "strong";
            var newItalic = italic || tag is "i" or "em" or "dfn" or "var" or "cite";
            var newSuperscript = superscript || tag == "sup";
            var newSubscript = subscript || tag == "sub";

            runs.AddRange(ExtractTextRuns(
                element, newBold, newItalic, newSuperscript, newSubscript, insertParagraphBreaks));
        }

        return runs;
    }

    private static bool ShouldSkipInline(IElement element)
    {
        var tag = element.LocalName;

        if (tag is "style" or "script" or "figure" or "figcaption") { return true; }

        var classes = element.ClassList;

        if (tag == "sup"
            && (classes.Contains("reference") || classes.Contains("plainlinks")
                || classes.Contains("noprint") || (element.GetAttribute("typeof") ?? "").Contains("mw:Extension/ref")))
        {
            return true;
        }

        if (classes.Contains("mw-editsection") || classes.Contains("noprint")
            || classes.Contains("mw-cite-backlink") || classes.Contains("citation-needed-content"))
        {
            return true;
        }

        //Coordinates, pronunciation help and IPA spans read poorly in print
        if (tag == "span"
            && (classes.Contains("geo-inline-hidden") || classes.Contains("noexcerpt")
                || classes.Contains("mw-reflink-text")))
        {
            return true;
        }

        return false;
    }

    // ── Shared helpers ───────────────────────────────────────────────────

    /// <summary>Removes reference markers, edit links and other non-content from an element (in place).</summary>
    private static void RemoveNonContent(IElement element)
    {
        foreach (var node in element
                     .QuerySelectorAll("sup.reference, sup.plainlinks, .mw-editsection, style, script, .noprint, sup[typeof*='mw:Extension/ref']")
                     .ToList())
        {
            node.Remove();
        }
    }

    private static void TrimRuns(List<TextRun> runs)
    {
        //Drop leading/trailing whitespace-only runs and trim the edges
        while (runs.Count > 0 && string.IsNullOrWhiteSpace(runs[0].Text))
        {
            runs.RemoveAt(0);
        }
        while (runs.Count > 0 && string.IsNullOrWhiteSpace(runs[^1].Text))
        {
            runs.RemoveAt(runs.Count - 1);
        }

        if (runs.Count > 0)
        {
            runs[0] = runs[0] with { Text = runs[0].Text.TrimStart() };
            runs[^1] = runs[^1] with { Text = runs[^1].Text.TrimEnd() };
            if (runs[0].Text.Length == 0) { runs.RemoveAt(0); }
            else if (runs.Count > 1 && runs[^1].Text.Length == 0) { runs.RemoveAt(runs.Count - 1); }
        }
    }

    private static string CollapseWhitespace(string text)
    {
        var sanitized = GlyphFilter.Sanitize(text ?? "", out var removed);
        _strippedGlyphCount += removed;
        return Regex.Replace(sanitized, @"[ \t\r\n]+", " ");
    }

    private static string NormalizeWhitespace(string text) =>
        CollapseWhitespace(text).Trim();

    private string GetTitle(IDocument document)
    {
        var heading = document.QuerySelector("h1#firstHeading");
        if (heading is not null)
        {
            return NormalizeWhitespace(heading.TextContent);
        }

        var title = document.Title ?? "";
        var suffixIndex = title.LastIndexOf(" - Wikipedia", StringComparison.OrdinalIgnoreCase);
        return suffixIndex > 0 ? title[..suffixIndex].Trim() : title.Trim();
    }

    private string NormalizeUrl(string url)
    {
        if (url.StartsWith("//")) { return "https:" + url; }
        if (url.StartsWith("/")) { return $"https://{_wikiHost}{url}"; }
        return url;
    }

    private static string GetHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }
        return "en.wikipedia.org";
    }

    private static void ReportSkips(ParsedArticle article, WalkState state)
    {
        foreach (var kvp in state.SkipCounts.OrderByDescending(k => k.Value))
        {
            article.Warnings.Add($"Skipped {kvp.Value} × {kvp.Key}");
        }
    }
}
