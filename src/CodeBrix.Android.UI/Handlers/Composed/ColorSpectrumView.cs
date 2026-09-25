using System;
using CodeBrix.Android.UI.Input;
using Microsoft.UI.Xaml.Controls;
using AColor = global::Android.Graphics.Color;
using AContext = global::Android.Content.Context;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native ColorPicker's spectrum (fresh; android.graphics): a square showing the two HSV channels of the
/// picker's ColorSpectrumComponents (rendered into a bitmap whenever the third channel changes, laid out as
/// WinUI lays the axes out - <see cref="ColorPickerMath.SpectrumPosition"/>), the selection ring at the current
/// colour, and a finger dragging the ring (<see cref="Picked"/> with the position as fractions).
/// </summary>
internal sealed class ColorSpectrumView : AView
{
    private const int BitmapSize = 96;
    private readonly global::Android.Graphics.Paint _bitmapPaint = new(global::Android.Graphics.PaintFlags.FilterBitmap);
    private readonly global::Android.Graphics.Paint _ring = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private readonly int[] _pixels = new int[BitmapSize * BitmapSize];
    private global::Android.Graphics.Bitmap _bitmap;
    private HsvColor _hsv;
    private ColorSpectrumComponents _components = ColorSpectrumComponents.HueSaturation;
    private double _renderedFraction = -1;
    private ColorSpectrumComponents _renderedComponents;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal ColorSpectrumView(AContext context)
        : base(context)
    {
        _ring.SetStyle(global::Android.Graphics.Paint.Style.Stroke);
        Clickable = true;
        ContentDescription = "Color spectrum";
    }

    /// <summary>Raised while a finger picks a colour: the position as fractions of the width and height.</summary>
    internal event EventHandler<(double X, double Y)> Picked;

    /// <summary>The largest side, in DIPs.</summary>
    internal const double MaxSide = 336;

    /// <summary>The side when the width is not constrained, in DIPs.</summary>
    internal const double DefaultSide = 256;

    /// <summary>Where the selection ring is, as fractions (for tests and diagnostics).</summary>
    internal (double X, double Y) RingPosition => ColorPickerMath.SpectrumPosition(_hsv, _components);

    /// <summary>Shows <paramref name="hsv"/> on the spectrum of <paramref name="components"/>.</summary>
    /// <param name="hsv">The colour.</param>
    /// <param name="components">The components.</param>
    internal void SetColor(HsvColor hsv, ColorSpectrumComponents components)
    {
        _hsv = hsv;
        _components = components;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        var mode = global::Android.Views.View.MeasureSpec.GetMode(widthMeasureSpec);
        var size = global::Android.Views.View.MeasureSpec.GetSize(widthMeasureSpec);
        var side = mode == global::Android.Views.MeasureSpecMode.Unspecified || size <= 0
            ? (int)Math.Round(DefaultSide * density)
            : Math.Min(size, (int)Math.Round(MaxSide * density));
        SetMeasuredDimension(side, side);
    }

    /// <inheritdoc />
    public override bool OnTouchEvent(global::Android.Views.MotionEvent e)
    {
        if (!Enabled || e == null || Width <= 0 || Height <= 0)
        {
            return base.OnTouchEvent(e);
        }

        switch (e.ActionMasked)
        {
            case AMotionEventActions.Down:
                Parent?.RequestDisallowInterceptTouchEvent(true);
                goto case AMotionEventActions.Move;
            case AMotionEventActions.Move:
            case AMotionEventActions.Up:
                NativeInput.MarkHandled();
                Picked?.Invoke(this, (Math.Clamp(e.GetX() / Width, 0, 1), Math.Clamp(e.GetY() / Height, 0, 1)));
                if (e.ActionMasked == AMotionEventActions.Up)
                {
                    Parent?.RequestDisallowInterceptTouchEvent(false);
                }

                return true;
            case AMotionEventActions.Cancel:
                Parent?.RequestDisallowInterceptTouchEvent(false);
                return true;
        }

        return base.OnTouchEvent(e);
    }

    /// <inheritdoc />
    protected override void OnDraw(global::Android.Graphics.Canvas canvas)
    {
        base.OnDraw(canvas);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var channel = ColorPickerMath.SliderChannel(_components);
        var fraction = ColorPickerMath.Fraction(_hsv, channel);
        if (_bitmap == null || Math.Abs(fraction - _renderedFraction) > 1e-4 || _renderedComponents != _components)
        {
            Render(fraction, channel);
        }

        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        var radius = 4 * density;
        using var rect = new global::Android.Graphics.RectF(0, 0, Width, Height);
        canvas.Save();
        using (var clip = new global::Android.Graphics.Path())
        {
            clip.AddRoundRect(rect, radius, radius, global::Android.Graphics.Path.Direction.Cw);
            canvas.ClipPath(clip);
        }

        canvas.DrawBitmap(_bitmap, null, rect, _bitmapPaint);
        canvas.Restore();

        var (x, y) = ColorPickerMath.SpectrumPosition(_hsv, _components);
        var cx = (float)(x * Width);
        var cy = (float)(y * Height);
        var ring = 8 * density;
        _ring.StrokeWidth = 3 * density;
        _ring.Color = new AColor(unchecked((int)0xFF000000));
        canvas.DrawCircle(cx, cy, ring, _ring);
        _ring.StrokeWidth = 1.5f * density;
        _ring.Color = new AColor(unchecked((int)0xFFFFFFFF));
        canvas.DrawCircle(cx, cy, ring, _ring);
    }

    private void Render(double fraction, ColorChannel channel)
    {
        _renderedFraction = fraction;
        _renderedComponents = _components;
        var third = ColorPickerMath.WithFraction(new HsvColor(0, 0, 0), channel, fraction);
        for (var row = 0; row < BitmapSize; row++)
        {
            for (var column = 0; column < BitmapSize; column++)
            {
                var hsv = ColorPickerMath.AtSpectrumPosition((column + 0.5) / BitmapSize, (row + 0.5) / BitmapSize, _components, third);
                var color = ColorPickerMath.ToColor(hsv);
                _pixels[(row * BitmapSize) + column] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }

        _bitmap ??= global::Android.Graphics.Bitmap.CreateBitmap(BitmapSize, BitmapSize, global::Android.Graphics.Bitmap.Config.Argb8888);
        _bitmap.SetPixels(_pixels, 0, BitmapSize, 0, 0, BitmapSize, BitmapSize);
    }
}
