using System;
using System.Collections.Generic;

namespace PdfSideBySide.PdfRender.Viewing;

/// <summary>
/// The zoom level shared by both panes. 100% means "the whole page fits the pane" and is the
/// minimum - the user never needs to zoom out further than that; the ladder climbs to 1000%.
/// </summary>
public sealed class ViewZoom
{
    /// <summary>The fit-the-page level; also the minimum.</summary>
    public const int MinimumPercent = 100;

    /// <summary>The closest look the ladder offers.</summary>
    public const int MaximumPercent = 1000;

    /// <summary>The highest resolution a page is ever rendered at, whatever the zoom.</summary>
    public const int MaximumRenderDpi = 600;

    /// <summary>The zoom levels, in order, that <see cref="ZoomIn"/> and <see cref="ZoomOut"/> step through.</summary>
    public static IReadOnlyList<int> Levels { get; } = [100, 125, 150, 200, 300, 400, 500, 700, 1000];

    private int _index;

    /// <summary>The current level as a percentage (100 = fit the page).</summary>
    public int Percent => Levels[_index];

    /// <summary>The current level as a multiplier of the fit-the-page size (1.0 at 100%).</summary>
    public double Factor => Percent / 100.0;

    /// <summary>Whether the view is closer than fit-the-page, i.e. panning is meaningful.</summary>
    public bool IsZoomedIn => _index > 0;

    /// <summary>Whether <see cref="ZoomIn"/> would change the level.</summary>
    public bool CanZoomIn => _index < Levels.Count - 1;

    /// <summary>Whether <see cref="ZoomOut"/> would change the level.</summary>
    public bool CanZoomOut => _index > 0;

    /// <summary>Steps one level closer. Returns whether the level changed.</summary>
    public bool ZoomIn()
    {
        if (!CanZoomIn) { return false; }
        _index++;
        return true;
    }

    /// <summary>Steps one level further away. Returns whether the level changed.</summary>
    public bool ZoomOut()
    {
        if (!CanZoomOut) { return false; }
        _index--;
        return true;
    }

    /// <summary>Returns to 100%. Returns whether the level changed.</summary>
    public bool Reset()
    {
        if (_index == 0) { return false; }
        _index = 0;
        return true;
    }

    /// <summary>
    /// The resolution to render a page at for the current level: baseDpi scaled by the
    /// zoom factor so text stays sharp, capped at <see cref="MaximumRenderDpi"/> (past the cap
    /// the image is scaled up a little on screen instead of rendering an enormous bitmap).
    /// </summary>
    public int GetRenderDpi(int baseDpi)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDpi, 1);
        return (int)Math.Min(Math.Round(baseDpi * Factor), MaximumRenderDpi);
    }
}
