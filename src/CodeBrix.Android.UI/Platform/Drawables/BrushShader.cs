using System;
using CodeBrix.Android.UI.Portable.Drawing;
using Microsoft.UI.Xaml.Media;
using AColor = global::Android.Graphics.Color;
using ALinearGradient = global::Android.Graphics.LinearGradient;
using AMatrix = global::Android.Graphics.Matrix;
using APaint = global::Android.Graphics.Paint;
using ARadialGradient = global::Android.Graphics.RadialGradient;
using AShader = global::Android.Graphics.Shader;
using ATileMode = global::Android.Graphics.Shader.TileMode;

namespace CodeBrix.Android.UI.Platform.Drawables;

/// <summary>Applies a <see cref="BrushPaint"/> to an Android <see cref="APaint"/> (colour or shader).</summary>
internal static class BrushShader
{
    /// <summary>
    /// Configures <paramref name="paint"/> to paint <paramref name="brush"/> over a box of
    /// <paramref name="width"/> x <paramref name="height"/> pixels. Returns false when the brush
    /// paints nothing (the caller skips drawing).
    /// </summary>
    internal static bool Apply(APaint paint, Brush brush, float width, float height, double density)
    {
        var resolved = BrushPaint.From(brush, width, height, density);
        paint.SetShader(null);
        switch (resolved.Kind)
        {
            case BrushPaintKind.Solid:
                paint.Color = new AColor(resolved.Color);
                return true;
            case BrushPaintKind.Linear:
                paint.Color = AColor.Black;
                paint.SetShader(new ALinearGradient(
                    (float)resolved.Start.X, (float)resolved.Start.Y, (float)resolved.End.X, (float)resolved.End.Y,
                    resolved.Colors, resolved.Offsets, Tile(resolved.Spread)));
                return true;
            case BrushPaintKind.Radial:
                {
                    paint.Color = AColor.Black;
                    var rx = (float)Math.Max(1e-3, resolved.RadiusX);
                    var ry = (float)Math.Max(1e-3, resolved.RadiusY);
                    var cx = (float)resolved.Center.X;
                    var cy = (float)resolved.Center.Y;
                    AShader shader;
                    var ox = (float)resolved.GradientOrigin.X;
                    var oy = (float)resolved.GradientOrigin.Y;
                    if (Math.Abs(ox - cx) > 0.01f || Math.Abs(oy - cy) > 0.01f)
                    {
                        // An off-centre GradientOrigin: a two-point conical gradient from the origin
                        // (radius 0) to the ellipse (the centre, radius rx) - WinUI's focal gradient.
                        // Built in the circle space of rx; the local matrix below scales y for ry.
                        var fy = cy + ((oy - cy) * rx / ry);
                        var colors = new long[resolved.Colors.Length];
                        for (var i = 0; i < colors.Length; i++)
                        {
                            colors[i] = ((long)(uint)resolved.Colors[i]) << 32; // Color.pack(int): an sRGB colour long
                        }

                        shader = new ARadialGradient(ox, fy, 0f, cx, cy, rx, colors, resolved.Offsets, Tile(resolved.Spread));
                    }
                    else
                    {
                        shader = new ARadialGradient(cx, cy, rx, resolved.Colors, resolved.Offsets, Tile(resolved.Spread));
                    }

                    if (Math.Abs(rx - ry) > 0.01f)
                    {
                        // An elliptical gradient: a circle of radius rx scaled vertically about the centre.
                        using var matrix = new AMatrix();
                        matrix.SetScale(1f, ry / rx, cx, cy);
                        shader.SetLocalMatrix(matrix);
                    }

                    paint.SetShader(shader);
                    return true;
                }

            default:
                return false;
        }
    }

    private static ATileMode Tile(GradientSpreadMethod spread) => spread switch
    {
        GradientSpreadMethod.Reflect => ATileMode.Mirror,
        GradientSpreadMethod.Repeat => ATileMode.Repeat,
        _ => ATileMode.Clamp,
    };
}
