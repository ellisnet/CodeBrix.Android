using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Composition.Portable;

/// <summary>
/// One cubic Bezier segment: two control points and an end point.
/// </summary>
internal readonly record struct CubicSegment(double C1X, double C1Y, double C2X, double C2Y, double X, double Y);

/// <summary>
/// Converts an SVG / Direct2D style elliptical arc (end point, radii, x-axis rotation,
/// large-arc and sweep flags) into cubic Bezier segments, for path APIs that only offer
/// center-parameterized arcs (android.graphics.Path). Implements the endpoint-to-center
/// conversion of the SVG 1.1 implementation notes (appendix F.6) and approximates each
/// quarter (or smaller) arc with one cubic.
/// </summary>
internal static class ArcToBezier
{
    /// <summary>
    /// Returns the cubic segments that draw the arc from (x1, y1) to (x2, y2). An arc with a
    /// zero radius is a straight line (one degenerate cubic); identical end points yield no
    /// segment.
    /// </summary>
    /// <param name="x1">Start point X.</param>
    /// <param name="y1">Start point Y.</param>
    /// <param name="x2">End point X.</param>
    /// <param name="y2">End point Y.</param>
    /// <param name="radiusX">Ellipse X radius.</param>
    /// <param name="radiusY">Ellipse Y radius.</param>
    /// <param name="rotationDegrees">Rotation of the ellipse's X axis, in degrees.</param>
    /// <param name="isLargeArc">True for the arc greater than 180 degrees.</param>
    /// <param name="isClockwise">True for the positive-angle (clockwise, y down) sweep.</param>
    internal static IReadOnlyList<CubicSegment> Convert(
        double x1, double y1, double x2, double y2,
        double radiusX, double radiusY, double rotationDegrees,
        bool isLargeArc, bool isClockwise)
    {
        var result = new List<CubicSegment>();
        if (x1 == x2 && y1 == y2)
        {
            return result;
        }

        var rx = Math.Abs(radiusX);
        var ry = Math.Abs(radiusY);
        if (rx == 0 || ry == 0)
        {
            result.Add(new CubicSegment(x1, y1, x2, y2, x2, y2));
            return result;
        }

        var phi = rotationDegrees * Math.PI / 180.0;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        // Step 1: (x1', y1')
        var dx = (x1 - x2) / 2.0;
        var dy = (y1 - y2) / 2.0;
        var x1p = (cosPhi * dx) + (sinPhi * dy);
        var y1p = (-sinPhi * dx) + (cosPhi * dy);

        // Correct out-of-range radii.
        var lambda = ((x1p * x1p) / (rx * rx)) + ((y1p * y1p) / (ry * ry));
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        // Step 2: (cx', cy')
        var numerator = (rx * rx * ry * ry) - (rx * rx * y1p * y1p) - (ry * ry * x1p * x1p);
        var denominator = (rx * rx * y1p * y1p) + (ry * ry * x1p * x1p);
        var coefficient = denominator == 0 ? 0 : Math.Sqrt(Math.Max(0, numerator / denominator));
        if (isLargeArc == isClockwise)
        {
            coefficient = -coefficient;
        }

        var cxp = coefficient * (rx * y1p / ry);
        var cyp = coefficient * -(ry * x1p / rx);

        // Step 3: (cx, cy)
        var cx = (cosPhi * cxp) - (sinPhi * cyp) + ((x1 + x2) / 2.0);
        var cy = (sinPhi * cxp) + (cosPhi * cyp) + ((y1 + y2) / 2.0);

        // Step 4: start angle and sweep.
        var theta1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var delta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!isClockwise && delta > 0)
        {
            delta -= 2 * Math.PI;
        }
        else if (isClockwise && delta < 0)
        {
            delta += 2 * Math.PI;
        }

        var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9));
        var step = delta / segments;
        var kappa = 4.0 / 3.0 * Math.Tan(step / 4);

        var angle = theta1;
        var startX = x1;
        var startY = y1;
        for (var i = 0; i < segments; i++)
        {
            var nextAngle = angle + step;
            var cos1 = Math.Cos(angle);
            var sin1 = Math.Sin(angle);
            var cos2 = Math.Cos(nextAngle);
            var sin2 = Math.Sin(nextAngle);

            // Derivatives on the unit circle, scaled by the radii, rotated by phi.
            var (d1x, d1y) = Rotate(-rx * sin1, ry * cos1, cosPhi, sinPhi);
            var (d2x, d2y) = Rotate(-rx * sin2, ry * cos2, cosPhi, sinPhi);
            var (p2x, p2y) = Rotate(rx * cos2, ry * sin2, cosPhi, sinPhi);
            p2x += cx;
            p2y += cy;

            if (i == segments - 1)
            {
                p2x = x2;
                p2y = y2;
            }

            result.Add(new CubicSegment(
                startX + (kappa * d1x), startY + (kappa * d1y),
                p2x - (kappa * d2x), p2y - (kappa * d2y),
                p2x, p2y));

            startX = p2x;
            startY = p2y;
            angle = nextAngle;
        }

        return result;
    }

    private static (double X, double Y) Rotate(double x, double y, double cos, double sin) =>
        ((cos * x) - (sin * y), (sin * x) + (cos * y));

    private static double Angle(double ux, double uy, double vx, double vy)
    {
        var dot = (ux * vx) + (uy * vy);
        var length = Math.Sqrt((ux * ux) + (uy * uy)) * Math.Sqrt((vx * vx) + (vy * vy));
        var cosine = length == 0 ? 1 : Math.Clamp(dot / length, -1, 1);
        var angle = Math.Acos(cosine);
        return ((ux * vy) - (uy * vx)) < 0 ? -angle : angle;
    }
}
