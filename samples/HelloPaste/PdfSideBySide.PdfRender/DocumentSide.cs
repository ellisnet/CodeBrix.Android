namespace PdfSideBySide.PdfRender;

/// <summary>
/// Identifies one of the two documents in a <see cref="PdfComparison"/>: the left pane
/// (Document 1) or the right pane (Document 2).
/// </summary>
public enum DocumentSide
{
    /// <summary>The left pane - Document 1.</summary>
    Left,

    /// <summary>The right pane - Document 2.</summary>
    Right,
}
