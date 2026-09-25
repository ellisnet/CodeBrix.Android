using System.Collections.Generic;

namespace WikipediaPublisher.RenderArticle.Models;

/// <summary>
/// A single run of text with uniform character formatting.
/// </summary>
public sealed record TextRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Superscript = false,
    bool Subscript = false);

/// <summary>
/// The kinds of block-level content that can appear in a parsed article.
/// </summary>
public enum ArticleBlockType
{
    Heading,
    Paragraph,
    BulletList,
    NumberedList,
    BlockQuote,
    Image,
    Table,
    DefinitionList
}

/// <summary>
/// One block-level element of a parsed article, in document order.
/// Only the members relevant to the block's <see cref="Type"/> are populated.
/// </summary>
public sealed class ArticleBlock
{
    /// <summary>The kind of block this is.</summary>
    public ArticleBlockType Type { get; init; }

    /// <summary>Heading level: 1 for article sections (HTML h2), 2 for h3, 3 for h4.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>Plain text — used by <see cref="ArticleBlockType.Heading"/> blocks.</summary>
    public string Text { get; init; } = "";

    /// <summary>Formatted text runs — used by Paragraph and BlockQuote blocks.</summary>
    public List<TextRun> Runs { get; init; }

    /// <summary>List items — used by BulletList, NumberedList and DefinitionList blocks.
    /// For definition lists, term items have <see cref="ListItem.IsTerm"/> set.</summary>
    public List<ListItem> Items { get; init; }

    /// <summary>Image details — used by Image blocks.</summary>
    public ArticleImage Image { get; init; }

    /// <summary>Table details — used by Table blocks.</summary>
    public ArticleTable Table { get; init; }
}

/// <summary>
/// One item of a bullet, numbered or definition list.
/// </summary>
public sealed class ListItem
{
    /// <summary>The formatted text of the item.</summary>
    public List<TextRun> Runs { get; init; } = [];

    /// <summary>Nesting depth (0 = top level).</summary>
    public int Depth { get; init; }

    /// <summary>For definition lists: true when this item is a term (dt) rather than a definition (dd).</summary>
    public bool IsTerm { get; init; }
}

/// <summary>
/// An image referenced by the article, together with everything needed to fetch and place it.
/// </summary>
public sealed class ArticleImage
{
    /// <summary>The image URL as found in the page (usually a thumbnail rendition).</summary>
    public string ThumbUrl { get; set; } = "";

    /// <summary>A higher-resolution rendition URL suitable for print, when one could be derived.</summary>
    public string PrintUrl { get; set; } = "";

    /// <summary>The caption text (may be empty).</summary>
    public string Caption { get; set; } = "";

    /// <summary>The Wikimedia "File:" page title for this media (e.g. "File:Xerxes Cuneiform Van.JPG"),
    /// used to look up authorship and licensing. May be empty when it could not be derived.</summary>
    public string MediaPageTitle { get; set; } = "";

    /// <summary>A short, single-line credit (author and/or license) shown under the image in print.
    /// Populated from the media file's Wikimedia metadata; empty when none is available.</summary>
    public string Attribution { get; set; } = "";

    /// <summary>The bare media file name (used for logging and de-duplication).</summary>
    public string FileName { get; set; } = "";

    /// <summary>Pixel width of the full media file when known (from data-file-width).</summary>
    public int FileWidth { get; set; }

    /// <summary>Pixel height of the full media file when known (from data-file-height).</summary>
    public int FileHeight { get; set; }

    /// <summary>The downloaded, print-processed image bytes (populated by the image pipeline).</summary>
    public byte[] ProcessedBytes { get; set; }

    /// <summary>Pixel width of <see cref="ProcessedBytes"/>.</summary>
    public int ProcessedWidth { get; set; }

    /// <summary>Pixel height of <see cref="ProcessedBytes"/>.</summary>
    public int ProcessedHeight { get; set; }
}

/// <summary>
/// A simple rectangular table extracted from the article.
/// </summary>
public sealed class ArticleTable
{
    /// <summary>The table caption (may be empty).</summary>
    public string Caption { get; set; } = "";

    /// <summary>Table rows in document order. The first row is treated as the header
    /// when <see cref="HasHeaderRow"/> is set.</summary>
    public List<ArticleTableRow> Rows { get; } = [];

    /// <summary>True when the first row consists of header (th) cells.</summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>The number of columns (accounting for column spans).</summary>
    public int ColumnCount { get; set; }
}

/// <summary>One row of an <see cref="ArticleTable"/>.</summary>
public sealed class ArticleTableRow
{
    /// <summary>The cells of the row, left to right.</summary>
    public List<ArticleTableCell> Cells { get; } = [];
}

/// <summary>One cell of an <see cref="ArticleTableRow"/>.</summary>
public sealed class ArticleTableCell
{
    /// <summary>The cell's text content.</summary>
    public string Text { get; set; } = "";

    /// <summary>Number of columns this cell spans (1 = no span).</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>True when the cell is a header (th) cell.</summary>
    public bool IsHeader { get; set; }
}

/// <summary>
/// The complete parsed content of one Wikipedia article, ready for composition into a book.
/// </summary>
public sealed class ParsedArticle
{
    /// <summary>The article title (from the page's main heading).</summary>
    public string Title { get; set; } = "";

    /// <summary>The article's short description, when present (used as the cover subtitle).</summary>
    public string ShortDescription { get; set; } = "";

    /// <summary>The canonical URL the article was fetched from.</summary>
    public string SourceUrl { get; set; } = "";

    /// <summary>The article's lead (hero) image, when one was found.</summary>
    public ArticleImage LeadImage { get; set; }

    /// <summary>All content blocks in document order.</summary>
    public List<ArticleBlock> Blocks { get; } = [];

    /// <summary>Non-fatal notes collected while parsing (skipped content, etc.).</summary>
    public List<string> Warnings { get; } = [];
}
