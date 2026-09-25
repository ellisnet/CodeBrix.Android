using System;
using System.Collections.Generic;

namespace NotionDocumentCreator.CreateDocument.Models;

/// <summary>
/// The kind of Notion object a tree node represents.
/// </summary>
public enum NotionSourceKind
{
    /// <summary>An ordinary Notion page.</summary>
    Page = 0,

    /// <summary>A Notion database (its rows are pages).</summary>
    Database
}

/// <summary>
/// One node in the Notion page tree: a page (or database) that can be selected
/// for inclusion in the book. This is a data record — selection state such as
/// checked/expanded lives on the view-model wrapper, not here.
/// </summary>
public sealed class NotionPageNode
{
    /// <summary>The Notion page (or database) ID.</summary>
    public string Id { get; init; } = "";

    /// <summary>The page title (or database title).</summary>
    public string Title { get; init; } = "";

    /// <summary>Whether this node is a page or a database.</summary>
    public NotionSourceKind Kind { get; init; } = NotionSourceKind.Page;

    /// <summary>The page icon when it is an emoji; empty otherwise.</summary>
    public string IconEmoji { get; init; } = "";

    /// <summary>The page icon when it is an image file URL; empty otherwise.</summary>
    public string IconUrl { get; init; } = "";

    /// <summary>The page cover image URL; empty when the page has no cover.</summary>
    public string CoverUrl { get; init; } = "";

    /// <summary>Whether the page has child pages (or the database has rows).</summary>
    public bool HasChildren { get; init; }

    /// <summary>When the page was last edited.</summary>
    public DateTimeOffset LastEditedTime { get; init; }

    /// <summary>Nesting depth below the entered root (the root itself is depth 0).</summary>
    public int Depth { get; init; }

    /// <summary>
    /// Child page nodes, when they have been loaded (the tree loads lazily, one
    /// level at a time, so this is empty until the node is expanded or the whole
    /// tree is loaded up front).
    /// </summary>
    public IReadOnlyList<NotionPageNode> Children { get; init; } = [];
}

/// <summary>
/// A short, non-scrolling preview of one Notion page for the preview pane —
/// an identification aid, not a Notion replica.
/// </summary>
public sealed class NotionPagePreview
{
    /// <summary>The Notion page ID this preview describes.</summary>
    public string Id { get; init; } = "";

    /// <summary>The page title.</summary>
    public string Title { get; init; } = "";

    /// <summary>The page icon when it is an emoji; empty otherwise.</summary>
    public string IconEmoji { get; init; } = "";

    /// <summary>The page icon when it is an image file URL; empty otherwise.</summary>
    public string IconUrl { get; init; } = "";

    /// <summary>The page cover image URL; empty when the page has no cover.</summary>
    public string CoverUrl { get; init; } = "";

    /// <summary>When the page was last edited.</summary>
    public DateTimeOffset LastEditedTime { get; init; }

    /// <summary>Number of immediate child pages.</summary>
    public int ChildPageCount { get; init; }

    /// <summary>
    /// The plain text of the first block or two of the page, trimmed short —
    /// just enough to identify the page.
    /// </summary>
    public IReadOnlyList<string> TextSnippets { get; init; } = [];
}
