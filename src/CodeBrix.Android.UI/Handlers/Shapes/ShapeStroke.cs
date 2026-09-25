// Technique derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI.Composition/Composition/CompositionSpriteShape.skia.cs @ tag 6.6.166 (the stroke outline: the stroker's
// fill path plus WinUI's own start/end/triangle caps, dashed end points and miter-clip joins, then filled with the
// stroke brush over the outline's bounds). Rewritten for android.graphics (Path, PathMeasure, Path.Op, PathIterator).
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Numerics;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ADashPathEffect = global::Android.Graphics.DashPathEffect;
using APaint = global::Android.Graphics.Paint;
using APath = global::Android.Graphics.Path;
using APathMeasure = global::Android.Graphics.PathMeasure;
using ARectF = global::Android.Graphics.RectF;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Builds the OUTLINE of a shape's stroke as a fillable path (in the shape's DIPs), the way WinUI strokes: the
/// Android stroker gives the body (thickness, join, the dash pattern with StrokeDashCap), and the WinUI rules it
/// does not know are added as geometry - different StrokeStartLineCap / StrokeEndLineCap, the Triangle cap, the
/// figure's own caps at dashed end points (Android puts the dash cap there), Triangle dash caps and the miter-clip
/// join (Android bevels a miter over the limit; WinUI cuts it at the limit). The shape's native view fills this
/// outline with the Stroke brush, and Core's hit test asks the same outline.
/// </summary>
internal static class ShapeStroke
{
    /// <summary>Builds the stroke outline of <paramref name="geometry"/> into <paramref name="outline"/>.</summary>
    /// <param name="geometry">The shape's geometry, transformed into the shape's coordinates (DIPs).</param>
    /// <param name="shape">The shape (its stroke properties).</param>
    /// <param name="thickness">The stroke thickness in DIPs (&gt; 0).</param>
    /// <param name="paint">A scratch paint (reset here).</param>
    /// <param name="outline">Receives the outline (reset here).</param>
    internal static void Build(APath geometry, Shape shape, float thickness, APaint paint, APath outline)
    {
        outline.Reset();
        paint.Reset();
        paint.AntiAlias = true;
        paint.SetStyle(APaint.Style.Stroke);
        paint.StrokeWidth = thickness;
        paint.StrokeJoin = shape.StrokeLineJoin switch
        {
            PenLineJoin.Round => APaint.Join.Round,
            PenLineJoin.Bevel => APaint.Join.Bevel,
            _ => APaint.Join.Miter,
        };
        var miterLimit = StrokeMath.EffectiveMiterLimit(shape.StrokeMiterLimit);
        paint.StrokeMiter = miterLimit;

        var startCap = shape.StrokeStartLineCap;
        var endCap = shape.StrokeEndLineCap;
        var dashCap = shape.StrokeDashCap;
        var customCaps = startCap != endCap || startCap == PenLineCap.Triangle;
        var intervals = StrokeMath.DashIntervals(shape.StrokeDashArray, thickness);
        var dashOffset = (float)(shape.StrokeDashOffset * thickness);
        if (intervals != null)
        {
            paint.StrokeCap = Cap(dashCap);
            paint.SetPathEffect(new ADashPathEffect(intervals, dashOffset));
        }
        else
        {
            paint.StrokeCap = customCaps ? APaint.Cap.Butt : Cap(endCap);
        }

        paint.GetFillPath(geometry, outline);
        paint.SetPathEffect(null);

        if (intervals == null && customCaps)
        {
            AddFigureCaps(outline, geometry, thickness, startCap, endCap);
        }

        if (intervals != null && (dashCap != PenLineCap.Flat || startCap != PenLineCap.Flat || endCap != PenLineCap.Flat))
        {
            FixDashedEndpoints(outline, geometry, thickness, dashCap, startCap, endCap, intervals, dashOffset);
        }

        if (intervals != null && dashCap == PenLineCap.Triangle)
        {
            AddTriangleDashCaps(outline, geometry, thickness, intervals, dashOffset);
        }

        if (shape.StrokeLineJoin == PenLineJoin.Miter && OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            AddClippedMiters(outline, geometry, thickness / 2, miterLimit);
        }
    }

    /// <summary>The Android cap of a WinUI cap (Triangle has none: added as geometry).</summary>
    internal static APaint.Cap Cap(PenLineCap cap) => cap switch
    {
        PenLineCap.Square => APaint.Cap.Square,
        PenLineCap.Round => APaint.Cap.Round,
        _ => APaint.Cap.Butt,
    };

