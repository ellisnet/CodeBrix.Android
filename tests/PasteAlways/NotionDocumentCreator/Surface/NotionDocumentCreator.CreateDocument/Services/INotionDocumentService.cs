using NotionDocumentCreator.CreateDocument.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NotionDocumentCreator.CreateDocument.Services;

/// <summary>
/// The main entry point of the NotionDocumentCreator pipeline: connect to the
/// Notion API, browse the page tree below an entered page or database, and
/// publish the selected pages as a book-designed, print-ready PDF.
/// </summary>
public interface INotionDocumentService
{
    /// <summary>Validates the token and returns the bot user's name, or throws.</summary>
    Task<string> ConnectAsync(string integrationToken, CancellationToken cancellationToken = default);

    /// <summary>Loads the root node(s) for a page ID or a database/data-source ID.</summary>
    Task<IList<NotionPageNode>> LoadRootsAsync(string pageOrDatabaseId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the immediate child pages of a node (called on expand).</summary>
    Task<IList<NotionPageNode>> LoadChildrenAsync(string pageId,
        CancellationToken cancellationToken = default);

    /// <summary>A short, non-scrolling preview for the right-hand pane.</summary>
    Task<NotionPagePreview> LoadPreviewAsync(string pageId,
        CancellationToken cancellationToken = default);

    /// <summary>Renders the selected pages, in the given order, into one book PDF.</summary>
    Task<CreatedDocument> CreateDocumentAsync(CreateRequest request,
        IProgress<CreateProgress> progress = null, CancellationToken cancellationToken = default);
}
