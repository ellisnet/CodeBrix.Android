// Technique derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI.Composition/Composition/CompositionSpriteShape.skia.cs @ tag 6.6.166 (the WinUI cap, dash-endpoint and
// miter-clip rules: IsPositionInDash, GetEndpointDashState, BuildCapPath, TryAddMiterClipTrapezoid).
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>Where a stroke's end point falls in its dash pattern (WinUI's dashed-endpoint rule).</summary>
internal enum EndpointDashState
{
    /// <summary>Inside a dash that is drawn (non-zero length).</summary>
    InRenderedDash,

    /// <summary>At the end of a gap (within WinUI's 0.1-pixel tolerance): a zero-length dash is drawn there.</summary>
    AtGapBoundary,

    /// <summary>In the middle of a gap: nothing is drawn at the end point.</summary>
    InGap,
}

/// <summary>
/// The WinUI stroke rules a shape's native view follows that the Android (Skia) stroker does not apply by
/// itself, as pure math (no Android types, host-testable): dash arrays in stroke-thickness units, which part
/// of the dash pattern a distance along the figure falls in, the polygons of the Square and Triangle caps
/// (Android has no Triangle cap and only one cap for both ends), and WinUI's miter-CLIP join (the miter is
/// cut at the limit; Android falls back to a bevel).
/// </summary>
internal static class StrokeMath
{
    /// <summary>WinUI's MIN_DASH_ARRAY_LENGTH: the tolerance of a figure end on a dash boundary, in pixels.</summary>
    internal const float DashBoundaryTolerance = 0.1f;

    /// <summary>
    /// The dash intervals Android needs from a WinUI StrokeDashArray: in pixels (WinUI lengths are multiples of
    /// the stroke thickness), an even count (an odd array repeats once, as WinUI does), each at least 0.01.
    /// </summary>
    /// <param name="dashes">The StrokeDashArray (null or empty = no dashes).</param>
    /// <param name="thickness">The stroke thickness.</param>
    /// <returns>The intervals, or null when there is no dash pattern (none, or a pattern of zero length).</returns>
    internal static float[] DashIntervals(IList<double> dashes, float thickness)
    {
        if (dashes == null || dashes.Count == 0 || thickness <= 0)
        {
            return null;
        }

        var count = dashes.Count % 2 == 0 ? dashes.Count : dashes.Count * 2;
        var intervals = new float[count];
        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            var value = dashes[i % dashes.Count];
            total += Math.Max(0, value);
            intervals[i] = (float)Math.Max(0.01, value * thickness);
        }

