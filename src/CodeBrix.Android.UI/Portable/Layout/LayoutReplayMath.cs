using System;
using CodeBrix.Android.UI.Portable.Projection;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Portable.Layout;

/// <summary>
/// The arithmetic of the layout replay: Core lays out in DIPs and each native view is placed at
/// its element's Core rectangle in physical pixels. Rectangles are rounded in WINDOW
/// coordinates (absolute DIPs times the density, start floored and end ceiled) and only then
/// made relative to the parent view, so neighbouring views tile without gaps or overlaps and
/// nesting never accumulates rounding error.
/// </summary>
internal static class LayoutReplayMath
{
    /// <summary>
    /// The pixel rectangle of a child view inside its parent view.
    /// </summary>
    /// <param name="parentAbsoluteDips">The parent element's origin in window DIPs.</param>
    /// <param name="childRect">The child's Core rectangle relative to the parent, in DIPs.</param>
    /// <param name="density">Physical pixels per DIP of the child's XamlRoot.</param>
    /// <returns>The child rectangle relative to the parent view, in physical pixels.</returns>
    internal static PixelRect ChildPixels(Point parentAbsoluteDips, Rect childRect, double density)
    {
        if (childRect.IsEmpty || density <= 0)
        {
            return default;
        }

        var absolute = PixelRect.FromDips(parentAbsoluteDips.X + childRect.X, parentAbsoluteDips.Y + childRect.Y, childRect.Width, childRect.Height, density);
        var origin = PixelRect.FromDips(parentAbsoluteDips.X, parentAbsoluteDips.Y, 0, 0, density);
        return new PixelRect(absolute.Left - origin.Left, absolute.Top - origin.Top, absolute.Right - origin.Left, absolute.Bottom - origin.Top);
    }

    /// <summary>The window-DIP origin of a child whose parent is at <paramref name="parentAbsoluteDips"/>.</summary>
    /// <param name="parentAbsoluteDips">The parent element's origin in window DIPs.</param>
    /// <param name="childRect">The child's Core rectangle relative to the parent.</param>
    /// <returns>The child's origin in window DIPs.</returns>
    internal static Point ChildOrigin(Point parentAbsoluteDips, Rect childRect) =>
        childRect.IsEmpty ? parentAbsoluteDips : new Point(parentAbsoluteDips.X + childRect.X, parentAbsoluteDips.Y + childRect.Y);

    /// <summary>
    /// The pixel size of an element laid out at <paramref name="rect"/> whose parent is at
    /// <paramref name="parentAbsoluteDips"/> (what its view measures to).
    /// </summary>
    internal static (int Width, int Height) PixelSize(Point parentAbsoluteDips, Rect rect, double density)
    {
        var pixels = ChildPixels(parentAbsoluteDips, rect, density);
        return (pixels.Width, pixels.Height);
    }

    /// <summary>DIPs to pixels for a length (floating-point noise removed, then ceiled, as MAUI's ToPixels).</summary>
    internal static int ToPixels(double dips, double density)
    {
        if (double.IsNaN(dips) || double.IsInfinity(dips))
        {
            return 0;
        }

        return (int)Math.Ceiling(Math.Round(dips * density, 3));
    }

    /// <summary>Pixels to DIPs.</summary>
    internal static double FromPixels(double pixels, double density) => density > 0 ? pixels / density : pixels;
}
