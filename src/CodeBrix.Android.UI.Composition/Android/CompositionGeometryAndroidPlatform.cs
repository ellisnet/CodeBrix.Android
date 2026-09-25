using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Android.UI.Composition.Portable;
using CodeBrix.Platform.UI.Composition;
using CodeBrix.Platform.UI.Composition.Contracts;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Interop.Direct2D;
using APath = global::Android.Graphics.Path;
using ARectF = global::Android.Graphics.RectF;

namespace CodeBrix.Android.UI.Composition.Android;

/// <summary>
/// The Android implementation of <see cref="ICompositionGeometryPlatform"/>: composition
/// geometries are <see cref="APath"/> objects (<see cref="AndroidGeometrySource2D"/>).
/// </summary>
/// <remarks>
/// Every path command is supported: points, the Direct2D fill-mode / figure enumerations,
/// and the Direct2D segment structures (Bezier, quadratic Bezier, arc; single or as a
/// sequence), which are internal to CodeBrix.Platform.Core and readable here through its
/// InternalsVisibleTo grant to CodeBrix.Android.UI.Composition (since pin 1.0.266.1160).
/// Arcs become cubic Beziers (<see cref="ArcToBezier"/>).
/// </remarks>
internal sealed class CompositionGeometryAndroidPlatform : ICompositionGeometryPlatform
{
    // D2D1_FILL_MODE and D2D1_FIGURE_END values (Direct2D ABI constants).
    private const int D2D1FillModeWinding = 1;
    private const int D2D1FigureEndClosed = 1;

    /// <inheritdoc />
    public IGeometrySource2D CreateLine(Vector2 start, Vector2 end)
    {
        var path = new APath();
        path.MoveTo(start.X, start.Y);
        path.LineTo(end.X, end.Y);
        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public IGeometrySource2D CreateRectangle(Vector2 offset, Vector2 size)
    {
        var path = new APath();
        path.AddRect(offset.X, offset.Y, offset.X + size.X, offset.Y + size.Y, APath.Direction.Cw);
        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public IGeometrySource2D CreateRoundedRectangle(Vector2 offset, Vector2 size, Vector2 cornerRadius)
    {
        var path = new APath();
        path.AddRoundRect(offset.X, offset.Y, offset.X + size.X, offset.Y + size.Y, cornerRadius.X, cornerRadius.Y, APath.Direction.Cw);
        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public IGeometrySource2D CreateEllipse(Vector2 center, Vector2 radius)
    {
        var path = new APath();
        path.AddOval(center.X - radius.X, center.Y - radius.Y, center.X + radius.X, center.Y + radius.Y, APath.Direction.Cw);
        return new AndroidGeometrySource2D(path);
    }

    /// <inheritdoc />
    public IGeometrySource2D CreatePath(List<CompositionPathCommand> commands)
    {
        var path = new APath();
        if (commands == null)
        {
            return new AndroidGeometrySource2D(path);
        }

        var current = new Point();
        foreach (var command in commands)
        {
            var parameters = command.Parameters ?? Array.Empty<object>();
            switch (command.Type)
            {
                case CompositionPathCommandType.SetFillMode:
                    path.SetFillType(Convert.ToInt32(parameters[0]) == D2D1FillModeWinding ? APath.FillType.Winding : APath.FillType.EvenOdd);
                    break;
                case CompositionPathCommandType.SetSegmentFlags:
                case CompositionPathCommandType.Close:
                    break;
                case CompositionPathCommandType.BeginFigure:
                    {
                        var point = (Point)parameters[0];
                        path.MoveTo((float)point.X, (float)point.Y);
                        current = point;
                        break;
                    }

                case CompositionPathCommandType.AddLine:
                    {
                        var point = (Point)parameters[0];
                        path.LineTo((float)point.X, (float)point.Y);
                        current = point;
                        break;
                    }

                case CompositionPathCommandType.AddLines:
                    foreach (var point in Items<Point>(parameters[0]))
                    {
                        path.LineTo((float)point.X, (float)point.Y);
                        current = point;
                    }

                    break;
                case CompositionPathCommandType.AddBezier:
                case CompositionPathCommandType.AddBeziers:
                    foreach (var bezier in Items<D2D1BezierSegment>(parameters[0]))
                    {
                        path.CubicTo((float)bezier.Point1.X, (float)bezier.Point1.Y, (float)bezier.Point2.X, (float)bezier.Point2.Y, (float)bezier.Point3.X, (float)bezier.Point3.Y);
                        current = bezier.Point3;
                    }

                    break;
                case CompositionPathCommandType.AddQuadraticBezier:
                case CompositionPathCommandType.AddQuadraticBeziers:
                    foreach (var quad in Items<D2D1QuadraticBezierSegment>(parameters[0]))
                    {
                        path.QuadTo((float)quad.Point1.X, (float)quad.Point1.Y, (float)quad.Point2.X, (float)quad.Point2.Y);
                        current = quad.Point2;
                    }

                    break;
                case CompositionPathCommandType.AddArc:
                    foreach (var arc in Items<D2D1ArcSegment>(parameters[0]))
                    {
                        AppendArc(
                            path,
                            (float)current.X,
                            (float)current.Y,
                            (float)arc.Point.X,
                            (float)arc.Point.Y,
                            arc.Size.Width,
                            arc.Size.Height,
                            arc.RotationAngle,
                            arc.ArcSize == D2D1ArcSize.Large,
                            arc.SweepDirection == D2D1SweepDirection.Clockwise);
                        current = arc.Point;
                    }

                    break;
                case CompositionPathCommandType.EndFigure:
                    if (Convert.ToInt32(parameters[0]) == D2D1FigureEndClosed)
                    {
                        path.Close();
                    }

                    break;
                default:
                    throw new NotSupportedException($"The composition path command {command.Type} is not supported on Android.");
            }
        }

        return new AndroidGeometrySource2D(path);
    }

    /// <summary>A command parameter that is one value or a sequence of values.</summary>
    private static IEnumerable<T> Items<T>(object parameter)
    {
        switch (parameter)
        {
            case T single:
                yield return single;
                break;
            case IEnumerable<T> typed:
                foreach (var item in typed)
                {
                    yield return item;
                }

                break;
            case IEnumerable untyped:
                foreach (var item in untyped)
                {
                    yield return (T)item;
                }

                break;
        }
    }

    /// <inheritdoc />
    public bool IsPlatformGeometry(IGeometrySource2D source) => source is AndroidGeometrySource2D;

    /// <inheritdoc />
    public Rect GetClipBounds(IGeometrySource2D geometry)
    {
        if (geometry is not AndroidGeometrySource2D androidGeometry)
        {
            return default;
        }

        using var bounds = new ARectF();
        androidGeometry.Path.ComputeBounds(bounds, exact: true);
        return new Rect(bounds.Left, bounds.Top, bounds.Width(), bounds.Height());
    }

    /// <summary>
    /// Appends the elliptical arc from the path's current point to (x2, y2) as cubic
    /// Beziers (android.graphics.Path has no endpoint-parameterized arc).
    /// </summary>
    internal static void AppendArc(APath path, float x1, float y1, float x2, float y2, double radiusX, double radiusY, double rotationDegrees, bool isLargeArc, bool isClockwise)
    {
        foreach (var segment in ArcToBezier.Convert(x1, y1, x2, y2, radiusX, radiusY, rotationDegrees, isLargeArc, isClockwise))
        {
            path.CubicTo((float)segment.C1X, (float)segment.C1Y, (float)segment.C2X, (float)segment.C2Y, (float)segment.X, (float)segment.Y);
        }
    }
}
