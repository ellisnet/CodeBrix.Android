using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.Platform;
using CodeBrix.Android.SkiaSharp.Views.Portable;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using SkiaSharp.Views.Windows.Contracts;
using ACanvas = global::Android.Graphics.Canvas;

namespace CodeBrix.Android.SkiaSharp.Views.Android;

/// <summary>
/// The Android implementation of <see cref="ISKXamlCanvasPlatform"/> (one per SKXamlCanvas, created by
/// the Core's constructor): <see cref="Invalidate"/> - the only thing that repaints an SKXamlCanvas -
/// marks the canvas dirty and asks its native view (<see cref="SKXamlCanvasViewGroup"/>) to draw; the
/// view's draw pass runs the PaintSurface handler on the kept pixel buffer (the buffer is reused
/// between paints, so a handler that does not clear draws over the previous frame) and draws it under
/// the canvas's XAML children. A draw pass that nothing invalidated shows the last painted buffer.
/// As the Skia twin does, the first paint gives the canvas an ImageBrush Background - what makes a
/// Panel hit-testable, so a touch reaches the canvas in the coordinates its handler paints in.
/// </summary>
internal sealed class SKXamlCanvasAndroidPlatform : ISKXamlCanvasPlatform
{
    private static readonly ConditionalWeakTable<SKXamlCanvas, SKXamlCanvasAndroidPlatform> _byOwner = new();

    private readonly SKXamlCanvas _owner;
    private readonly SkiaBitmapSurface _surface = new();
    private SKXamlCanvasViewGroup _view;
    private ImageBrush _hitTestBrush;
    private bool _dirty = true;

    /// <summary>Creates the platform of one canvas.</summary>
    /// <param name="owner">The canvas.</param>
    internal SKXamlCanvasAndroidPlatform(SKXamlCanvas owner)
    {
        _owner = owner;
        _byOwner.AddOrUpdate(owner, this);
    }

    /// <summary>How many times the PaintSurface handler has been run (device tests).</summary>
    internal int PaintCount { get; private set; }

    /// <summary>The platform of a canvas (null for a canvas built before the bootstrap ran).</summary>
    /// <param name="owner">The canvas.</param>
    /// <returns>The platform, or null.</returns>
    internal static SKXamlCanvasAndroidPlatform For(SKXamlCanvas owner) =>
        owner != null && _byOwner.TryGetValue(owner, out var platform) ? platform : null;

    /// <summary>Shows the canvas in <paramref name="view"/> (its handler connected).</summary>
    /// <param name="view">The native view.</param>
    internal void Attach(SKXamlCanvasViewGroup view)
    {
        _view = view;
        view.CanvasPlatform = this;
        _dirty = true;
        view.Invalidate();
    }

    /// <summary>Stops showing the canvas in <paramref name="view"/> (its handler disconnected).</summary>
    /// <param name="view">The native view.</param>
    internal void Detach(SKXamlCanvasViewGroup view)
    {
        if (view != null)
        {
            view.CanvasPlatform = null;
        }

        if (ReferenceEquals(_view, view))
        {
            _view = null;
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        if (SKXamlCanvas.IsInDesignMode || !_owner.IsShown)
        {
            return;
        }

        if (!CanvasSurfaceMath.IsPaintable(_owner.ActualWidth, _owner.ActualHeight))
        {
            _owner.SetCanvasSize(SKSize.Empty);
            return;
        }

        if (_hitTestBrush == null)
        {
            _hitTestBrush = new ImageBrush { AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, Stretch = Stretch.None };
            _owner.Background = _hitTestBrush;
        }

        _dirty = true;
        _view?.Invalidate();
    }

    /// <inheritdoc />
    public void Unload()
    {
        _surface.Free();
        _dirty = true;
    }

    /// <summary>
    /// The view's draw pass: paints the canvas when it was invalidated (or its pixel size changed), then
    /// draws the buffer.
    /// </summary>
    /// <param name="view">The native view.</param>
    /// <param name="canvas">The Android canvas of the draw pass.</param>
    internal void Draw(SKXamlCanvasViewGroup view, ACanvas canvas)
    {
        if (SKXamlCanvas.IsInDesignMode || !_owner.IsShown)
        {
            return;
        }

        var width = view.Width;
        var height = view.Height;
        if (width <= 0 || height <= 0 || !CanvasSurfaceMath.IsPaintable(_owner.ActualWidth, _owner.ActualHeight))
        {
            _owner.SetCanvasSize(SKSize.Empty);
            return;
        }

        var resized = _surface.EnsureSize(width, height);
        if (_dirty || resized)
        {
            _dirty = false;
            Paint(HandlerContext.Density(_owner));
        }

        _surface.DrawOn(canvas);
    }

    private void Paint(double density)
    {
        var ignorePixelScaling = _owner.IgnorePixelScaling;
        var (visibleWidth, visibleHeight) = CanvasSurfaceMath.UserVisibleSize(
            _surface.Width, _surface.Height, _owner.ActualWidth, _owner.ActualHeight, ignorePixelScaling);
        _owner.SetCanvasSize(new SKSize(visibleWidth, visibleHeight));

        _surface.Paint((surface, info) =>
        {
            var canvas = surface.Canvas;
            canvas.RestoreToCount(1);
            canvas.ResetMatrix();
            if (ignorePixelScaling)
            {
                canvas.Scale(CanvasSurfaceMath.CanvasScale(density));
                canvas.Save();
            }

            PaintCount++;
            _owner.RaisePaintSurface(new SKPaintSurfaceEventArgs(surface, info.WithSize(new SKSizeI(visibleWidth, visibleHeight)), info));
        });
    }
}
