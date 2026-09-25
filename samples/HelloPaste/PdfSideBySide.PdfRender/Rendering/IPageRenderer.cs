using System;
using System.Threading;
using System.Threading.Tasks;
using PdfSideBySide.PdfRender.Documents;

namespace PdfSideBySide.PdfRender.Rendering;

/// <summary>
/// Rasterizes pages of <see cref="PdfPageDocument"/>s to PNG. The implementation the application
/// registers is <see cref="PageRenderer"/>; a view model asks the dependency-injection container
/// for this interface so it never names the PDF engine, and a test can stand in for it.
/// </summary>
public interface IPageRenderer : IDisposable
{
    /// <summary>
    /// The rendering resolution in dots per inch used when a call names none. Setting a value
    /// below 1 restores the default; changing it discards anything cached at the old resolution.
    /// </summary>
    int Dpi { get; set; }

    /// <summary>The maximum number of rendered pages kept in the cache.</summary>
    int CacheCapacity { get; }

    /// <summary>The number of rendered pages currently cached.</summary>
    int CachedPageCount { get; }

    /// <summary>Renders the page document's cursor is on at <see cref="Dpi"/>.</summary>
    /// <param name="document">The open document to render from.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The rendered page.</returns>
    Task<RenderedPage> RenderCurrentPageAsync(PdfPageDocument document, CancellationToken cancellationToken = default);

    /// <summary>Renders the page document's cursor is on at dpi.</summary>
    /// <param name="document">The open document to render from.</param>
    /// <param name="dpi">The resolution to render at, in dots per inch.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The rendered page.</returns>
    Task<RenderedPage> RenderCurrentPageAsync(PdfPageDocument document, int dpi, CancellationToken cancellationToken = default);

    /// <summary>Renders the 1-based pageNumber of document at <see cref="Dpi"/>.</summary>
    /// <param name="document">The open document to render from.</param>
    /// <param name="pageNumber">The 1-based page to render.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The rendered page.</returns>
    Task<RenderedPage> RenderPageAsync(PdfPageDocument document, int pageNumber, CancellationToken cancellationToken = default);

    /// <summary>Renders the 1-based pageNumber of document at dpi.</summary>
    /// <param name="document">The open document to render from.</param>
    /// <param name="pageNumber">The 1-based page to render.</param>
    /// <param name="dpi">The resolution to render at, in dots per inch.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The rendered page.</returns>
    Task<RenderedPage> RenderPageAsync(PdfPageDocument document, int pageNumber, int dpi, CancellationToken cancellationToken = default);

    /// <summary>Forgets every cached page.</summary>
    void ClearCache();
}
