using System;
using CodeBrix.Android.SkiaSharp.Views.CanvasHost;
using CodeBrix.Android.SkiaSharp.Views.Platform;
using CodeBrix.Android.SkiaSharp.Views.Portable;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Foundation;
using AHandler = global::Android.OS.Handler;
using ALooper = global::Android.OS.Looper;

namespace CodeBrix.Android.SkiaSharp.Views.Handlers;

/// <summary>
/// The handler of every element that paints through the Skia canvas-host seam (SKCanvasHostElement:
/// Svg's SvgCanvas; Graphics2DSK's SKCanvasElement, registered by that add-in): a leaf
/// <see cref="SkiaCanvasView"/> that runs the element's paint (its composition visual's render
/// callback) on each draw, with one canvas unit = one DIP, clipped to the element; the element's
/// Invalidate() posts a redraw. The element owns its visuals (Core asks this handler to hit-test it:
/// its whole box, as on the Skia heads).
/// <para>
/// IMAGE CONTENT: Core puts an SvgImageSource's SvgCanvas inside the Image as its visual child, but the
/// Image's native view is a leaf ImageView that cannot hold a child view. Then this handler paints the
/// element into a bitmap of the Image's arranged pixel size (at the element's place in the Image) and
/// hands it to the Image's handler (ImageHandler.ShowChildContent), again after every Invalidate() and
/// every re-arrange.
/// </para>
/// </summary>
internal sealed class SkiaCanvasElementHandler : ViewHandler<FrameworkElement, SkiaCanvasView>
{
    /// <summary>The canvas element's mapper (only what every view maps).</summary>
    public static readonly PropertyMapper<FrameworkElement, SkiaCanvasElementHandler> Mapper = new(ViewMappers.ViewMapper);

    private readonly SkiaBitmapSurface _imageContent = new();
    private AHandler _mainThread;
    private bool _imageRenderPosted;

    /// <summary>Creates the handler.</summary>
    public SkiaCanvasElementHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals;

    /// <summary>How many times the element was painted as an Image's content (device tests).</summary>
    internal int ImageContentPaintCount { get; private set; }

    /// <inheritdoc />
    protected override SkiaCanvasView CreatePlatformView() => new(Context) { Painter = Paint };

    /// <inheritdoc />
    protected override void ConnectHandler(SkiaCanvasView platformView)
    {
        base.ConnectHandler(platformView);
        if (Element?.Visual is AndroidSKCanvasVisual visual)
        {
            visual.View = platformView;
            visual.Invalidated = HostImage() != null ? ScheduleImageRender : null;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(SkiaCanvasView platformView)
    {
        if (Element?.Visual is AndroidSKCanvasVisual visual && ReferenceEquals(visual.View, platformView))
        {
            visual.View = null;
            visual.Invalidated = null;
        }

        if (HostImage() is { Handler: ImageHandler imageHandler })
        {
            imageHandler.ShowChildContent(null);
        }

        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (!changed)
        {
            return;
        }

        if (HostImage() != null)
        {
            ScheduleImageRender();
        }
        else
        {
            NativeView?.Invalidate();
        }
    }

    private Image HostImage() =>
        Element != null && VisualTreeHelper.GetParent(Element) is Image image && image.Handler is ImageHandler ? image : null;

    private void ScheduleImageRender()
    {
        _mainThread ??= new AHandler(ALooper.MainLooper);
        lock (_imageContent)
        {
            if (_imageRenderPosted)
            {
                return;
            }

            _imageRenderPosted = true;
        }

        _mainThread.Post(RenderImageContent);
    }

    private void RenderImageContent()
    {
        lock (_imageContent)
        {
            _imageRenderPosted = false;
        }

        if (Element is not FrameworkElement element || HostImage() is not { Handler: ImageHandler imageHandler } image
            || !HasArranged || !CanvasSurfaceMath.IsPaintable(image.ActualWidth, image.ActualHeight))
        {
            return;
        }

        var density = CanvasSurfaceMath.CanvasScale(Density);
        var width = (int)Math.Round(image.ActualWidth * density);
        var height = (int)Math.Round(image.ActualHeight * density);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var place = ArrangedRect;
        _imageContent.EnsureSize(width, height, recycleOld: false);
        _imageContent.Paint((surface, info) =>
        {
            var canvas = surface.Canvas;
            canvas.RestoreToCount(1);
            canvas.ResetMatrix();
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(density);
            canvas.Translate((float)place.X, (float)place.Y);
            PaintElement(canvas, element);
        });
        ImageContentPaintCount++;
        imageHandler.ShowChildContent(_imageContent.Bitmap);
    }

    private void Paint(SKCanvas canvas, SKImageInfo info)
    {
        if (Element is FrameworkElement element)
        {
            canvas.Scale(CanvasSurfaceMath.CanvasScale(Density));
            PaintElement(canvas, element);
        }
    }

    private static void PaintElement(SKCanvas canvas, FrameworkElement element)
    {
        if (element.Visual is not AndroidSKCanvasVisual visual)
        {
            return;
        }

        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (!CanvasSurfaceMath.IsPaintable(width, height))
        {
            return;
        }

        canvas.ClipRect(SKRect.Create(0, 0, (float)width, (float)height));
        visual.Render(canvas, new Size(width, height));
    }
}
