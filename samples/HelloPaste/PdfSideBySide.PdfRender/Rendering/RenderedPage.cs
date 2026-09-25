namespace PdfSideBySide.PdfRender.Rendering;

/// <summary>
/// One rasterized PDF page: which file and page it came from, its pixel size, and the
/// PNG-encoded pixels ready to hand to an image control.
/// </summary>
/// <param name="FilePath">The full path of the source PDF.</param>
/// <param name="PageNumber">The 1-based page that was rendered.</param>
/// <param name="PixelWidth">Width of the rendered image in pixels.</param>
/// <param name="PixelHeight">Height of the rendered image in pixels.</param>
/// <param name="PngBytes">The rendered page, PNG-encoded.</param>
public sealed record RenderedPage(string FilePath, int PageNumber, int PixelWidth, int PixelHeight, byte[] PngBytes);
