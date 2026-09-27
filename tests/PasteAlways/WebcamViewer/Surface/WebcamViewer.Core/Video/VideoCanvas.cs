using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using WebcamViewer.ViewModels;

namespace WebcamViewer.Video;

/// <summary>
/// The SkiaSharp video surface the page hosts, so the XAML can say
/// <c>&lt;video:VideoCanvas /&gt;</c>. It is a plain <c>SKXamlCanvas</c> subclass that carries
/// no extra behavior of its own - the hosting page's code-behind wires PaintSurface to
/// <see cref="VideoCanvasHelper.RenderFrame"/>.
/// </summary>
public class VideoCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }

/// <summary>
/// Renders the most recent webcam frame onto a Skia surface, aspect-fit and centered on a
/// black background. The frame comes from an <see cref="IVideoFrameSource"/>, so nothing here
/// knows which view model produced it. Called from the canvas PaintSurface handler (always on
/// the UI thread, so the cached buffers need no locking of their own).
/// </summary>
public static class VideoCanvasHelper
{
    private static byte[] _frameBuffer;
    private static SKBitmap _bitmap;

    /// <summary>
    /// Renders the most recent frame; leaves the surface black when no frame is available.
    /// </summary>
    /// <param name="surface">The Skia surface to render onto.</param>
    /// <param name="info">The image info describing the surface.</param>
    /// <param name="frameSource">The frame source to pull from; nothing renders when null.</param>
    public static void RenderFrame(SKSurface surface, SKImageInfo info, IVideoFrameSource frameSource)
    {
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        if (frameSource == null
            || !frameSource.TryGetLatestFrame(ref _frameBuffer, out int width, out int height)
            || width <= 0 || height <= 0)
        {
            return;
        }

        if (_bitmap == null || _bitmap.Width != width || _bitmap.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        }
        Marshal.Copy(_frameBuffer, 0, _bitmap.GetPixels(), width * height * 4);

        float scale = Math.Min((float)info.Width / width, (float)info.Height / height);
        float destWidth = width * scale;
        float destHeight = height * scale;
        float destX = (info.Width - destWidth) / 2f;
        float destY = (info.Height - destHeight) / 2f;
        canvas.DrawBitmap(_bitmap, new SKRect(destX, destY, destX + destWidth, destY + destHeight),
            new SKSamplingOptions(SKFilterMode.Linear));
    }
}
