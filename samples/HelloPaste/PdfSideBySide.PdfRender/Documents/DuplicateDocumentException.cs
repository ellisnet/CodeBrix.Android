using System;
using System.IO;

namespace PdfSideBySide.PdfRender.Documents;

/// <summary>
/// Thrown when the PDF being opened for one side of a <see cref="PdfComparison"/> is the file
/// already open on the other side - a document cannot be compared with itself.
/// </summary>
public sealed class DuplicateDocumentException : InvalidOperationException
{
    /// <summary>Creates the exception for filePath, which is already open as alreadyOpenSide.</summary>
    public DuplicateDocumentException(string filePath, DocumentSide alreadyOpenSide)
        : base($"“{Path.GetFileName(filePath)}” is already selected as " +
               $"{DescribeSide(alreadyOpenSide)}; choose a different PDF for " +
               $"{DescribeSide(alreadyOpenSide == DocumentSide.Left ? DocumentSide.Right : DocumentSide.Left)}.")
    {
        FilePath = filePath;
        AlreadyOpenSide = alreadyOpenSide;
    }

    /// <summary>The full path of the file that was rejected.</summary>
    public string FilePath { get; }

    /// <summary>The side of the comparison that already holds the file.</summary>
    public DocumentSide AlreadyOpenSide { get; }

    private static string DescribeSide(DocumentSide side) =>
        side == DocumentSide.Left ? "Document 1" : "Document 2";
}