    private static void AddFigureCaps(APath outline, APath geometry, float width, PenLineCap startCap, PenLineCap endCap)
    {
        using var measure = new APathMeasure(geometry, false);
        var pos = new float[2];
        var tan = new float[2];
        do
        {
            var length = measure.Length;
            if (measure.IsClosed || length <= 0)
            {
                continue;
            }

            if (startCap != PenLineCap.Flat && measure.GetPosTan(0, pos, tan))
            {
                Union(outline, CapPath(new Vector2(pos[0], pos[1]), -new Vector2(tan[0], tan[1]), width, startCap));
            }

            if (endCap != PenLineCap.Flat && measure.GetPosTan(length, pos, tan))
            {
                Union(outline, CapPath(new Vector2(pos[0], pos[1]), new Vector2(tan[0], tan[1]), width, endCap));
            }
        }
        while (measure.NextContour());
    }

    private static void FixDashedEndpoints(APath outline, APath geometry, float width, PenLineCap dashCap, PenLineCap startCap,
        PenLineCap endCap, float[] intervals, float dashOffset)
    {
        using var measure = new APathMeasure(geometry, false);
        var pos = new float[2];
        var tan = new float[2];
        do
        {
            var length = measure.Length;
            if (measure.IsClosed || length <= 0)
            {
                continue;
            }

            if (dashCap != startCap && measure.GetPosTan(0, pos, tan) && StrokeMath.IsPositionInDash(0, intervals, dashOffset))
            {
                var at = new Vector2(pos[0], pos[1]);
                var back = -new Vector2(tan[0], tan[1]);
                if (dashCap != PenLineCap.Flat)
                {
                    Cut(outline, HalfPlane(at, back, width));
                }

                if (startCap != PenLineCap.Flat)
                {
                    Union(outline, CapPath(at, back, width, startCap));
                }
            }

            if (measure.GetPosTan(length, pos, tan))
            {
                var at = new Vector2(pos[0], pos[1]);
                var forward = new Vector2(tan[0], tan[1]);
                switch (StrokeMath.EndpointState(length, intervals, dashOffset))
                {
                    case EndpointDashState.InRenderedDash when dashCap != endCap:
                        if (dashCap != PenLineCap.Flat)
                        {
                            Cut(outline, HalfPlane(at, forward, width));
                        }

                        if (endCap != PenLineCap.Flat)
                        {
                            Union(outline, CapPath(at, forward, width, endCap));
                        }

                        break;
                    case EndpointDashState.AtGapBoundary:
                        // A zero-length dash at the end: the dash cap facing back and the end cap facing forward.
                        if (dashCap != PenLineCap.Flat)
                        {
                            Union(outline, CapPath(at, -forward, width, dashCap));
                        }

                        if (endCap != PenLineCap.Flat)
                        {
                            Union(outline, CapPath(at, forward, width, endCap));
                        }

                        break;
                }
            }
        }
        while (measure.NextContour());
    }

