using System;
using System.Threading;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Graphics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Lottie.Android;

/// <summary>
/// The element a Lottie animation source puts into its AnimatedVisualPlayer on Android: a leaf element whose
/// composition visual is created by the Skia canvas-host factory (SKCanvasVisualBaseFactory, registered by the
/// SkiaSharp.Views add-in), so the SkiaSharp.Views canvas-element handler shows it as a native Skia view that runs
/// the source's render callback on each draw - one canvas unit = one DIP, clipped to the element - and redraws on
/// <see cref="Invalidate"/>.
/// </summary>
/// <remarks>
/// It is the Skia heads' Graphics2DSK SKCanvasElement (the Lottie twin's LottieSKCanvasElement) without the
/// Graphics2DSK dependency: the same visual, the same whole-box hit test, the same render callback, which the Core
/// hands over already typed for the platform's canvas (an SKCanvas passed as an object).
/// </remarks>
internal sealed partial class LottieCanvasElement : FrameworkElement
{
    private readonly Action<object, Size> _render;
    private SKCanvasVisualBase _canvasVisual;
    private int _paintCount;

    /// <summary>Creates the element around the source's render callback.</summary>
    /// <param name="render">Draws one frame: the canvas (an SKCanvas) and the area in DIPs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="render"/> is null.</exception>
    internal LottieCanvasElement(Action<object, Size> render)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
    }

    /// <summary>How many frames the element has painted (diagnostics and device fences).</summary>
    internal int PaintCount => Volatile.Read(ref _paintCount);

    /// <summary>Requests a repaint: the render callback runs again on the next draw.</summary>
    internal void Invalidate() => _canvasVisual?.Invalidate();

    /// <inheritdoc />
    private protected override ContainerVisual CreateElementVisual()
    {
        if (ApiExtensibility.CreateInstance<SKCanvasVisualBaseFactory>(this, out var factory))
        {
            return _canvasVisual = factory.CreateInstance(Paint, Compositor.GetSharedCompositor());
        }

        throw new InvalidOperationException(
            $"No {nameof(SKCanvasVisualBaseFactory)} is registered: the SkiaSharp.Views add-in must be registered before a Lottie animation is shown.");
    }

    /// <inheritdoc />
    internal override bool IsViewHit() => true;

    private void Paint(object canvas, Size area)
    {
        Interlocked.Increment(ref _paintCount);
        _render(canvas, area);
    }
}
