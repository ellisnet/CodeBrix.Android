// Technique from .NET MAUI, src/Core/src/Handlers/ShapeView/ShapeViewHandler.Android.cs @ 828569a864 (a shape is one
// custom Android view that draws its path with the fill and stroke paints). Copyright (c) .NET Foundation and
// Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Composition.Android;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using ACanvas = global::Android.Graphics.Canvas;
using AContext = global::Android.Content.Context;
using AMatrix = global::Android.Graphics.Matrix;
using APaint = global::Android.Graphics.Paint;
using APath = global::Android.Graphics.Path;
using ARectF = global::Android.Graphics.RectF;
using ARegion = global::Android.Graphics.Region;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of every Shape (Rectangle, Ellipse, Line, Polygon, Polyline, Path): Core keeps measuring,
/// stretching and building the geometry exactly as on Skia (the shape's composition sprite holds the geometry -
/// an android.graphics.Path from CodeBrix.Android's geometry platform, built from the WinUI figure/segment
/// model - and the stretch transform); the native <see cref="ShapeView"/> draws it. The Fill brush (solid,
/// linear or radial gradient) fills the fill geometry over ITS bounds; the stroke is an outline built the WinUI
/// way (<see cref="ShapeStroke"/>: thickness not scaled by Stretch, independent start/end caps, Triangle caps,
/// dash caps, dashed end points, miter-clip joins) filled with the Stroke brush over the outline's bounds - what
/// the Skia heads paint. The handler owns the shape's hit test (OwnsVisuals): Core asks it whether a point is in
/// the fill or on the stroke outline (Android's composition platform is inert, so Core's own sprite hit test
/// answers no).
/// </summary>
internal sealed class ShapeHandler : ViewHandler<Shape, ShapeView>
{
    /// <summary>The shapes' mapper: every drawing property repaints.</summary>
    public static readonly PropertyMapper<Shape, ShapeHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Shape.FillProperty] = MapPaint,
        [Shape.StrokeProperty] = MapPaint,
        [Shape.StrokeThicknessProperty] = MapPaint,
        [Shape.StrokeDashArrayProperty] = MapPaint,
        [Shape.StrokeDashOffsetProperty] = MapPaint,
        [Shape.StrokeDashCapProperty] = MapPaint,
        [Shape.StrokeStartLineCapProperty] = MapPaint,
        [Shape.StrokeEndLineCapProperty] = MapPaint,
        [Shape.StrokeLineJoinProperty] = MapPaint,
        [Shape.StrokeMiterLimitProperty] = MapPaint,
        [Shape.StretchProperty] = MapPaint,
    };

    private readonly BrushWatcher _fillWatcher;
    private readonly BrushWatcher _strokeWatcher;

    /// <summary>Creates the handler.</summary>
    public ShapeHandler()
        : base(Mapper)
    {
        _fillWatcher = new BrushWatcher(() => NativeView?.Invalidate());
        _strokeWatcher = new BrushWatcher(() => NativeView?.Invalidate());
    }

    /// <summary>
    /// The native view draws the shape and answers its hit test; Core still measures, stretches and builds
    /// the geometry (a Shape has no template, so nothing else changes).
    /// </summary>
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals;

    /// <summary>Repaints (and follows the brushes' contents).</summary>
    public static void MapPaint(ShapeHandler handler, Shape shape)
    {
        handler._fillWatcher.Watch(shape.Fill);
        handler._strokeWatcher.Watch(shape.Stroke);
        handler.NativeView?.Invalidate();
    }

    /// <summary>
    /// Core's hit test of the shape: true on its fill (when it has a Fill) or on its stroke outline (when it has
    /// a Stroke), as WinUI hit-tests shapes - not the bounding box.
    /// </summary>
    /// <param name="relativeLocation">The point relative to the shape, in DIPs.</param>
    /// <returns>True when the shape is hit.</returns>
    public override bool HitTest(Point relativeLocation)
    {
        if (Element is not Shape shape)
        {
            return false;
        }

        var x = (float)relativeLocation.X;
        var y = (float)relativeLocation.Y;
        using var geometry = new ShapeGeometry(shape);
        return geometry.Contains(x, y);
    }

    /// <inheritdoc />
    protected override ShapeView CreatePlatformView() => new(Context, this);

    /// <inheritdoc />
    protected override void DisconnectHandler(ShapeView platformView)
    {
        _fillWatcher.Clear();
        _strokeWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);

        // Core rebuilds the geometry during arrange (stretch, size): repaint every time.
        NativeView?.Invalidate();
    }
}

/// <summary>
/// A shape's drawable geometry at one moment: its fill paths and stroke outlines in the shape's DIPs (each
/// composition sprite's path transformed by the sprite's stretch transform and offset).
/// </summary>
internal sealed class ShapeGeometry : IDisposable
{
    private const float HitScale = 8f;
    private readonly Shape _shape;
    private readonly APaint _paint = new();