    private static void AddTriangleDashCaps(APath outline, APath geometry, float width, float[] intervals, float dashOffset)
    {
        using var measure = new APathMeasure(geometry, false);
        var pos = new float[2];
        var tan = new float[2];
        do
        {
            foreach (var (distance, backward) in StrokeMath.InternalDashBoundaries(measure.Length, measure.IsClosed, intervals, dashOffset))
            {
                if (measure.GetPosTan(distance, pos, tan))
                {
                    var direction = new Vector2(tan[0], tan[1]);
                    Union(outline, CapPath(new Vector2(pos[0], pos[1]), backward ? -direction : direction, width, PenLineCap.Triangle));
                }
            }
        }
        while (measure.NextContour());
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android34.0")]
    private static void AddClippedMiters(APath outline, APath geometry, float halfWidth, float miterLimit)
    {
        var iterator = geometry.PathIterator;
        var points = new float[8];
        var contourStart = Vector2.Zero;
        var firstOut = Vector2.Zero;
        var hasFirstOut = false;
        var previousIn = Vector2.Zero;
        var hasPreviousIn = false;
        var previousEnd = Vector2.Zero;
        while (iterator.HasNext)
        {
            var verb = (global::Android.Graphics.PathVerb)iterator.Next(points, 0);
            switch (verb)
            {
                case global::Android.Graphics.PathVerb.Move:
                    contourStart = new Vector2(points[0], points[1]);
                    previousEnd = contourStart;
                    hasPreviousIn = false;
                    hasFirstOut = false;
                    break;
                case global::Android.Graphics.PathVerb.Line:
                case global::Android.Graphics.PathVerb.Quad:
                case global::Android.Graphics.PathVerb.Conic:
                case global::Android.Graphics.PathVerb.Cubic:
                    {
                        var count = verb == global::Android.Graphics.PathVerb.Line ? 2
                            : verb == global::Android.Graphics.PathVerb.Cubic ? 4 : 3;
                        var start = new Vector2(points[0], points[1]);
                        var end = new Vector2(points[(count - 1) * 2], points[((count - 1) * 2) + 1]);
                        var outDir = Vector2.Zero;
                        for (var i = 1; i < count && outDir == Vector2.Zero; i++)
                        {
                            outDir = StrokeMath.Normalize(points[i * 2] - start.X, points[(i * 2) + 1] - start.Y);
                        }

                        var inDir = Vector2.Zero;
                        for (var i = count - 2; i >= 0 && inDir == Vector2.Zero; i--)
                        {
                            inDir = StrokeMath.Normalize(end.X - points[i * 2], end.Y - points[(i * 2) + 1]);
                        }

                        if (outDir == Vector2.Zero || inDir == Vector2.Zero)
                        {
                            break;
                        }

                        if (hasPreviousIn)
                        {
                            AddMiter(outline, start, previousIn, outDir, halfWidth, miterLimit);
                        }

                        if (!hasFirstOut)
                        {
                            firstOut = outDir;
                            hasFirstOut = true;
                        }

                        previousIn = inDir;
                        hasPreviousIn = true;
                        previousEnd = end;
                        break;
                    }

                case global::Android.Graphics.PathVerb.Close:
                    if (hasPreviousIn && hasFirstOut)
                    {
                        var closing = contourStart - previousEnd;
                        if (closing.Length() > 1e-6f)
                        {
                            var closeDir = Vector2.Normalize(closing);
                            AddMiter(outline, previousEnd, previousIn, closeDir, halfWidth, miterLimit);
                            AddMiter(outline, contourStart, closeDir, firstOut, halfWidth, miterLimit);
                        }
                        else
                        {
                            AddMiter(outline, contourStart, previousIn, firstOut, halfWidth, miterLimit);
                        }
                    }

                    hasPreviousIn = false;
                    hasFirstOut = false;
                    break;
            }
        }
    }

    private static void AddMiter(APath outline, Vector2 vertex, Vector2 incoming, Vector2 outgoing, float halfWidth, float miterLimit)
    {
        if (StrokeMath.MiterClipTrapezoid(vertex, incoming, outgoing, halfWidth, miterLimit) is { } corners)
        {
            Union(outline, Polygon(corners));
        }
    }

    private static APath CapPath(Vector2 position, Vector2 direction, float width, PenLineCap cap)
    {
        if (cap == PenLineCap.Round)
        {
            var half = width / 2;
            var normal = new Vector2(-direction.Y, direction.X);
            var startAngle = (float)(Math.Atan2(normal.Y, normal.X) * 180 / Math.PI);
            var path = new APath();
            using var oval = new ARectF(position.X - half, position.Y - half, position.X + half, position.Y + half);
            path.AddArc(oval, startAngle, -180);
            path.Close();
            return path;
        }

        return StrokeMath.CapPolygon(position, direction, width, cap) is { } corners ? Polygon(corners) : null;
    }

    private static APath HalfPlane(Vector2 position, Vector2 direction, float width)
    {
        var size = width * 2;
        var normal = new Vector2(-direction.Y, direction.X);
        var p1 = position + (normal * size);
        var p2 = p1 + (direction * size);
        var p3 = position - (normal * size) + (direction * size);
        var p4 = position - (normal * size);
        return Polygon(new[] { p1, p2, p3, p4 });
    }

    private static APath Polygon(Vector2[] corners)
    {
        var path = new APath();
        path.MoveTo(corners[0].X, corners[0].Y);
        for (var i = 1; i < corners.Length; i++)
        {
            path.LineTo(corners[i].X, corners[i].Y);
        }

        path.Close();
        return path;
    }

    private static void Union(APath outline, APath piece)
    {
        if (piece == null)
        {
            return;
        }

        using (piece)
        {
            outline.InvokeOp(piece, APath.Op.Union);
        }
    }

    private static void Cut(APath outline, APath cutter)
    {
        using (cutter)
        {
            outline.InvokeOp(cutter, APath.Op.Difference);
        }
    }
}
