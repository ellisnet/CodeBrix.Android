using AContext = global::Android.Content.Context;
using ACanvas = global::Android.Graphics.Canvas;
using APaint = global::Android.Graphics.Paint;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ADashPathEffect = global::Android.Graphics.DashPathEffect;
using AColor = global::Android.Graphics.Color;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Projection;

/// <summary>
/// One projected box: an element's own background fill and border stroke (as Core would
/// draw them), and - for a placeholder - a dashed outline, a label (type name + key
/// properties) and, for an Image, a diagonal cross. The paints are shared (UI thread only);
/// drawing allocates nothing.
/// </summary>
internal sealed class ProjectionBoxView : AView
{
    private static APaint _fill;
    private static APaint _stroke;
    private static APaint _outline;
    private static APaint _labelPaint;
    private static APaint _labelBackground;

    private int _fillColor;
    private int _strokeColor;
    private float _strokeWidth;
    private float _cornerRadius;
    private bool _isEllipse;
    private bool _isPlaceholder;
    private bool _isCrossed;
    private string _label;
    private float _density = 1;

    internal ProjectionBoxView(AContext context)
        : base(context)
    {
        EnsurePaints();
        Focusable = false;
        Clickable = false;
    }

    /// <summary>Sets what the box draws; invalidates only when something changed.</summary>
    internal void Set(int fillColor, int strokeColor, float strokeWidth, float cornerRadius, bool isEllipse,
        bool isPlaceholder, bool isCrossed, string label, float density)
    {
        if (_fillColor == fillColor && _strokeColor == strokeColor && _strokeWidth == strokeWidth
            && _cornerRadius == cornerRadius && _isEllipse == isEllipse && _isPlaceholder == isPlaceholder
            && _isCrossed == isCrossed && ReferenceEquals(_label, label) && _density == density)
        {
            return;
        }

        _fillColor = fillColor;
        _strokeColor = strokeColor;
        _strokeWidth = strokeWidth;
        _cornerRadius = cornerRadius;
        _isEllipse = isEllipse;
        _isPlaceholder = isPlaceholder;
        _isCrossed = isCrossed;
        _label = label;
        _density = density;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        float w = Width;
        float h = Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        if ((_fillColor >> 24) != 0)
        {
            _fill.Color = new AColor(_fillColor);
            DrawShape(canvas, _fill, 0, w, h);
        }

        if ((_strokeColor >> 24) != 0 && _strokeWidth > 0)
        {
            _stroke.Color = new AColor(_strokeColor);
            _stroke.StrokeWidth = _strokeWidth;
            DrawShape(canvas, _stroke, _strokeWidth / 2, w, h);
        }

        if (_isPlaceholder)
        {
            _outline.StrokeWidth = _density;
            canvas.DrawRect(_density / 2, _density / 2, w - (_density / 2), h - (_density / 2), _outline);
            if (_isCrossed)
            {
                canvas.DrawLine(0, 0, w, h, _outline);
                canvas.DrawLine(w, 0, 0, h, _outline);
            }

            var textSize = 9 * _density;
            if (_label != null && h >= textSize + (2 * _density))
            {
                _labelPaint.TextSize = textSize;
                var textWidth = System.Math.Min(_labelPaint.MeasureText(_label), w - (2 * _density));
                canvas.DrawRect(_density, _density, _density + textWidth + (2 * _density), (2 * _density) + textSize, _labelBackground);
                canvas.Save();
                canvas.ClipRect(0, 0, w - _density, h);
                canvas.DrawText(_label, 2 * _density, _density + (textSize * 0.9f), _labelPaint);
                canvas.Restore();
            }
        }
    }

    private void DrawShape(ACanvas canvas, APaint paint, float inset, float w, float h)
    {
        if (_isEllipse)
        {
            canvas.DrawOval(inset, inset, w - inset, h - inset, paint);
        }
        else if (_cornerRadius > 0)
        {
            canvas.DrawRoundRect(inset, inset, w - inset, h - inset, _cornerRadius, _cornerRadius, paint);
        }
        else
        {
            canvas.DrawRect(inset, inset, w - inset, h - inset, paint);
        }
    }

    private static void EnsurePaints()
    {
        if (_fill != null)
        {
            return;
        }

        _fill = new APaint(APaintFlags.AntiAlias);
        _fill.SetStyle(APaint.Style.Fill);
        _stroke = new APaint(APaintFlags.AntiAlias);
        _stroke.SetStyle(APaint.Style.Stroke);
        _outline = new APaint(APaintFlags.AntiAlias) { Color = new AColor(unchecked((int)0xB07E57C2)) };
        _outline.SetStyle(APaint.Style.Stroke);
        _outline.SetPathEffect(new ADashPathEffect(new[] { 6f, 4f }, 0));
        _labelPaint = new APaint(APaintFlags.AntiAlias) { Color = new AColor(unchecked((int)0xFF5E35B1)) };
        _labelBackground = new APaint { Color = new AColor(unchecked((int)0xD0FFFFFF)) };
        _labelBackground.SetStyle(APaint.Style.Fill);
    }
}
