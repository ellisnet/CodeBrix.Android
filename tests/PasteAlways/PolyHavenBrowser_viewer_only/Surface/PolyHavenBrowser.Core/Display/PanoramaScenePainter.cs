using System;
using SkiaSharp;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// Paints an interactive equirectangular HDRI panorama with the CPU panorama renderer:
/// each frame renders the current view into a reused bitmap and draws it onto the Skia
/// canvas. Pointer drag looks around; the wheel changes the field of view.
/// </summary>
public sealed class PanoramaScenePainter : IScenePainter
{
    private const float LookDegreesPerPixel = 0.2f;
    private const float ZoomDegreesPerNotch = 5f;

    //Cap the CPU ray-traced resolution so a maximized window stays interactive; the result
    //is scaled up to fill the canvas. A lower cap while dragging keeps the look-around smooth;
    //the crisp full cap is used once the drag stops.
    private const int MaxRenderDimension = 1440;
    private const int DragRenderDimension = 960;

    private readonly EquirectPanoramaRenderer _renderer;

    private SKBitmap _buffer;
    private int _bufferWidth;
    private int _bufferHeight;

    private bool _dragging;
    private double _lastX;
    private double _lastY;
    private bool _disposed;

    /// <summary>Creates a painter over a decoded equirectangular panorama.</summary>
    public PanoramaScenePainter(FloatImage panorama)
    {
        _renderer = new EquirectPanoramaRenderer(panorama ?? throw new ArgumentNullException(nameof(panorama)));
    }

    /// <inheritdoc />
    public void Paint(SKSurface surface, SKImageInfo info)
    {
        if (_disposed || info.Width <= 0 || info.Height <= 0)
        {
            return;
        }

        var (renderWidth, renderHeight) = CapResolution(info.Width, info.Height);
        if (_buffer == null || _bufferWidth != renderWidth || _bufferHeight != renderHeight)
        {
            _buffer?.Dispose();
            _buffer = new SKBitmap(new SKImageInfo(renderWidth, renderHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
            _bufferWidth = renderWidth;
            _bufferHeight = renderHeight;
        }

        _renderer.RenderTo(_buffer);
        surface.Canvas.DrawBitmap(_buffer, new SKRect(0, 0, info.Width, info.Height), new SKSamplingOptions(SKFilterMode.Linear));
    }

    /// <inheritdoc />
    public void PointerDown(double x, double y)
    {
        _dragging = true;
        _lastX = x;
        _lastY = y;
    }

    /// <inheritdoc />
    public void PointerDrag(double x, double y)
    {
        if (!_dragging) { return; }
        var deltaYaw = (float)(x - _lastX) * LookDegreesPerPixel;
        var deltaPitch = (float)(y - _lastY) * LookDegreesPerPixel;
        _lastX = x;
        _lastY = y;
        //Grab-and-drag feel: dragging right pulls the scene right (turns the view left);
        //dragging up looks up.
        _renderer.Camera.Rotate(-deltaYaw, deltaPitch);
    }

    private (int Width, int Height) CapResolution(int width, int height)
    {
        var max = _dragging ? DragRenderDimension : MaxRenderDimension;
        var longest = Math.Max(width, height);
        if (longest <= max)
        {
            return (width, height);
        }
        var scale = (double)max / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <inheritdoc />
    public void PointerSkip(double x, double y)
    {
        if (!_dragging) { return; }
        _lastX = x;
        _lastY = y;
    }

    /// <inheritdoc />
    public void PointerUp() => _dragging = false;

    /// <inheritdoc />
    public void Zoom(double wheelDelta) =>
        _renderer.Camera.Zoom(wheelDelta > 0 ? -ZoomDegreesPerNotch : ZoomDegreesPerNotch);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _buffer?.Dispose();
        _buffer = null;
    }
}
