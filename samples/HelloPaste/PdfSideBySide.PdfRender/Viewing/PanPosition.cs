using System;

namespace PdfSideBySide.PdfRender.Viewing;

/// <summary>
/// Where one pane's viewport sits over its zoomed page, as fractions of the scrollable range:
/// 0 is the left/top edge, 1 the right/bottom edge, 0.5 the middle. Fractions survive zoom
/// changes unchanged, which is what keeps the view anchored on the same part of the page.
/// </summary>
public sealed class PanPosition
{
    /// <summary>The centred position on both axes.</summary>
    public const double Centre = 0.5;

    /// <summary>Horizontal position: 0 = left edge, 1 = right edge.</summary>
    public double Horizontal { get; private set; } = Centre;

    /// <summary>Vertical position: 0 = top edge, 1 = bottom edge.</summary>
    public double Vertical { get; private set; } = Centre;

    /// <summary>Whether <see cref="Move"/> in direction would change the position.</summary>
    public bool CanMove(PanDirection direction) => direction switch
    {
        PanDirection.Up => Vertical > 0,
        PanDirection.Down => Vertical < 1,
        PanDirection.Left => Horizontal > 0,
        PanDirection.Right => Horizontal < 1,
        _ => false,
    };

    /// <summary>
    /// Nudges the viewport by fraction of the scrollable range in direction, stopping at
    /// the page edge. Returns whether the position changed.
    /// </summary>
    public bool Move(PanDirection direction, double fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fraction);
        if (fraction == 0 || !CanMove(direction)) { return false; }

        switch (direction)
        {
            case PanDirection.Up: Vertical = Math.Max(0, Vertical - fraction); break;
            case PanDirection.Down: Vertical = Math.Min(1, Vertical + fraction); break;
            case PanDirection.Left: Horizontal = Math.Max(0, Horizontal - fraction); break;
            case PanDirection.Right: Horizontal = Math.Min(1, Horizontal + fraction); break;
        }
        return true;
    }

    /// <summary>Back to the middle of the page. Returns whether the position changed.</summary>
    public bool Reset()
    {
        if (Horizontal == Centre && Vertical == Centre) { return false; }
        Horizontal = Centre;
        Vertical = Centre;
        return true;
    }
}
