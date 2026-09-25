namespace PdfSideBySide.PdfRender.Viewing;

/// <summary>The four ways a zoomed-in pane's viewport can be nudged across its page.</summary>
public enum PanDirection
{
    /// <summary>Toward the top of the page.</summary>
    Up,

    /// <summary>Toward the bottom of the page.</summary>
    Down,

    /// <summary>Toward the left edge of the page.</summary>
    Left,

    /// <summary>Toward the right edge of the page.</summary>
    Right,
}
