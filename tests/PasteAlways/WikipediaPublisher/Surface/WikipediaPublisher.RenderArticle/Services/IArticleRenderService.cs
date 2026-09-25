using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Services;

/// <summary>
/// The main entry point of the WikipediaPublisher rendering pipeline: search for
/// Wikipedia articles, and publish an article as a book-designed, print-ready PDF.
/// </summary>
public interface IArticleRenderService
{
    /// <summary>
    /// Searches Wikipedia for articles matching the given search terms.
    /// </summary>
    Task<IList<ArticleSearchResult>> SearchArticlesAsync(
        string searchTerms,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the article named by <see cref="RenderRequest.ArticleUrl"/>, lays it
    /// out as a book at the requested page size, and saves the PDF into
    /// <see cref="RenderRequest.OutputDirectory"/>.
    /// </summary>
    Task<RenderedArticle> RenderArticleAsync(
        RenderRequest request,
        IProgress<RenderProgress> progress = null,
        CancellationToken cancellationToken = default);
}
