using CodeBrix.PdfRasterizer;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfSideBySide.PdfRender.Documents;

/// <summary>
/// A PDF file opened for page-by-page viewing: its bytes (read once), its page count, and a
/// 1-based current-page cursor that never leaves the document's page range.
/// </summary>
public sealed class PdfPageDocument
{
    private PdfPageDocument(string filePath, byte[] pdfBytes, int pageCount)
    {
        FilePath = filePath;
        PdfBytes = pdfBytes;
        PageCount = pageCount;
    }

    /// <summary>The full path of the PDF file.</summary>
    public string FilePath { get; }

    /// <summary>The file name (with extension) of the PDF file.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>The number of pages in the document (always at least 1).</summary>
    public int PageCount { get; }

    /// <summary>The 1-based page the cursor is on; starts at 1 when the document is opened.</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Whether <see cref="MovePrevious"/> would move the cursor.</summary>
    public bool CanMovePrevious => CurrentPage > 1;

    /// <summary>Whether <see cref="MoveNext"/> would move the cursor.</summary>
    public bool CanMoveNext => CurrentPage < PageCount;

    /// <summary>The raw PDF bytes, handed to the rasterizer so the file is never re-read.</summary>
    internal byte[] PdfBytes { get; }

    /// <summary>
    /// Moves the cursor to the previous page. Returns <c>false</c> (and stays put) when the cursor
    /// is already on the first page.
    /// </summary>
    public bool MovePrevious()
    {
        if (!CanMovePrevious) { return false; }
        CurrentPage--;
        return true;
    }

    /// <summary>
    /// Moves the cursor to the next page. Returns <c>false</c> (and stays put) when the cursor
    /// is already on the last page.
    /// </summary>
    public bool MoveNext()
    {
        if (!CanMoveNext) { return false; }
        CurrentPage++;
        return true;
    }

    /// <summary>Moves the cursor to the 1-based pageNumber.</summary>
    /// <exception cref="ArgumentOutOfRangeException">pageNumber is not between 1 and <see cref="PageCount"/>.</exception>
    public void GoToPage(int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageNumber, PageCount);
        CurrentPage = pageNumber;
    }

    /// <summary>
    /// Opens the PDF at filePath: reads its bytes and asks the rasterizer for its page count.
    /// </summary>
    /// <exception cref="FileNotFoundException">No file exists at filePath.</exception>
    /// <exception cref="InvalidDataException">The file is not a PDF the rasterizer can read, or has no pages.</exception>
    public static async Task<PdfPageDocument> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = DocumentPath.Normalize(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The PDF file was not found.", fullPath);
        }

        var pdfBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        int pageCount;
        try
        {
            using var rasterizer = new PageRasterizer();
            pageCount = await rasterizer.GetPageCount(pdfBytes, cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new InvalidDataException(
                $"“{Path.GetFileName(fullPath)}” could not be read as a PDF document.", e);
        }

        if (pageCount < 1)
        {
            throw new InvalidDataException($"“{Path.GetFileName(fullPath)}” has no pages.");
        }

        return new PdfPageDocument(fullPath, pdfBytes, pageCount);
    }
}
