using CodeBrix.Android.SkiaSharp.Views.Platform;
using CodeBrix.Android.SkiaSharp.Views.Portable;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Windows.Foundation;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Android;

/// <summary>
/// The handler of an editor drawing surface (<see cref="EditorCanvasElement"/>): a leaf SkiaCanvasView of the
/// SkiaSharp.Views add-in whose every draw pass clears its pixel buffer and runs the element's paint handlers (the
/// Core's TextView / margin paint code) with one canvas unit = one DIP, clipped to the element; the element's
/// Invalidate() posts a redraw, and so does every re-arrange. The element owns its visuals (Core asks this handler to
/// hit-test it: its whole box, as on the Skia heads).
/// </summary>
internal sealed class EditorCanvasHandler : ViewHandler<EditorCanvasElement, SkiaCanvasView>
{
    /// <summary>The surface's mapper (only what every view maps).</summary>
    public static readonly PropertyMapper<EditorCanvasElement, EditorCanvasHandler> Mapper = new(ViewMappers.ViewMapper);

    /// <summary>Creates the handler.</summary>
    public EditorCanvasHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals;

    /// <inheritdoc />
    protected override SkiaCanvasView CreatePlatformView() => new(Context) { Painter = Paint };

    /// <inheritdoc />
    protected override void ConnectHandler(SkiaCanvasView platformView)
    {
        base.ConnectHandler(platformView);
        if (Surface is { } surface)
        {
            surface.Invalidated = platformView.PostInvalidate;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(SkiaCanvasView platformView)
    {
        if (Surface is { } surface)
        {
            surface.Invalidated = null;
        }

        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (changed)
        {
            NativeView?.Invalidate();
        }
    }

    private EditorCanvasElement Surface => Element as EditorCanvasElement;

    private void Paint(SKCanvas canvas, SKImageInfo info)
    {
        if (Surface is not { } surface)
        {
            return;
        }

        var width = surface.ActualWidth;
        var height = surface.ActualHeight;
        if (!CanvasSurfaceMath.IsPaintable(width, height) || surface.Visibility != Visibility.Visible)
        {
            return;
        }

        canvas.Scale(CanvasSurfaceMath.CanvasScale(Density));
        canvas.ClipRect(SKRect.Create(0, 0, (float)width, (float)height));
        surface.Paint(canvas, new Size(width, height));
    }
}
