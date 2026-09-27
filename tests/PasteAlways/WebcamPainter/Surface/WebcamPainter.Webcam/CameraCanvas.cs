using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace WebcamPainter.Webcam;

/// <summary>
/// The SkiaSharp canvas control that displays live webcam video - a plain SKXamlCanvas
/// subclass so the XAML can say <c>&lt;webcam:CameraCanvas /&gt;</c>; the hosting page's
/// code-behind wires PaintSurface to a <see cref="WebcamFrameRenderer"/>.
/// </summary>
public class CameraCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }

/// <summary>
/// Renders a capture service's most recent webcam frame onto a Skia surface - aspect-fit,
/// centered on a black background, and optionally mirrored (selfie-style). Create one
/// renderer per canvas; the frame buffers it caches are reused across paints and are only
/// touched on the UI thread.
/// </summary>
public sealed class WebcamFrameRenderer
{
    private byte[] _frameBuffer;
    private SKBitmap _bitmap;

    /// <summary>
    /// Renders the most recent frame; leaves the surface black when no frame is available.
    /// </summary>
    /// <param name="surface">The Skia surface to render onto.</param>
    /// <param name="info">The image info describing the surface.</param>
    /// <param name="service">The capture service to pull the frame from; nothing renders when null.</param>
    /// <param name="mirror"><c>true</c> to flip the video left-to-right, like a mirror.</param>
    public void Render(SKSurface surface, SKImageInfo info, WebcamCaptureService service, bool mirror)
    {
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        if (service == null
            || !service.TryCopyLatestFrame(ref _frameBuffer, out int width, out int height)
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

        int restoreTo = canvas.Save();
        if (mirror)
        {
            canvas.Scale(-1, 1, destX + (destWidth / 2f), 0);
        }
        canvas.DrawBitmap(_bitmap, new SKRect(destX, destY, destX + destWidth, destY + destHeight),
            new SKSamplingOptions(SKFilterMode.Linear));
        canvas.RestoreToCount(restoreTo);
    }
}
