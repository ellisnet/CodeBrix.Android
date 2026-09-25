using System;
using CodeBrix.Android.UI.Platform;
using ABitmap = global::Android.Graphics.Bitmap;
using ACanvas = global::Android.Graphics.Canvas;
using AContext = global::Android.Content.Context;
using APaint = global::Android.Graphics.Paint;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ARect = global::Android.Graphics.Rect;
using AViewTreeObserver = global::Android.Views.ViewTreeObserver;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Platform;

/// <summary>
/// The native view of a GLCanvasElement / SkiaGLCanvasElement: a view group that draws the element's read-back
/// picture across its bounds, under the element's XAML children (which it hosts as any panel does).
/// <para>
/// Before every drawing pass of the window it raises <see cref="Painting"/>: the moment the Skia heads call the
/// element's OnVisualPainting from their compositor. An element that was invalidated renders then (on the
/// dispatcher, as on every head); an element that was not does nothing, so the picture it last produced stays.
/// </para>
/// </summary>
internal sealed class GLPictureViewGroup : CodeBrixContentViewGroup, AViewTreeObserver.IOnPreDrawListener
{
    private readonly APaint _paint = new(APaintFlags.FilterBitmap);
    private readonly ARect _source = new();
    private readonly ARect _destination = new();
    private AViewTreeObserver _observer;

    internal GLPictureViewGroup(AContext context)
        : base(context)
    {
        // A view group skips its own OnDraw by default.
        SetWillNotDraw(false);
    }

    /// <summary>Raised before each drawing pass of the window while the view is attached.</summary>
    internal Action Painting { get; set; }

    /// <summary>The picture (top-down RGBA), drawn stretched across the view; null draws nothing.</summary>
    internal ABitmap Picture { get; set; }

    /// <summary>How many times the view drew a picture (diagnostics, gates).</summary>
    internal int DrawCount { get; private set; }

    /// <inheritdoc />
    public bool OnPreDraw()
    {
        Painting?.Invoke();
        return true;
    }

    /// <inheritdoc />
    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        _observer = ViewTreeObserver;
        _observer?.AddOnPreDrawListener(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromWindow()
    {
        if (_observer is { IsAlive: true })
        {
            _observer.RemoveOnPreDrawListener(this);
        }
        else
        {
            ViewTreeObserver?.RemoveOnPreDrawListener(this);
        }

        _observer = null;
        base.OnDetachedFromWindow();
    }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Picture is not { IsRecycled: false } picture || Width <= 0 || Height <= 0)
        {
            return;
        }

        _source.Set(0, 0, picture.Width, picture.Height);
        _destination.Set(0, 0, Width, Height);
        canvas.DrawBitmap(picture, _source, _destination, _paint);
        DrawCount++;
    }
}
