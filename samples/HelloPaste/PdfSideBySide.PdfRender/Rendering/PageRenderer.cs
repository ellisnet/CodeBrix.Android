using PdfSideBySide.PdfRender.Documents;
using System;
using System.Threading;
using System.Threading.Tasks;

// STUB (HelloPaste on Android): the CodeBrix.Samples PageRenderer rasterizes pages with
// CodeBrix.PdfRasterizer (PDFium). Its constructor loads the native PDFium library, which this
// Android sample does not ship (see PdfSideBySide.PdfRender.csproj), so the page's view model
// could not even be created. This stub keeps the public surface of the original (same
// namespace, type, constants, constructor and members) and renders nothing; every other file
// of this library is the verbatim copy. The view model never renders before a document is
// opened, and HelloPaste cannot open one on Android yet (no file picker).

namespace PdfSideBySide.PdfRender.Rendering;

/// <summary>
/// Stub of the PDFium-backed page renderer: the same public surface, no rendering (every
/// render request fails with <see cref="NotSupportedException"/>).
/// </summary>
public sealed class PageRenderer : IPageRenderer
{
    /// <summary>The rendering resolution used when none is set: comfortable for on-screen comparison.</summary>
    public const int DefaultDpi = 150;

    /// <summary>The number of rendered pages the cache holds when none is set.</summary>
    public const int DefaultCacheCapacity = 12;

    /// <summary>Creates a renderer; cacheCapacity bounds how many rendered pages are kept.</summary>
    public PageRenderer(int cacheCapacity = DefaultCacheCapacity)
    {
        CacheCapacity = Math.Max(0, cacheCapacity);
    }

    /// <summary>The rendering resolution in dots per inch (values below 1 restore <see cref="DefaultDpi"/>).</summary>
    public int Dpi
    {
        get;
        set => field = value < 1 ? DefaultDpi : value;
    } = DefaultDpi;

    /// <summary>The maximum number of rendered pages kept in the cache.</summary>
    public int CacheCapacity { get; }

    /// <summary>The number of rendered pages currently cached (always 0: nothing is rendered).</summary>
    public int CachedPageCount => 0;

    /// <summary>Not supported in this sample on Android.</summary>
    public Task<RenderedPage> RenderCurrentPageAsync(PdfPageDocument document, CancellationToken cancellationToken = default) =>
        NotSupported();

    /// <summary>Not supported in this sample on Android.</summary>
    public Task<RenderedPage> RenderCurrentPageAsync(PdfPageDocument document, int dpi, CancellationToken cancellationToken = default) =>
        NotSupported();

    /// <summary>Not supported in this sample on Android.</summary>
    public Task<RenderedPage> RenderPageAsync(PdfPageDocument document, int pageNumber, CancellationToken cancellationToken = default) =>
        NotSupported();

    /// <summary>Not supported in this sample on Android.</summary>
    public Task<RenderedPage> RenderPageAsync(PdfPageDocument document, int pageNumber, int dpi, CancellationToken cancellationToken = default) =>
        NotSupported();

    /// <summary>Forgets every cached page (there are none).</summary>
    public void ClearCache()
    {
    }

    /// <summary>Releases nothing (the stub holds no native resources).</summary>
    public void Dispose()
    {
    }

    private static Task<RenderedPage> NotSupported() =>
        Task.FromException<RenderedPage>(new NotSupportedException("PDF page rendering (PDFium) is not available in HelloPaste on Android."));
}
