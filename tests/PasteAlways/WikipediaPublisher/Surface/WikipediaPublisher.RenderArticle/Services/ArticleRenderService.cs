using CodeBrix.PdfDocCreate.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WikipediaPublisher.RenderArticle.Internal;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Services;

/// <summary>
/// Default implementation of <see cref="IArticleRenderService"/>: fetch → parse →
/// download images → compose book → render and save PDF.
/// </summary>
public sealed class ArticleRenderService : IArticleRenderService, IDisposable
{
    private readonly ILogger<ArticleRenderService> _logger;
    private readonly WikipediaClient _client = new();
    private bool _isDisposed;

    public ArticleRenderService(ILogger<ArticleRenderService> logger = null)
    {
        _logger = logger ?? NullLogger<ArticleRenderService>.Instance;
    }

    /// <inheritdoc />
    public Task<IList<ArticleSearchResult>> SearchArticlesAsync(
        string searchTerms,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();
        return _client.SearchAsync(searchTerms, maxResults, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RenderedArticle> RenderArticleAsync(
        RenderRequest request,
        IProgress<RenderProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ArticleUrl))
        {
            throw new ArgumentException("The request must specify an article URL.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.OutputFilePath)
            && string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new ArgumentException(
                "The request must specify an output file path (or an output directory).", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Fetch
        progress?.Report(new RenderProgress(RenderStage.FetchingArticle, "Fetching the article…", 5));
        _logger.LogInformation("Fetching article: {Url}", request.ArticleUrl);
        var html = await _client.GetArticleHtmlAsync(request.ArticleUrl, cancellationToken);

        // 2. Parse
        progress?.Report(new RenderProgress(RenderStage.ParsingArticle, "Reading the article…", 12));
        var parser = new ArticleParser(request.ArticleUrl);
        var article = parser.Parse(html);
        _logger.LogInformation(
            "Parsed \"{Title}\": {Blocks} blocks, {Warnings} warnings",
            article.Title, article.Blocks.Count, article.Warnings.Count);

        if (article.Blocks.Count == 0)
        {
            throw new InvalidOperationException(
                "No readable article content was found at the given URL. " +
                "Make sure the URL points at a Wikipedia article page.");
        }

        // 3. Images
        var imageCount = 0;
        if (request.IncludeImages)
        {
            var imageTotal = article.Blocks.Count(b => b.Type == ArticleBlockType.Image)
                             + (article.LeadImage is null ? 0 : 1);
            progress?.Report(new RenderProgress(
                RenderStage.DownloadingImages, $"Downloading {imageTotal} images…", 20));

            var pipeline = new ImagePipeline(_client);
            var done = 0;
            imageCount = await pipeline.PrepareImagesAsync(
                article,
                message =>
                {
                    done++;
                    var percent = 20 + (int)(50.0 * done / Math.Max(1, imageTotal));
                    progress?.Report(new RenderProgress(RenderStage.DownloadingImages, message, percent));
                },
                cancellationToken);

            //Look up author/licence credits for the images that will be placed
            progress?.Report(new RenderProgress(
                RenderStage.DownloadingImages, "Fetching image credits…", 71));
            await new AttributionResolver(_client).ResolveAsync(article, cancellationToken);
        }
        else
        {
            article.LeadImage = null;
            article.Blocks.RemoveAll(b => b.Type == ArticleBlockType.Image);
        }

        // 4. Compose
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new RenderProgress(RenderStage.ComposingBook, "Laying out the book…", 74));
        var theme = BookTheme.For(request.PageSize);
        var composer = new BookComposer(article, theme, DateTime.Now);
        var document = composer.Compose();

        // 5. Render + save
        progress?.Report(new RenderProgress(RenderStage.SavingPdf, "Rendering the PDF…", 82));
        var renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();

        string outputPath;
        if (!string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            //A full save path was chosen (e.g. via the app's "Save PDF to" file dialog)
            outputPath = request.OutputFilePath.Trim();
            var folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        else
        {
            Directory.CreateDirectory(request.OutputDirectory);
            var fileName = string.IsNullOrWhiteSpace(request.OutputFileName)
                ? SanitizeFileName(article.Title) + ".pdf"
                : request.OutputFileName;
            outputPath = Path.Combine(request.OutputDirectory, fileName);
        }

        renderer.PdfDocument.Save(outputPath);
        stopwatch.Stop();

        var pageCount = renderer.PdfDocument.PageCount;
        _logger.LogInformation(
            "Saved {Path}: {Pages} pages, {Images} images, {Elapsed:F1}s",
            outputPath, pageCount, composer.PlacedImageCount, stopwatch.Elapsed.TotalSeconds);

        progress?.Report(new RenderProgress(RenderStage.Done, "Done.", 100));

        return new RenderedArticle
        {
            OutputFilePath = outputPath,
            Title = article.Title,
            PageCount = pageCount,
            ImageCount = composer.PlacedImageCount,
            Elapsed = stopwatch.Elapsed,
            Warnings = article.Warnings.ToList()
        };
    }

    /// <summary>Replaces characters that are invalid in file names on any supported OS.</summary>
    internal static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { return "article"; }

        var invalid = Path.GetInvalidFileNameChars()
            .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
            .Distinct()
            .ToArray();

        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "article" : cleaned;
    }

    private void CheckIsDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        if (_isDisposed) { return; }
        _isDisposed = true;
        _client.Dispose();
    }
}
