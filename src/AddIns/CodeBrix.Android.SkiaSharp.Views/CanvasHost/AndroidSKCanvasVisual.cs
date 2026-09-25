using System;
using CodeBrix.Platform.UI.Graphics;
using Microsoft.UI.Composition;
using SkiaSharp;
using Windows.Foundation;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.SkiaSharp.Views.CanvasHost;

/// <summary>
/// The Android visual of the Skia canvas-host seam (SKCanvasHostElement, Graphics2DSK's
/// SKCanvasElement): Core keeps it as the element's composition visual (the inert composition
/// paints nothing); the element's handler shows a native Skia view that calls
/// <see cref="Render"/> whenever Android draws it, and <see cref="Invalidate"/> asks that view to
/// draw again.
/// </summary>
internal sealed class AndroidSKCanvasVisual : SKCanvasVisualBase
{
    private AView _view;

    /// <summary>Creates the visual around the element's render callback.</summary>
    /// <param name="renderCallback">The element's paint (an SKCanvas, the element size in DIPs).</param>
    /// <param name="compositor">Core's shared compositor.</param>
    internal AndroidSKCanvasVisual(Action<object, Size> renderCallback, Compositor compositor)
        : base(renderCallback, compositor)
    {
    }

    /// <summary>The native view that shows this visual (null while no handler is connected).</summary>
    internal AView View
    {
        get => _view;
        set => _view = value;
    }

    /// <summary>Runs the element's paint on <paramref name="canvas"/> (one canvas unit = one DIP).</summary>
    /// <param name="canvas">The canvas, already scaled to the element's density and clipped to it.</param>
    /// <param name="area">The element's size in DIPs.</param>
    internal void Render(SKCanvas canvas, Size area) => RenderCallback?.Invoke(canvas, area);

    /// <summary>
    /// Called instead of invalidating <see cref="View"/> when the element is shown another way (an Image's
    /// content: see SkiaCanvasElementHandler); runs on any thread.
    /// </summary>
    internal Action Invalidated { get; set; }

    /// <summary>Repaints on the next frame (any thread: the native view is posted an invalidation).</summary>
    public override void Invalidate()
    {
        if (Invalidated is { } invalidated)
        {
            invalidated();
            return;
        }

        _view?.PostInvalidate();
    }
}

/// <summary>The SKCanvasVisualBaseFactory the Android bootstrap registers (one instance).</summary>
internal sealed class AndroidSKCanvasVisualFactory : SKCanvasVisualBaseFactory
{
    /// <inheritdoc />
    public SKCanvasVisualBase CreateInstance(Action<object, Size> renderCallback, Compositor compositor) =>
        new AndroidSKCanvasVisual(renderCallback, compositor);
}
