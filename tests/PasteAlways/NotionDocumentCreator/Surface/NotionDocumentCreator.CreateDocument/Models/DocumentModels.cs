using System;
using System.Collections.Generic;

namespace NotionDocumentCreator.CreateDocument.Models;

/// <summary>
/// Everything needed to render a selection of Notion pages to a book-style PDF.
/// </summary>
public sealed class CreateRequest
{
    /// <summary>
    /// The IDs of the Notion pages to include, already ordered top-to-bottom in
    /// depth-first tree order. Each page becomes its own chapter; the first page
    /// becomes the cover/title page.
    /// </summary>
    public IReadOnlyList<string> PageIds { get; init; } = [];

    /// <summary>
    /// The full path (folder + file name) the finished PDF is saved to; the
    /// containing folder is created if it does not exist.
    /// </summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>The page (trim) size for the book.</summary>
    public PageSizeOption PageSize { get; init; } = PageSizeOption.EightByTen;

    /// <summary>When false, images are skipped entirely (text-only rendering).</summary>
    public bool IncludeImages { get; init; } = true;

    /// <summary>
    /// When false, non-image media (video poster frames, audio/file cards' downloads)
    /// are not fetched; those blocks render as cards from metadata alone.
    /// </summary>
    public bool IncludeMedia { get; init; } = true;
}

/// <summary>
/// The stages a document creation moves through, in order (useful for progress display).
/// </summary>
public enum CreateStage
{
    /// <summary>Reading the selected pages' block trees from the Notion API.</summary>
    FetchingPages = 0,

    /// <summary>Downloading images and other referenced media files.</summary>
    DownloadingMedia,

    /// <summary>Laying the content out as a book.</summary>
    ComposingBook,

    /// <summary>Rendering and saving the PDF file.</summary>
    SavingPdf,

    /// <summary>The document has been created.</summary>
    Done
}

/// <summary>
/// A progress report raised while creating a document.
/// </summary>
public sealed record CreateProgress(CreateStage Stage, string Message, int PercentComplete);

/// <summary>
/// The result of a successfully created document.
/// </summary>
public sealed class CreatedDocument
{
    /// <summary>The full path of the PDF that was written.</summary>
    public string OutputFilePath { get; init; } = "";

    /// <summary>The book title (the title of the first selected page).</summary>
    public string Title { get; init; } = "";

    /// <summary>Number of pages in the finished PDF.</summary>
    public int PageCount { get; init; }

    /// <summary>Number of chapters (selected Notion pages) in the book.</summary>
    public int ChapterCount { get; init; }

    /// <summary>Number of images included in the book.</summary>
    public int ImageCount { get; init; }

    /// <summary>How long the creation took.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Non-fatal notes collected during the creation.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
