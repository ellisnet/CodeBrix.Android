using System;
using CodeBrix.Platform.UI.AdvancedTextEdit.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Android;

/// <summary>
/// The Android canvas supply of the AdvancedTextEdit add-in (IRenderCanvasPlatform): one <see cref="EditorCanvasElement"/>
/// per drawing surface the editor makes (its TextView, the line-number, folding and dotted-line margins), painted by
/// the Core's own paint code on every draw and repainted when the Core invalidates it (a document change, the caret
/// blink, a selection, a scroll, a highlighting or option change). The editor in the Core owns everything else.
/// </summary>
internal sealed class RenderCanvasAndroidPlatform : IRenderCanvasPlatform
{
    /// <summary>How many surfaces this supply created (diagnostics and device fences).</summary>
    internal int Created { get; private set; }

    /// <inheritdoc />
    public FrameworkElement CreateRenderCanvas()
    {
        Created++;
        return new EditorCanvasElement();
    }

    /// <inheritdoc />
    public void AddPaintHandler(FrameworkElement renderCanvas, Action<object, Size> paint) =>
        Canvas(renderCanvas).AddPaintHandler(paint);

    /// <inheritdoc />
    public void Invalidate(FrameworkElement renderCanvas) => Canvas(renderCanvas).Invalidate();

    /// <inheritdoc />
    public double GetScale(FrameworkElement renderCanvas) => Canvas(renderCanvas).DisplayScale;

    private static EditorCanvasElement Canvas(FrameworkElement renderCanvas) =>
        renderCanvas as EditorCanvasElement
        ?? throw new ArgumentException("The element was not created by this canvas supply.", nameof(renderCanvas));
}
