using System;

namespace CodeBrix.Android.UI.Portable.Projection;

/// <summary>An integer rectangle in physical pixels (left/top inclusive, right/bottom exclusive).</summary>
/// <param name="Left">Left edge.</param>
/// <param name="Top">Top edge.</param>
/// <param name="Right">Right edge.</param>
/// <param name="Bottom">Bottom edge.</param>
internal readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>A rectangle that clips nothing.</summary>
    internal static readonly PixelRect Unbounded = new(int.MinValue / 2, int.MinValue / 2, int.MaxValue / 2, int.MaxValue / 2);

    /// <summary>The width (0 when empty).</summary>
    internal int Width => Math.Max(0, Right - Left);

    /// <summary>The height (0 when empty).</summary>
    internal int Height => Math.Max(0, Bottom - Top);

    /// <summary>True when the rectangle has no area.</summary>
    internal bool IsEmpty => Right <= Left || Bottom <= Top;

    /// <summary>
    /// Converts a rectangle in DIPs (Core's layout units) to physical pixels: the left/top
    /// edges are floored and the right/bottom edges ceiled, so a native view always covers
    /// the whole Core box (text measured to a fractional width never wraps early).
    /// </summary>
    internal static PixelRect FromDips(double x, double y, double width, double height, double density)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(width) || double.IsNaN(height) || density <= 0)
        {
            return default;
        }

        width = Math.Max(0, width);
        height = Math.Max(0, height);
        var left = (int)Math.Floor(Round(x * density));
        var top = (int)Math.Floor(Round(y * density));
        var right = (int)Math.Ceiling(Round((x + width) * density));
        var bottom = (int)Math.Ceiling(Round((y + height) * density));
        return new PixelRect(left, top, right, bottom);
    }

    /// <summary>The intersection of two rectangles (empty when they do not overlap).</summary>
    internal PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top ? default : new PixelRect(left, top, right, bottom);
    }

    /// <summary>True when this rectangle lies completely inside <paramref name="other"/>.</summary>
    internal bool IsInside(PixelRect other) =>
        Left >= other.Left && Top >= other.Top && Right <= other.Right && Bottom <= other.Bottom;

    // Removes floating-point noise (e.g. 2.0000000001 * 1 must not ceil to 3).
    private static double Round(double value) => Math.Round(value, 3);
}
