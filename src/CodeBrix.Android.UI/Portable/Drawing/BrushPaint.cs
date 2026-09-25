using System;
using System.Linq;
using CodeBrix.Android.UI.Portable.Projection;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Portable.Drawing;

/// <summary>The kind of paint a brush becomes.</summary>
internal enum BrushPaintKind
{
    /// <summary>Nothing is painted (null brush, transparent colour, unsupported brush).</summary>
    None,

    /// <summary>A solid colour (<see cref="BrushPaint.Color"/>).</summary>
    Solid,

    /// <summary>A linear gradient from <see cref="BrushPaint.Start"/> to <see cref="BrushPaint.End"/>.</summary>
    Linear,

    /// <summary>A radial gradient around <see cref="BrushPaint.Center"/> with radii <see cref="BrushPaint.RadiusX"/>/<see cref="BrushPaint.RadiusY"/>.</summary>
    Radial,
}

/// <summary>
/// A WinUI brush resolved against a box of known pixel size: solid ARGB colour or gradient
/// stops (ARGB colours and 0..1 offsets, sorted) with pixel-space geometry. The Android
/// drawables turn it into a Paint colour or Shader.
/// </summary>
internal sealed record BrushPaint(
    BrushPaintKind Kind,
    int Color,
    int[] Colors,
    float[] Offsets,
    Point Start,
    Point End,
    Point Center,
    Point GradientOrigin,
    double RadiusX,
    double RadiusY,
    GradientSpreadMethod Spread)
{
    /// <summary>The paint of no brush.</summary>
    internal static readonly BrushPaint Empty = new(BrushPaintKind.None, 0, Array.Empty<int>(), Array.Empty<float>(), default, default, default, default, 0, 0, GradientSpreadMethod.Pad);

    /// <summary>
    /// Resolves a brush for a box of <paramref name="width"/> x <paramref name="height"/>
    /// pixels. <paramref name="density"/> converts Absolute-mapping gradient points (DIPs).
    /// </summary>
    internal static BrushPaint From(Brush brush, double width, double height, double density)
    {
        switch (brush)
        {
            case null:
                return Empty;
            case SolidColorBrush solid:
                {
                    var c = solid.Color;
                    var argb = ProjectionColor.ToArgb(c.A, c.R, c.G, c.B, solid.Opacity);
                    return ProjectionColor.IsTransparent(argb) ? Empty : Empty with { Kind = BrushPaintKind.Solid, Color = argb };
                }

            case LinearGradientBrush linear:
                {
                    var (colors, offsets) = Stops(linear.GradientStops, linear.Opacity);
                    if (colors.Length == 0)
                    {
                        return Empty;
                    }

                    var relative = linear.MappingMode != BrushMappingMode.Absolute;
                    var start = Map(linear.StartPoint, relative, width, height, density);
                    var end = Map(linear.EndPoint, relative, width, height, density);
                    return Empty with { Kind = BrushPaintKind.Linear, Colors = colors, Offsets = offsets, Start = start, End = end, Spread = linear.SpreadMethod };
                }

            case RadialGradientBrush radial:
                {
                    var (colors, offsets) = Stops(radial.GradientStops, radial.Opacity);
                    if (colors.Length == 0)
                    {
                        return Empty;
                    }

                    var relative = radial.MappingMode != BrushMappingMode.Absolute;
                    var center = Map(radial.Center, relative, width, height, density);
                    var origin = Map(radial.GradientOrigin, relative, width, height, density);
                    var rx = relative ? radial.RadiusX * width : radial.RadiusX * density;
                    var ry = relative ? radial.RadiusY * height : radial.RadiusY * density;
                    return Empty with { Kind = BrushPaintKind.Radial, Colors = colors, Offsets = offsets, Center = center, GradientOrigin = origin, RadiusX = rx, RadiusY = ry, Spread = radial.SpreadMethod };
                }

            case XamlCompositionBrushBase composition:
                {
                    // AcrylicBrush and the other composition brushes: Android draws no blur, so they show their
                    // FallbackColor - what WinUI shows when transparency effects are off.
                    var c = composition.FallbackColor;
                    var argb = ProjectionColor.ToArgb(c.A, c.R, c.G, c.B, composition.Opacity);
                    return ProjectionColor.IsTransparent(argb) ? Empty : Empty with { Kind = BrushPaintKind.Solid, Color = argb };
                }

            default:
                return Empty;
        }
    }

    /// <summary>
    /// The single colour a brush shows when only one can be used (a text colour): the solid
    /// colour, or the first gradient stop; <paramref name="fallback"/> for none.
    /// </summary>
    internal static int SingleColor(Brush brush, int fallback)
    {
        var paint = From(brush, 1, 1, 1);
        return paint.Kind switch
        {
            BrushPaintKind.Solid => paint.Color,
            BrushPaintKind.Linear or BrushPaintKind.Radial => paint.Colors[0],
            _ => brush is SolidColorBrush ? 0 : fallback,
        };
    }

    private static (int[] Colors, float[] Offsets) Stops(System.Collections.Generic.IList<GradientStop> stops, double opacity)
    {
        if (stops == null || stops.Count == 0)
        {
            return (Array.Empty<int>(), Array.Empty<float>());
        }

        var sorted = stops.OrderBy(s => s.Offset).ToArray();
        var colors = new int[sorted.Length];
        var offsets = new float[sorted.Length];
        for (var i = 0; i < sorted.Length; i++)
        {
            var c = sorted[i].Color;
            colors[i] = ProjectionColor.ToArgb(c.A, c.R, c.G, c.B, opacity);
            offsets[i] = (float)Math.Clamp(sorted[i].Offset, 0, 1);
        }

        if (sorted.Length == 1)
        {
            // Android shaders need two stops; one stop paints its colour everywhere.
            return (new[] { colors[0], colors[0] }, new[] { 0f, 1f });
        }

        return (colors, offsets);
    }

    private static Point Map(Point point, bool relative, double width, double height, double density) =>
        relative ? new Point(point.X * width, point.Y * height) : new Point(point.X * density, point.Y * density);
}
