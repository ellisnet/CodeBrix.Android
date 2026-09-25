using System;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// A rectangle in device-independent pixels (DIPs).
/// </summary>
internal readonly record struct DipRect(double X, double Y, double Width, double Height);

/// <summary>
/// Computes a window's WinUI Bounds and VisibleBounds (in DIPs) from the Android window
/// size and the system-bar and display-cutout insets (in physical pixels). The content is
/// always laid out edge to edge, so the Bounds are the whole window and the VisibleBounds
/// are the Bounds minus the insets.
/// </summary>
internal static class WindowBoundsCalculator
{
    /// <summary>Returns (bounds, visibleBounds) in DIPs.</summary>
    /// <param name="widthPx">Window width in physical pixels.</param>
    /// <param name="heightPx">Window height in physical pixels.</param>
    /// <param name="insetLeftPx">Left inset in physical pixels.</param>
    /// <param name="insetTopPx">Top inset in physical pixels.</param>
    /// <param name="insetRightPx">Right inset in physical pixels.</param>
    /// <param name="insetBottomPx">Bottom inset in physical pixels.</param>
    /// <param name="density">Physical pixels per DIP (DisplayMetrics.Density).</param>
    internal static (DipRect Bounds, DipRect VisibleBounds) Compute(
        int widthPx, int heightPx,
        int insetLeftPx, int insetTopPx, int insetRightPx, int insetBottomPx,
        double density)
    {
        if (density <= 0 || double.IsNaN(density))
        {
            throw new ArgumentOutOfRangeException(nameof(density), "The density must be positive.");
        }

        var width = Math.Max(0, widthPx) / density;
        var height = Math.Max(0, heightPx) / density;
        var left = Math.Max(0, insetLeftPx) / density;
        var top = Math.Max(0, insetTopPx) / density;
        var right = Math.Max(0, insetRightPx) / density;
        var bottom = Math.Max(0, insetBottomPx) / density;

        var visibleWidth = Math.Max(0, width - left - right);
        var visibleHeight = Math.Max(0, height - top - bottom);
        return (new DipRect(0, 0, width, height), new DipRect(left, top, visibleWidth, visibleHeight));
    }
}