        return total > 0 ? intervals : null;
    }

    /// <summary>True when a distance along the figure falls in a dash (not a gap) of the pattern.</summary>
    /// <param name="position">The distance from the figure start, in pixels.</param>
    /// <param name="intervals">The dash intervals in pixels (dash, gap, dash, ...).</param>
    /// <param name="offset">The dash offset in pixels.</param>
    /// <returns>True inside a dash.</returns>
    internal static bool IsPositionInDash(float position, float[] intervals, float offset)
    {
        var total = Total(intervals);
        if (total <= 0)
        {
            return true;
        }

        var inPattern = (position + offset) % total;
        if (inPattern < 0)
        {
            inPattern += total;
        }

        var accumulated = 0f;
        for (var i = 0; i < intervals.Length; i++)
        {
            accumulated += intervals[i];
            if (inPattern < accumulated)
            {
                return i % 2 == 0;
            }
        }

        return true;
    }

    /// <summary>Where the end of a figure of <paramref name="length"/> falls in the dash pattern.</summary>
    /// <param name="length">The figure length in pixels.</param>
    /// <param name="intervals">The dash intervals in pixels.</param>
    /// <param name="offset">The dash offset in pixels.</param>
    /// <returns>The end point's state.</returns>
    internal static EndpointDashState EndpointState(float length, float[] intervals, float offset)
    {
        var total = Total(intervals);
        if (total <= 0)
        {
            return EndpointDashState.InRenderedDash;
        }

        var position = -(offset % total);
        if (position > 0)
        {
            position -= total;
        }

        var index = 0;
        while (position < length)
        {
            var segment = intervals[index % intervals.Length];
            if (segment <= 0)
            {
                index++;
                continue;
            }

            var end = position + segment;
            var isDash = index % 2 == 0;
            if (end >= length - DashBoundaryTolerance)
            {
                if (isDash && position < length)
                {
                    return EndpointDashState.InRenderedDash;
                }

                if (!isDash && MathF.Abs(end - length) < DashBoundaryTolerance)
                {
                    return EndpointDashState.AtGapBoundary;
                }

                return EndpointDashState.InGap;
            }

            position = end;
            index++;
        }

        return EndpointDashState.InGap;
    }

    /// <summary>
    /// The dash boundaries inside a figure (the starts and ends of the drawn dashes that are not the figure's
    /// own ends on an open figure) - where a Triangle dash cap is added.
    /// </summary>
    /// <param name="length">The figure length in pixels.</param>
    /// <param name="closed">True for a closed figure (its start and end are dash boundaries too).</param>
    /// <param name="intervals">The dash intervals in pixels.</param>
    /// <param name="offset">The dash offset in pixels.</param>
    /// <returns>(distance, faces backwards) for each boundary: a dash start faces back along the figure.</returns>
    internal static List<(float Distance, bool Backward)> InternalDashBoundaries(float length, bool closed, float[] intervals, float offset)
    {
        var result = new List<(float, bool)>();
        var total = Total(intervals);
        if (total <= 0 || length <= 0)
        {
            return result;
        }

        var position = -(offset % total);
        if (position > 0)
        {
            position -= total;
        }

        var index = 0;
        while (position < length)
        {
            var segment = intervals[index % intervals.Length];
            if (segment <= 0)
            {
                index++;
                continue;
            }

            var end = position + segment;
            if (index % 2 == 0)
            {
                var dashStart = Math.Max(position, 0f);
                var dashEnd = Math.Min(end, length);
                if (dashStart < dashEnd)
                {
                    if (closed || dashStart > 0f)
                    {
                        result.Add((dashStart, true));
                    }

                    if (closed || dashEnd < length)
                    {
                        result.Add((dashEnd, false));
                    }
                }
            }

            position = end;
            index++;
        }

        return result;
    }

    /// <summary>
    /// The polygon of a Square or Triangle cap at <paramref name="position"/>, extending along the unit
    /// <paramref name="direction"/>; null for Flat and Round (Round is an arc: the caller draws it).
    /// </summary>
    /// <param name="position">The figure end.</param>
    /// <param name="direction">The unit direction the cap extends in (backwards along the figure at its start).</param>
    /// <param name="width">The stroke thickness.</param>
    /// <param name="cap">The cap.</param>
    /// <returns>The polygon's corners, or null.</returns>
    internal static Vector2[] CapPolygon(Vector2 position, Vector2 direction, float width, PenLineCap cap)
    {
        var half = width / 2;
        var normal = new Vector2(-direction.Y, direction.X);
        switch (cap)
        {
            case PenLineCap.Square:
                var p1 = position + (normal * half);
                var p2 = p1 + (direction * half);
                var p3 = p2 - (normal * width);
                var p4 = position - (normal * half);
                return new[] { p1, p2, p3, p4 };
            case PenLineCap.Triangle:
                return new[] { position + (normal * half), position + (direction * half), position - (normal * half) };
            default:
                return null;
        }
    }

    /// <summary>
    /// The trapezoid WinUI adds at a join whose miter is longer than the limit (miter-clip: the miter cut at the
    /// limit distance), or null when the plain miter is within the limit, the join is (nearly) straight, or it
    /// folds back. <paramref name="incoming"/> and <paramref name="outgoing"/> are unit directions.
    /// </summary>
    /// <param name="vertex">The join point.</param>
    /// <param name="incoming">The unit direction of the segment arriving at the vertex.</param>
    /// <param name="outgoing">The unit direction of the segment leaving it.</param>
    /// <param name="halfWidth">Half the stroke thickness.</param>
    /// <param name="miterLimit">The miter limit (a multiple of half the thickness).</param>
    /// <returns>The four corners, or null.</returns>
    internal static Vector2[] MiterClipTrapezoid(Vector2 vertex, Vector2 incoming, Vector2 outgoing, float halfWidth, float miterLimit)
    {
        var dot = Vector2.Dot(incoming, outgoing);
        var sinHalfSq = (1 + dot) / 2;
        if (sinHalfSq <= 0)
        {
            return null;
        }

        var sinHalf = MathF.Sqrt(sinHalfSq);
        if (miterLimit <= 0 || sinHalf >= 1f / miterLimit)
        {
            return null;
        }

        var cosHalfSq = (1 - dot) / 2;
        if (cosHalfSq <= 1e-12f)
        {
            return null;
        }

        var cosHalf = MathF.Sqrt(cosHalfSq);
        var ratio = (miterLimit - sinHalf) / cosHalf;
        if (ratio <= 0)
        {
            return null;
        }

        var cross = (incoming.X * outgoing.Y) - (incoming.Y * outgoing.X);
        if (MathF.Abs(cross) < 1e-6f)
        {
            return null;
        }

        Vector2 normalIn;
        Vector2 normalOut;
        if (cross > 0)
        {
            normalIn = new Vector2(incoming.Y, -incoming.X);
            normalOut = new Vector2(outgoing.Y, -outgoing.X);
        }
        else
        {
            normalIn = new Vector2(-incoming.Y, incoming.X);
            normalOut = new Vector2(-outgoing.Y, outgoing.X);
        }

        var bevelIn = vertex + (normalIn * halfWidth);
        var bevelOut = vertex + (normalOut * halfWidth);
        var extension = ratio * halfWidth;
        return new[] { bevelIn, bevelIn + (incoming * extension), bevelOut - (outgoing * extension), bevelOut };
    }

    /// <summary>The unit vector of (x, y), or zero for a (near) zero-length vector.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <returns>The unit vector or <see cref="Vector2.Zero"/>.</returns>
    internal static Vector2 Normalize(float x, float y)
    {
        var length = MathF.Sqrt((x * x) + (y * y));
        return length < 1e-6f ? Vector2.Zero : new Vector2(x / length, y / length);
    }

    /// <summary>
    /// The miter limit the view strokes with: the shape's StrokeMiterLimit, at least 1 (a ratio below 1 cannot hold
    /// even a straight join; WinUI treats it as 1). Since pin 1.0.268.12 (WPE1-5 B5) Core's default is WinUI's 10; the
    /// AP6 rule "below 1 means not set, use 10" (for Core's old default 0) is gone.
    /// </summary>
    /// <param name="miterLimit">The shape's StrokeMiterLimit.</param>
    /// <returns>The effective limit.</returns>
    internal static float EffectiveMiterLimit(double miterLimit) => (float)(double.IsFinite(miterLimit) ? Math.Max(1, miterLimit) : 10);

    private static float Total(float[] intervals)
    {
        if (intervals == null)
        {
            return 0;
        }

        var total = 0f;
        foreach (var value in intervals)
        {
            total += value;
        }

        return total;
    }
}
