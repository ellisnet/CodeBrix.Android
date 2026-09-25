// Derived from .NET MAUI, src/Core/src/Platform/Android/BorderDrawable.cs and src/Core/src/Graphics/MauiDrawable.Android.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using CodeBrix.Android.UI.Portable.Drawing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ACanvas = global::Android.Graphics.Canvas;
using AColorFilter = global::Android.Graphics.ColorFilter;
using ADrawable = global::Android.Graphics.Drawables.Drawable;
using APaint = global::Android.Graphics.Paint;
using APath = global::Android.Graphics.Path;
using ARect = global::Android.Graphics.Rect;
using ARectF = global::Android.Graphics.RectF;

namespace CodeBrix.Android.UI.Platform.Drawables;

/// <summary>
/// The background of a WinUI Border / Panel / ContentPresenter / Control as one Android
/// drawable: the Background brush (solid or gradient) over the inner or outer edge
/// (BackgroundSizing), the BorderBrush over the ring between the outer rounded rectangle and
/// the inner one deflated by the PER-SIDE BorderThickness, and PER-CORNER CornerRadius.
/// Adapted from MAUI's BorderDrawable/MauiDrawable (whose drawing lived partly in the Java
/// PlatformDrawable); rewritten in C# for WinUI's border model.
/// </summary>
internal sealed class BorderDrawable : ADrawable
{
    private readonly APaint _fill = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private readonly APaint _stroke = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private readonly APath _outer = new();
    private readonly APath _inner = new();
    private readonly APath _ring = new();
    private Brush _background;
    private Brush _borderBrush;
    private Thickness _thickness;
    private CornerRadius _cornerRadius;
    private BackgroundSizing _sizing = BackgroundSizing.InnerBorderEdge;
    private double _density = 1;
    private bool _dirty = true;
    private bool _paintsFill;
    private bool _paintsStroke;
    private int _alpha = 255;

    /// <summary>The current geometry (valid after the first draw at a size).</summary>
    internal BorderGeometry Geometry { get; private set; }

    /// <summary>True when the drawable paints anything.</summary>
    internal bool PaintsAnything => _background != null || (_borderBrush != null && (_thickness.Left > 0 || _thickness.Top > 0 || _thickness.Right > 0 || _thickness.Bottom > 0));

    /// <summary>Updates what is drawn; redraws when anything changed.</summary>
    internal void Update(Brush background, Brush borderBrush, Thickness thickness, CornerRadius cornerRadius, BackgroundSizing sizing, double density)
    {
        _background = background;
        _borderBrush = borderBrush;
        _thickness = thickness;
        _cornerRadius = cornerRadius;
        _sizing = sizing;
        _density = density <= 0 ? 1 : density;
        _dirty = true;
        InvalidateSelf();
    }

    /// <summary>Forces the brushes to be re-read on the next draw (a brush's colour changed).</summary>
    internal void InvalidateBrushes()
    {
        _dirty = true;
        InvalidateSelf();
    }

    /// <inheritdoc />
    public override int Opacity => (int)global::Android.Graphics.Format.Translucent;

    /// <inheritdoc />
    public override void SetAlpha(int alpha)
    {
        _alpha = alpha;
        _dirty = true;
        InvalidateSelf();
    }

    /// <inheritdoc />
    public override void SetColorFilter(AColorFilter colorFilter)
    {
        _fill.SetColorFilter(colorFilter);
        _stroke.SetColorFilter(colorFilter);
        InvalidateSelf();
    }

    /// <inheritdoc />
    public override void Draw(ACanvas canvas)
    {
        var bounds = Bounds;
        if (bounds.Width() <= 0 || bounds.Height() <= 0)
        {
            return;
        }

        if (_dirty)
        {
            Rebuild(bounds);
        }

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        if (_paintsFill)
        {
            canvas.DrawPath(GeometryFillsInner ? _inner : _outer, _fill);
        }

        if (_paintsStroke)
        {
            canvas.DrawPath(_ring, _stroke);
        }

        canvas.Restore();
    }

    /// <inheritdoc />
    protected override void OnBoundsChange(ARect bounds)
    {
        base.OnBoundsChange(bounds);
        _dirty = true;
    }

    private bool GeometryFillsInner => BorderGeometry.FillsInnerOnly(_sizing) && Geometry.HasBorder;

    private int MultiplyAlpha(int alpha) => _alpha >= 255 ? alpha : alpha * _alpha / 255;

    private void Rebuild(ARect bounds)
    {
        _dirty = false;
        var width = bounds.Width();
        var height = bounds.Height();
        var geometry = BorderGeometry.Compute(width, height, _thickness, _cornerRadius, _density);
        Geometry = geometry;

        _outer.Reset();
        using (var outerRect = new ARectF(0, 0, width, height))
        {
            _outer.AddRoundRect(outerRect, geometry.OuterRadii, APath.Direction.Cw);
        }

        _inner.Reset();
        using (var innerRect = new ARectF(geometry.InnerLeft, geometry.InnerTop, geometry.InnerRight, geometry.InnerBottom))
        {
            if (innerRect.Width() > 0 && innerRect.Height() > 0)
            {
                _inner.AddRoundRect(innerRect, geometry.InnerRadii, APath.Direction.Cw);
            }
        }

        _ring.Reset();
        _ring.SetFillType(APath.FillType.EvenOdd);
        _ring.AddPath(_outer);
        _ring.AddPath(_inner);

        var fillArea = GeometryFillsInner ? (w: geometry.InnerRight - geometry.InnerLeft, h: geometry.InnerBottom - geometry.InnerTop) : (w: (float)width, h: (float)height);
        _paintsFill = BrushShader.Apply(_fill, _background, width, height, _density) && fillArea.w > 0 && fillArea.h > 0;
        _fill.SetStyle(APaint.Style.Fill);
        _paintsStroke = geometry.HasBorder && BrushShader.Apply(_stroke, _borderBrush, width, height, _density);
        _stroke.SetStyle(APaint.Style.Fill);
        if (_alpha < 255)
        {
            _fill.Alpha = MultiplyAlpha(_fill.Alpha);
            _stroke.Alpha = MultiplyAlpha(_stroke.Alpha);
        }
    }
}
