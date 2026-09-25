using System;
using System.Collections.Generic;

namespace WikipediaPublisher.RenderArticle.Models;

/// <summary>
/// Everything needed to render one Wikipedia article to a book-style PDF.
/// </summary>
public sealed class RenderRequest
{
    /// <summary>The full URL of the Wikipedia article (e.g. https://en.wikipedia.org/wiki/Cuneiform).</summary>
    public string ArticleUrl { get; init; } = "";

    /// <summary>
    /// The full path (folder + file name) the finished PDF is saved to. When set, this takes
    /// precedence over <see cref="OutputDirectory"/> / <see cref="OutputFileName"/>; the
    /// containing folder is created if it does not exist.
    /// </summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>The folder the finished PDF is saved into (ignored when <see cref="OutputFilePath"/> is set).</summary>
    public string OutputDirectory { get; init; } = "";

    /// <summary>The page (trim) size for the book.</summary>
    public PageSizeOption PageSize { get; init; } = PageSizeOption.EightByTen;

    /// <summary>When false, images are skipped entirely (text-only rendering).</summary>
    public bool IncludeImages { get; init; } = true;

    /// <summary>Optional file name (without folder) for the PDF; derived from the article title when blank (ignored when <see cref="OutputFilePath"/> is set).</summary>
    public string OutputFileName { get; init; } = "";
}

/// <summary>
/// The stages a render moves through, in order (useful for progress display).
/// </summary>
public enum RenderStage
{
    FetchingArticle = 0,
    ParsingArticle,
    DownloadingImages,
    ComposingBook,
    SavingPdf,
    Done
}

/// <summary>
/// A progress report raised while rendering.
/// </summary>
public sealed record RenderProgress(RenderStage Stage, string Message, int PercentComplete);

/// <summary>
/// The result of a successful render.
/// </summary>
public sealed class RenderedArticle
{
    /// <summary>The full path of the PDF that was written.</summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>The article title.</summary>
    public string Title { get; init; } = "";

    /// <summary>Number of pages in the finished PDF.</summary>
    public int PageCount { get; init; }

    /// <summary>Number of images included in the book.</summary>
    public int ImageCount { get; init; }

    /// <summary>How long the render took.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Non-fatal notes collected during the render.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// One result row from a Wikipedia article search.
/// </summary>
public sealed class ArticleSearchResult
{
    /// <summary>The article title.</summary>
    public string Title { get; init; } = "";

    /// <summary>A short plain-text extract of the matching content.</summary>
    public string Snippet { get; init; } = "";

    /// <summary>The full article URL.</summary>
    public string ArticleUrl { get; init; } = "";
}
