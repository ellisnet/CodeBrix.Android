using System;
using CodeBrix.Android.UI.Composition.Android;
using CodeBrix.Platform.Media;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using AMatrix = global::Android.Graphics.Matrix;
using APath = global::Android.Graphics.Path;
using ARectF = global::Android.Graphics.RectF;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IGeometryPlatform"/>: XAML geometries become
/// <see cref="APath"/> objects in DIPs (wrapped as <see cref="AndroidGeometrySource2D"/>, the
/// type the Android composition geometry platform also uses). Supports RectangleGeometry,
/// EllipseGeometry, LineGeometry, PathGeometry (every segment type; arcs are converted to
/// cubic Beziers), GeometryGroup and StreamGeometry, with the geometry's Transform applied.
/// </summary>
internal sealed class GeometryAndroidPlatform : IGeometryPlatform
{
    /// <inheritdoc />
    public IGeometryPathBuilder CreatePathBuilder() => new PathBuilder();

    /// <inheritdoc />
    public IGeometrySource2D CreateGeometrySource(Geometry geometry) => new AndroidGeometrySource2D(BuildPath(geometry, filledOnly: false));

    /// <inheritdoc />
    public IGeometrySource2D CreateFilledGeometrySource(Geometry geometry) => new AndroidGeometrySource2D(BuildPath(geometry, filledOnly: true));

    /// <inheritdoc />
    public IGeometrySource2D CreateEllipse(Rect bounds)
    {
        var path = new APath();
        path.AddOval((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom, APath.Direction.Cw);
        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public IGeometrySource2D CreateRectangle(Rect rect, double radiusX, double radiusY)
    {
        var path = new APath();
        if (radiusX > 0 || radiusY > 0)
        {
            path.AddRoundRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom, (float)radiusX, (float)radiusY, APath.Direction.Cw);
        }
        else
        {
            path.AddRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom, APath.Direction.Cw);
        }

        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public Rect GetTightBounds(IGeometrySource2D geometry)
    {
        if (geometry is not AndroidGeometrySource2D androidGeometry || androidGeometry.Path.IsEmpty)
        {
            return default;
        }

        using var bounds = new ARectF();
        androidGeometry.Path.ComputeBounds(bounds, exact: true);
        return new Rect(bounds.Left, bounds.Top, bounds.Width(), bounds.Height());
    }

    /// <summary>Builds the Android path of a XAML geometry (DIPs).</summary>
    internal static APath BuildPath(Geometry geometry, bool filledOnly)
    {
        var path = new APath();
        if (geometry != null)
        {
            Append(path, geometry, filledOnly);
            ApplyTransform(path, geometry.Transform);
        }

        return path;
    }

    private static void Append(APath path, Geometry geometry, bool filledOnly)
    {
        switch (geometry)
        {
            case RectangleGeometry rectangle:
                var rect = rectangle.Rect;
                path.AddRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom, APath.Direction.Cw);
                break;
            case EllipseGeometry ellipse:
                var center = ellipse.Center;
                path.AddOval(
                    (float)(center.X - ellipse.RadiusX), (float)(center.Y - ellipse.RadiusY),
                    (float)(center.X + ellipse.RadiusX), (float)(center.Y + ellipse.RadiusY),
                    APath.Direction.Cw);
                break;
            case LineGeometry line:
                if (!filledOnly)
                {
                    path.MoveTo((float)line.StartPoint.X, (float)line.StartPoint.Y);
                    path.LineTo((float)line.EndPoint.X, (float)line.EndPoint.Y);
                }

                break;
            case PathGeometry pathGeometry:
                path.SetFillType(pathGeometry.FillRule == FillRule.Nonzero ? APath.FillType.Winding : APath.FillType.EvenOdd);
                if (pathGeometry.Figures != null)
                {
                    foreach (var figure in pathGeometry.Figures)
                    {
                        if (!filledOnly || figure.IsFilled)
                        {
                            AppendFigure(path, figure);
                        }
                    }
                }

                break;
            case GeometryGroup group:
                path.SetFillType(group.FillRule == FillRule.Nonzero ? APath.FillType.Winding : APath.FillType.EvenOdd);
                if (group.Children != null)
                {
                    foreach (var child in group.Children)
                    {
                        using var childPath = BuildPath(child, filledOnly);
                        path.AddPath(childPath);
                    }
                }

                break;
            case StreamGeometry stream:
                if (stream.PlatformPath is APath streamPath)
                {
                    path.AddPath(streamPath);
                }

                path.SetFillType(stream.FillRule == FillRule.Nonzero ? APath.FillType.Winding : APath.FillType.EvenOdd);
                break;
        }
    }

