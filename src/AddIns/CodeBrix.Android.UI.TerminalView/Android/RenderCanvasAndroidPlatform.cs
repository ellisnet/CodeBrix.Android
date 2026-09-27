using System;
using CodeBrix.Platform.UI.TerminalView.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.TerminalView.Android;

/// <summary>
/// The Android canvas supply of the TerminalView add-in (IRenderCanvasPlatform): one <see cref="TerminalCanvasElement"/>
/// per terminal, painted by the control's engine (TerminalRenderer.Paint) on every draw and repainted when the control
/// invalidates it (output fed, the cursor blink, a selection, a colour or font change). The control in the Core owns
/// everything else - the terminal, the gestures, the key encoding, the scroll bar and the clipboard.
/// </summary>
internal sealed class RenderCanvasAndroidPlatform : IRenderCanvasPlatform
{
    /// <inheritdoc />
    public FrameworkElement CreateRenderCanvas() => new TerminalCanvasElement();

    /// <inheritdoc />
    public void AddPaintHandler(FrameworkElement renderCanvas, Action<object, Size> paint) =>
        Canvas(renderCanvas).AddPaintHandler(paint);

    /// <inheritdoc />
    public void Invalidate(FrameworkElement renderCanvas) => Canvas(renderCanvas).Invalidate();

    /// <inheritdoc />
    public double GetScale(FrameworkElement renderCanvas) => Canvas(renderCanvas).DisplayScale;

    private static TerminalCanvasElement Canvas(FrameworkElement renderCanvas) =>
        renderCanvas as TerminalCanvasElement
        ?? throw new ArgumentException("The element was not created by this canvas supply.", nameof(renderCanvas));
}
