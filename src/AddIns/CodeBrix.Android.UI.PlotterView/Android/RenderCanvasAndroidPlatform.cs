using System;
using CodeBrix.Platform.UI.PlotterView.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.PlotterView.Android;

/// <summary>
/// The Android canvas supply of the PlotterView add-in (IRenderCanvasPlatform): one <see cref="PlotterCanvasElement"/>
/// per chart, painted by the control's engine (PlotHost.Paint: the model, the zoom rectangle, the tracker) on every draw
/// and repainted when the control invalidates it (a model update, a pan, a zoom, the tracker). The control in the Core
/// owns everything else - the model, the controller, the pointer and touch gestures, the key bindings.
/// </summary>
internal sealed class RenderCanvasAndroidPlatform : IRenderCanvasPlatform
{
    /// <inheritdoc />
    public FrameworkElement CreateRenderCanvas() => new PlotterCanvasElement();

    /// <inheritdoc />
    public void AddPaintHandler(FrameworkElement renderCanvas, Action<object, Size> paint) =>
        Canvas(renderCanvas).AddPaintHandler(paint);

    /// <inheritdoc />
    public void Invalidate(FrameworkElement renderCanvas) => Canvas(renderCanvas).Invalidate();

    /// <inheritdoc />
    public double GetScale(FrameworkElement renderCanvas) => Canvas(renderCanvas).DisplayScale;

    private static PlotterCanvasElement Canvas(FrameworkElement renderCanvas) =>
        renderCanvas as PlotterCanvasElement
        ?? throw new ArgumentException("The element was not created by this canvas supply.", nameof(renderCanvas));
}