    private static void AppendFigure(APath path, PathFigure figure)
    {
        var current = figure.StartPoint;
        path.MoveTo((float)current.X, (float)current.Y);
        if (figure.Segments != null)
        {
            foreach (var segment in figure.Segments)
            {
                current = AppendSegment(path, segment, current);
            }
        }

        if (figure.IsClosed)
        {
            path.Close();
        }
    }

    private static Point AppendSegment(APath path, PathSegment segment, Point current)
    {
        switch (segment)
        {
            case LineSegment line:
                path.LineTo((float)line.Point.X, (float)line.Point.Y);
                return line.Point;
            case PolyLineSegment polyLine:
                foreach (var point in polyLine.Points)
                {
                    path.LineTo((float)point.X, (float)point.Y);
                    current = point;
                }

                return current;
            case BezierSegment bezier:
                path.CubicTo((float)bezier.Point1.X, (float)bezier.Point1.Y, (float)bezier.Point2.X, (float)bezier.Point2.Y, (float)bezier.Point3.X, (float)bezier.Point3.Y);
                return bezier.Point3;
            case PolyBezierSegment polyBezier:
                for (var i = 0; i + 2 < polyBezier.Points.Count; i += 3)
                {
                    var p1 = polyBezier.Points[i];
                    var p2 = polyBezier.Points[i + 1];
                    var p3 = polyBezier.Points[i + 2];
                    path.CubicTo((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, (float)p3.X, (float)p3.Y);
                    current = p3;
                }

                return current;
            case QuadraticBezierSegment quadratic:
                path.QuadTo((float)quadratic.Point1.X, (float)quadratic.Point1.Y, (float)quadratic.Point2.X, (float)quadratic.Point2.Y);
                return quadratic.Point2;
            case PolyQuadraticBezierSegment polyQuadratic:
                for (var i = 0; i + 1 < polyQuadratic.Points.Count; i += 2)
                {
                    var p1 = polyQuadratic.Points[i];
                    var p2 = polyQuadratic.Points[i + 1];
                    path.QuadTo((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y);
                    current = p2;
                }

                return current;
            case ArcSegment arc:
                CompositionGeometryAndroidPlatform.AppendArc(
                    path,
                    (float)current.X, (float)current.Y, (float)arc.Point.X, (float)arc.Point.Y,
                    arc.Size.Width, arc.Size.Height, arc.RotationAngle,
                    arc.IsLargeArc, arc.SweepDirection == SweepDirection.Clockwise);
                return arc.Point;
            default:
                return current;
        }
    }

    private static void ApplyTransform(APath path, Transform transform)
    {
        if (transform == null)
        {
            return;
        }

        var m = transform.MatrixCore;
        if (m.IsIdentity)
        {
            return;
        }

        using var matrix = new AMatrix();
        matrix.SetValues(new[] { m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, 0f, 0f, 1f });
        path.Transform(matrix);
    }

    /// <summary>The <see cref="IGeometryPathBuilder"/> of StreamGeometry: records into an Android path.</summary>
    private sealed class PathBuilder : IGeometryPathBuilder
    {
        private readonly APath _path = new();

        public void MoveTo(Point point) => _path.MoveTo((float)point.X, (float)point.Y);

        public void LineTo(Point point) => _path.LineTo((float)point.X, (float)point.Y);

        public void CubicTo(Point point1, Point point2, Point point3) =>
            _path.CubicTo((float)point1.X, (float)point1.Y, (float)point2.X, (float)point2.Y, (float)point3.X, (float)point3.Y);

        public void QuadTo(Point point1, Point point2) =>
            _path.QuadTo((float)point1.X, (float)point1.Y, (float)point2.X, (float)point2.Y);

        public void ArcTo(Rect oval, double startAngle, double sweepAngle)
        {
            using var rect = new ARectF((float)oval.Left, (float)oval.Top, (float)oval.Right, (float)oval.Bottom);
            _path.ArcTo(rect, (float)startAngle, (float)sweepAngle, forceMoveTo: false);
        }

        public void Close() => _path.Close();

        public object Snapshot() => new APath(_path);
    }
}