    /// <summary>Builds the geometry of <paramref name="shape"/> from Core's composition sprites.</summary>
    /// <param name="shape">The shape.</param>
    internal ShapeGeometry(Shape shape)
    {
        _shape = shape;
        if (shape.Visual is not ShapeVisual visual || visual.ShapesIfCreated is not { } shapes)
        {
            return;
        }

        var thickness = StrokeThickness(shape);
        foreach (var item in shapes)
        {
            if (item is not CompositionSpriteShape sprite || PathOf(sprite.Geometry) is not { } path)
            {
                continue;
            }

            // The sprite's transform (Stretch scale about its centre point) and then its Offset (the Stretch
            // translation: half the stroke thickness minus the scaled geometry origin), as Core's ShapeVisual paints it.
            var m = sprite.CombinedTransformMatrix;
            var offset = sprite.Offset;
            using var matrix = new AMatrix();
            matrix.SetValues(new[] { m.M11, m.M21, m.M31 + offset.X, m.M12, m.M22, m.M32 + offset.Y, 0f, 0f, 1f });

            var geometry = new APath(path);
            geometry.Transform(matrix);
            var fill = geometry;
            if (PathOf(sprite.FillGeometry) is { } fillPath)
            {
                fill = new APath(fillPath);
                fill.Transform(matrix);
            }

            APath outline = null;
            if (thickness > 0)
            {
                outline = new APath();
                ShapeStroke.Build(geometry, shape, thickness, _paint, outline);
            }

            Items.Add((fill, outline));
            if (!ReferenceEquals(fill, geometry))
            {
                geometry.Dispose();
            }
        }
    }

    /// <summary>(fill path, stroke outline or null) per sprite.</summary>
    internal System.Collections.Generic.List<(APath Fill, APath Outline)> Items { get; } = new();

    /// <summary>The stroke thickness the shape draws with (0 without a Stroke).</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The thickness in DIPs.</returns>
    internal static float StrokeThickness(Shape shape) =>
        shape.Stroke == null || double.IsNaN(shape.StrokeThickness) ? 0f : (float)Math.Max(0, shape.StrokeThickness);

    /// <summary>True when (x, y) (DIPs) is in a fill (with a Fill brush) or on a stroke outline (with a Stroke brush).</summary>
    /// <param name="x">X in DIPs.</param>
    /// <param name="y">Y in DIPs.</param>
    /// <returns>True when hit.</returns>
    internal bool Contains(float x, float y)
    {
        foreach (var (fill, outline) in Items)
        {
            if (_shape.Fill != null && Contains(fill, x, y))
            {
                return true;
            }

            if (outline != null && Contains(outline, x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (fill, outline) in Items)
        {
            fill?.Dispose();
            outline?.Dispose();
        }

        Items.Clear();
        _paint.Dispose();
    }

    private static APath PathOf(CompositionGeometry geometry) =>
        (geometry as CompositionPathGeometry)?.Path?.GeometrySource is AndroidGeometrySource2D { Path: { } path } ? path : null;

    private static bool Contains(APath path, float x, float y)
    {
        using var bounds = new ARectF();
#pragma warning disable CA1422 // ComputeBounds(RectF, bool): the exact form; the one-argument overload is API 31+.
        path.ComputeBounds(bounds, true);
#pragma warning restore CA1422
        if (!bounds.Contains(x, y))
        {
            return false;
        }

        // Region works in whole numbers: test at 1/8 DIP.
        using var scale = new AMatrix();
        scale.SetScale(HitScale, HitScale);
        using var scaled = new APath(path);
        scaled.Transform(scale);
        using var clip = new ARegion(
            (int)Math.Floor(bounds.Left * HitScale) - 1,
            (int)Math.Floor(bounds.Top * HitScale) - 1,
            (int)Math.Ceiling(bounds.Right * HitScale) + 1,
            (int)Math.Ceiling(bounds.Bottom * HitScale) + 1);
        using var region = new ARegion();
        region.SetPath(scaled, clip);
        return region.Contains((int)Math.Floor(x * HitScale), (int)Math.Floor(y * HitScale));
    }
}

/// <summary>The native view of a Shape: draws the shape's Core-built geometry the WinUI way.</summary>
internal sealed class ShapeView : AView
{
    private readonly ShapeHandler _handler;
    private readonly APaint _paint = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private readonly APath _offset = new();
    private readonly ARectF _bounds = new();

    internal ShapeView(AContext context, ShapeHandler handler)
        : base(context)
    {
        _handler = handler;
        _paint.SetStyle(APaint.Style.Fill);
    }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (_handler.Element is not Shape shape)
        {
            return;
        }

        using var geometry = new ShapeGeometry(shape);
        if (geometry.Items.Count == 0)
        {
            return;
        }

        var density = (float)_handler.Density;
        canvas.Save();
        canvas.Scale(density, density);
        foreach (var (fill, outline) in geometry.Items)
        {
            if (shape.Fill != null)
            {
                Paint(canvas, fill, shape.Fill);
            }

            if (outline != null)
            {
                Paint(canvas, outline, shape.Stroke);
            }
        }

        canvas.Restore();
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="brush"/> mapped over the path's own bounds.</summary>
    private void Paint(ACanvas canvas, APath path, Brush brush)
    {
#pragma warning disable CA1422 // ComputeBounds(RectF, bool): the exact form; the one-argument overload is API 31+.
        path.ComputeBounds(_bounds, true);
#pragma warning restore CA1422
        var width = _bounds.Width();
        var height = _bounds.Height();
        _paint.Reset();
        _paint.AntiAlias = true;
        _paint.SetStyle(APaint.Style.Fill);
        if (width <= 0 && height <= 0)
        {
            return;
        }

        if (!BrushShader.Apply(_paint, brush, Math.Max(width, 1e-3f), Math.Max(height, 1e-3f), 1))
        {
            return;
        }

        canvas.Save();
        canvas.Translate(_bounds.Left, _bounds.Top);
        _offset.Set(path);
        _offset.Offset(-_bounds.Left, -_bounds.Top);
        canvas.DrawPath(_offset, _paint);
        canvas.Restore();
    }
}
