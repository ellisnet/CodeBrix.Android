using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Portable.Drawing;

/// <summary>
/// The pixel geometry of a WinUI border (Border, Panel, ContentPresenter, Control backgrounds):
/// the outer rounded rectangle, the inner one (deflated by the per-side BorderThickness), the
/// per-corner radii of both, and the rectangle the Background fills (BackgroundSizing).
/// Radii arrays are in android.graphics.Path.addRoundRect order: top-left x,y, top-right x,y,
/// bottom-right x,y, bottom-left x,y.
/// </summary>
internal readonly record struct BorderGeometry(
    float Width,
    float Height,
    float Left,
    float Top,
    float Right,
    float Bottom,
    float[] OuterRadii,
    float[] InnerRadii,
    bool HasBorder)
{
    /// <summary>The inner rectangle's left edge (pixels).</summary>
    internal float InnerLeft => Left;

    /// <summary>The inner rectangle's top edge (pixels).</summary>
    internal float InnerTop => Top;

    /// <summary>The inner rectangle's right edge (pixels).</summary>
    internal float InnerRight => Width - Right;

    /// <summary>The inner rectangle's bottom edge (pixels).</summary>
    internal float InnerBottom => Height - Bottom;

    /// <summary>True when the outer shape has any rounded corner.</summary>
    internal bool IsRounded => Array.Exists(OuterRadii, r => r > 0);

    /// <summary>Computes the geometry of a box <paramref name="width"/> x <paramref name="height"/> pixels.</summary>
    internal static BorderGeometry Compute(float width, float height, Thickness thickness, CornerRadius radius, double density)
    {
        var left = Scale(thickness.Left, density);
        var top = Scale(thickness.Top, density);
        var right = Scale(thickness.Right, density);
        var bottom = Scale(thickness.Bottom, density);

        // Thickness never exceeds the box (WinUI clamps the same way when the box is too small).
        if (left + right > width)
        {
            var f = width / Math.Max(1e-3f, left + right);
            left *= f;
            right *= f;
        }

        if (top + bottom > height)
        {
            var f = height / Math.Max(1e-3f, top + bottom);
            top *= f;
            bottom *= f;
        }

        var tl = Scale(radius.TopLeft, density);
        var tr = Scale(radius.TopRight, density);
        var br = Scale(radius.BottomRight, density);
        var bl = Scale(radius.BottomLeft, density);

        // Radii that overlap are scaled down together (the CSS/Direct2D rule).
        var factor = 1f;
        factor = Math.Min(factor, Fit(tl + tr, width));
        factor = Math.Min(factor, Fit(bl + br, width));
        factor = Math.Min(factor, Fit(tl + bl, height));
        factor = Math.Min(factor, Fit(tr + br, height));
        tl *= factor;
        tr *= factor;
        br *= factor;
        bl *= factor;

        var outer = new[] { tl, tl, tr, tr, br, br, bl, bl };
        var inner = new[]
        {
            Math.Max(0, tl - left), Math.Max(0, tl - top),
            Math.Max(0, tr - right), Math.Max(0, tr - top),
            Math.Max(0, br - right), Math.Max(0, br - bottom),
            Math.Max(0, bl - left), Math.Max(0, bl - bottom),
        };

        var hasBorder = left > 0 || top > 0 || right > 0 || bottom > 0;
        return new BorderGeometry(width, height, left, top, right, bottom, outer, inner, hasBorder);
    }

    /// <summary>
    /// True when the Background fills only the inner rectangle (InnerBorderEdge, the default of
    /// Border and panels) rather than reaching under the border (OuterBorderEdge).
    /// </summary>
    internal static bool FillsInnerOnly(BackgroundSizing sizing) => sizing != BackgroundSizing.OuterBorderEdge;

    private static float Scale(double dips, double density) =>
        double.IsNaN(dips) || dips <= 0 ? 0 : (float)(dips * density);

    private static float Fit(float sum, float available) => sum > available && sum > 0 ? available / sum : 1f;
}
