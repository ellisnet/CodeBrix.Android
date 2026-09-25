using System;
using SkiaSharp;
using ACanvas = global::Android.Graphics.Canvas;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.SkiaSharp.Views.Platform;

/// <summary>
/// A leaf native view that paints with Skia: every Android draw pass clears its pixel buffer and runs
/// <see cref="Painter"/> on it (the canvas-host seam's contract: the element paints its whole area
/// each time it is drawn), then draws the buffer.
/// </summary>
internal sealed class SkiaCanvasView : AView
{
    private readonly SkiaBitmapSurface _surface = new();

    /// <summary>Creates the view.</summary>
    /// <param name="context">The activity context.</param>
    internal SkiaCanvasView(AContext context)
        : base(context)
    {
    }

    /// <summary>The paint, called with a cleared canvas over the whole view and the buffer's info.</summary>
    internal Action<SKCanvas, SKImageInfo> Painter { get; set; }

    /// <summary>How many times the view has painted (diagnostics and device tests).</summary>
    internal int PaintCount { get; private set; }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Width <= 0 || Height <= 0 || Painter is not { } painter)
        {
            return;
        }

        _surface.EnsureSize(Width, Height);
        _surface.Paint((surface, info) =>
        {
            var skCanvas = surface.Canvas;
            skCanvas.RestoreToCount(1);
            skCanvas.ResetMatrix();
            skCanvas.Clear(SKColors.Transparent);
            painter(skCanvas, info);
        });
        PaintCount++;
        _surface.DrawOn(canvas);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromWindow()
    {
        base.OnDetachedFromWindow();
        _surface.Free();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface.Dispose();
        }

        base.Dispose(disposing);
    }
}
