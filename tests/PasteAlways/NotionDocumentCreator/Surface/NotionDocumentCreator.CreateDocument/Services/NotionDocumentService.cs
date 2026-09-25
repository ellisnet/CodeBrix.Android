// STUB (paste always): NotionDocumentCreator.CreateDocument.Services.NotionDocumentService (assembly NotionDocumentCreator.CreateDocument, CodeBrix.Samples); the real service is built on CodeBrix.NotionApi / CodeBrix.PdfDocCreate / CodeBrix.Imaging / CodeBrix.VideoProcessing, which this compile-only head does not pull in - only its public surface is needed.
using Microsoft.Extensions.Logging;
using NotionDocumentCreator.CreateDocument.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NotionDocumentCreator.CreateDocument.Services;

public sealed class NotionDocumentService : INotionDocumentService, IDisposable
{
    public NotionDocumentService(ILogger<NotionDocumentService> logger = null)
    {
    }

    public Task<string> ConnectAsync(string integrationToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Paste-always stub.");

    public Task<IList<NotionPageNode>> LoadRootsAsync(string pageOrDatabaseId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Paste-always stub.");

    public Task<IList<NotionPageNode>> LoadChildrenAsync(string pageId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Paste-always stub.");

    public Task<NotionPagePreview> LoadPreviewAsync(string pageId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Paste-always stub.");

    public Task<CreatedDocument> CreateDocumentAsync(CreateRequest request,
        IProgress<CreateProgress> progress = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Paste-always stub.");

    public void Dispose()
    {
    }
}
