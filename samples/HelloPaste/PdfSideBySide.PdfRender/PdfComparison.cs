using PdfSideBySide.PdfRender.Documents;
using PdfSideBySide.PdfRender.Viewing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfSideBySide.PdfRender;

/// <summary>
/// Two PDF documents viewed side by side, each with its own page cursor. The "both" moves
/// step the two cursors together (keeping whatever page offset is in effect); the "adjust"
/// moves step only the right document, which is how the user lines the two up when their
/// page counts differ. Every move clamps at each document's own first and last page, and
/// every move that shows a different page resets the <see cref="View"/> to fit-the-page.
/// </summary>
public sealed class PdfComparison
{
    /// <summary>The shared zoom and per-pane pan positions.</summary>
    public ComparisonView View { get; } = new();

    /// <summary>The left-pane document (Document 1), or <c>null</c> when none is open.</summary>
    public PdfPageDocument Left { get; private set; }

    /// <summary>The right-pane document (Document 2), or <c>null</c> when none is open.</summary>
    public PdfPageDocument Right { get; private set; }

    /// <summary>Whether both sides hold a document, i.e. there is something to compare.</summary>
    public bool IsReady => Left != null && Right != null;

    /// <summary>The document open on side, or <c>null</c>.</summary>
    public PdfPageDocument GetDocument(DocumentSide side) =>
        side == DocumentSide.Left ? Left : Right;

    /// <summary>
    /// Opens the PDF at filePath as side's document (replacing whatever was there, with
    /// the cursor back on page 1). The other side's document and cursor are left alone.
    /// </summary>
    /// <exception cref="DuplicateDocumentException">The file is already open on the other side.</exception>
    public async Task<PdfPageDocument> OpenAsync(DocumentSide side, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var otherSide = side == DocumentSide.Left ? DocumentSide.Right : DocumentSide.Left;
        var other = GetDocument(otherSide);
        if (other != null && DocumentPath.AreSame(other.FilePath, filePath))
        {
            throw new DuplicateDocumentException(DocumentPath.Normalize(filePath), otherSide);
        }

        var document = await PdfPageDocument.OpenAsync(filePath, cancellationToken);
        SetDocument(side, document);
        View.Reset();
        return document;
    }

    /// <summary>Closes side's document, if any.</summary>
    public void Close(DocumentSide side)
    {
        SetDocument(side, null);
        View.Reset();
    }

    #region | Moving both documents together |

    /// <summary>Whether <see cref="MoveBothPrevious"/> would move at least one cursor.</summary>
    public bool CanMoveBothPrevious => IsReady && (Left.CanMovePrevious || Right.CanMovePrevious);

    /// <summary>Whether <see cref="MoveBothNext"/> would move at least one cursor.</summary>
    public bool CanMoveBothNext => IsReady && (Left.CanMoveNext || Right.CanMoveNext);

    /// <summary>
    /// Steps both documents to their previous page; a document already on its first page
    /// stays there. Returns whether any cursor moved.
    /// </summary>
    public bool MoveBothPrevious()
    {
        if (!IsReady) { return false; }
        var movedLeft = Left.MovePrevious();
        var movedRight = Right.MovePrevious();
        return ResetViewIf(movedLeft || movedRight);
    }

    /// <summary>
    /// Steps both documents to their next page; a document already on its last page stays
    /// there. Returns whether any cursor moved.
    /// </summary>
    public bool MoveBothNext()
    {
        if (!IsReady) { return false; }
        var movedLeft = Left.MoveNext();
        var movedRight = Right.MoveNext();
        return ResetViewIf(movedLeft || movedRight);
    }

    #endregion

    #region | Adjusting the right document alone |

    /// <summary>Whether <see cref="AdjustRightPrevious"/> would move the right cursor.</summary>
    public bool CanAdjustRightPrevious => IsReady && Right.CanMovePrevious;

    /// <summary>Whether <see cref="AdjustRightNext"/> would move the right cursor.</summary>
    public bool CanAdjustRightNext => IsReady && Right.CanMoveNext;

    /// <summary>Steps only the right document to its previous page. Returns whether it moved.</summary>
    public bool AdjustRightPrevious() => ResetViewIf(IsReady && Right.MovePrevious());

    /// <summary>Steps only the right document to its next page. Returns whether it moved.</summary>
    public bool AdjustRightNext() => ResetViewIf(IsReady && Right.MoveNext());

    #endregion

    //A page change always comes back at fit-the-page, both panes centred
    private bool ResetViewIf(bool moved)
    {
        if (moved) { View.Reset(); }
        return moved;
    }

    private void SetDocument(DocumentSide side, PdfPageDocument document)
    {
        if (side == DocumentSide.Left) { Left = document; } else { Right = document; }
    }
}
