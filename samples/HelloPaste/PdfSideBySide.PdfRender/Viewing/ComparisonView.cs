namespace PdfSideBySide.PdfRender.Viewing;

/// <summary>
/// How the two panes are looking at their pages: one zoom level for both, and a pan position
/// for each (the thing being zoomed into may sit in different places on the two pages).
/// Changing page resets the whole view to fit-the-page, centred.
/// </summary>
public sealed class ComparisonView
{
    /// <summary>How much of the visible area one pan step moves: a quarter of it.</summary>
    public const double PanStepOfViewport = 0.25;

    /// <summary>The zoom level shared by both panes.</summary>
    public ViewZoom Zoom { get; } = new();

    /// <summary>Where the left pane's viewport sits over its page.</summary>
    public PanPosition LeftPan { get; } = new();

    /// <summary>Where the right pane's viewport sits over its page.</summary>
    public PanPosition RightPan { get; } = new();

    /// <summary>The pan position of side.</summary>
    public PanPosition PanOf(DocumentSide side) => side == DocumentSide.Left ? LeftPan : RightPan;

    /// <summary>
    /// How side's pane should lay its page out: a page pageWidth by pageHeight
    /// pixels shown in a viewer viewportWidth by viewportHeight pixels, at the shared
    /// zoom and this pane's pan position. <see cref="PaneLayout.None"/> when there is nothing to
    /// show yet. See <see cref="PaneLayout.Create"/> for the arithmetic.
    /// </summary>
    public PaneLayout LayoutOf(DocumentSide side, double pageWidth, double pageHeight,
        double viewportWidth, double viewportHeight) =>
        PaneLayout.Create(pageWidth, pageHeight, viewportWidth, viewportHeight, Zoom.Factor, PanOf(side));

    /// <summary>
    /// One pan step as a fraction of the scrollable range. At zoom factor <c>f</c> the page is
    /// <c>f</c> viewports wide, so the scrollable range is <c>f - 1</c> viewports and a quarter
    /// of a viewport is <c>0.25 / (f - 1)</c> of it. Zero at 100%, where nothing scrolls.
    /// </summary>
    public double PanStepFraction => Zoom.IsZoomedIn ? PanStepOfViewport / (Zoom.Factor - 1) : 0;

    /// <summary>Whether <see cref="Pan"/> would move side's viewport in direction.</summary>
    public bool CanPan(DocumentSide side, PanDirection direction) =>
        Zoom.IsZoomedIn && PanOf(side).CanMove(direction);

    /// <summary>Nudges side's viewport one step in direction. Returns whether it moved.</summary>
    public bool Pan(DocumentSide side, PanDirection direction) =>
        Zoom.IsZoomedIn && PanOf(side).Move(direction, PanStepFraction);

    /// <summary>Zooms both panes one level closer, keeping each pane's position. Returns whether the level changed.</summary>
    public bool ZoomIn() => Zoom.ZoomIn();

    /// <summary>Zooms both panes one level further out, keeping each pane's position. Returns whether the level changed.</summary>
    public bool ZoomOut()
    {
        if (!Zoom.ZoomOut()) { return false; }
        if (!Zoom.IsZoomedIn) { CentrePans(); }
        return true;
    }

    /// <summary>Back to fit-the-page, both panes centred. Returns whether anything changed.</summary>
    public bool Reset()
    {
        var zoomChanged = Zoom.Reset();
        var pansChanged = CentrePans();
        return zoomChanged || pansChanged;
    }

    private bool CentrePans()
    {
        var left = LeftPan.Reset();
        var right = RightPan.Reset();
        return left || right;
    }
}
