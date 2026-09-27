using System;
using System.Numerics;
using SkiaSharp;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// Paints a <see cref="LoadedModel"/> (a real 3D model, or a texture wrapped on a cube) onto
/// the app's Skia canvas. This is the <b>graphics-API-agnostic</b> half of the 3D preview: it
/// hands the actual drawing to an <see cref="IModelRenderEngine"/> (OpenGL today, Vulkan later)
/// and then composites the returned pixels onto the canvas - an optional darkened background
/// texture behind, the rendered model on top. Pointer drag orbits; the wheel zooms.
/// <para>
/// It always renders at full canvas resolution so the model stays crisp even when maximized;
/// smoothness under load comes from dropping frames (the view model coalesces paints and skips
/// stale pointer frames), not from lowering resolution.
/// </para>
/// </summary>
public sealed class ModelScenePainter : IScenePainter
{
    private const float OrbitDegreesPerPixel = 0.25f;

    // The dark solid background used when no background texture is set.
    private static readonly (float R, float G, float B, float A) SolidBackground = (0.13f, 0.13f, 0.15f, 1f);

    // The swappable 3D engine that actually draws the model and returns its pixels.
    private readonly IModelRenderEngine _engine;

    // Optional texture drawn (darkened, cover-scaled) behind the model; owned by this painter.
    private SKBitmap _backgroundBitmap;

    private bool _dragging;
    private double _lastX;
    private double _lastY;
    private bool _disposed;

    /// <summary>Creates a painter that renders through the given engine (typically from an <see cref="IModelRenderEngineFactory"/>).</summary>
    public ModelScenePainter(IModelRenderEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>The orbit camera; set its yaw/pitch/fov/margins before <see cref="SetModel"/> to frame from a chosen angle.</summary>
    public OrbitCamera Camera => _engine.Camera;

    /// <summary>A fixed world-space light direction for solid-shape shading, or null for a camera headlight.</summary>
    public Vector3? FixedLightDirection
    {
        get => _engine.FixedLightDirection;
        set => _engine.FixedLightDirection = value;
    }

    /// <summary>Sets (or clears) the model to display. Safe to call from any thread.</summary>
    public void SetModel(LoadedModel model) => _engine.SetModel(model);

    /// <summary>
    /// Sets a background texture drawn (darkened, cover-scaled) behind the model, or
    /// <see langword="null"/> for a solid dark background. Takes ownership of the bitmap.
    /// </summary>
    public void SetBackgroundTexture(SKBitmap bitmap)
    {
        var previous = _backgroundBitmap;
        _backgroundBitmap = bitmap;
        if (!ReferenceEquals(previous, bitmap))
        {
            previous?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Paint(SKSurface surface, SKImageInfo info)
    {
        if (_disposed || info.Width <= 0 || info.Height <= 0)
        {
            return;
        }

        // When a background texture is present, render the model over a transparent clear so it
        // composites onto the texture we draw behind it; otherwise use the solid dark colour.
        var hasBackground = _backgroundBitmap != null;
        var background = hasBackground ? (0f, 0f, 0f, 0f) : SolidBackground;

        // The engine does all the API-specific work and hands back RGBA pixels.
        var frame = _engine.RenderFrame(info.Width, info.Height, background);

        DrawFrame(surface, info, frame);
    }

    // Composites one engine frame onto the Skia canvas: the darkened background texture first,
    // then the rendered model (flipped if the engine's pixels are bottom-up, as OpenGL's are).
    private void DrawFrame(SKSurface surface, SKImageInfo info, RenderedFrame frame)
    {
        var canvas = surface.Canvas;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);

        DrawBackground(canvas, info, sampling);

        // Straight (unpremultiplied) alpha so a transparent clear lets the background show through.
        var imageInfo = new SKImageInfo(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var image = SKImage.FromPixelCopy(imageInfo, frame.Rgba);

        canvas.Save();
        if (frame.IsBottomUp)
        {
            // The engine's first pixel row is the bottom of the image; flip vertically to match
            // Skia's top-down surface.
            canvas.Scale(1f, -1f);
            canvas.Translate(0f, -info.Height);
        }
        canvas.DrawImage(image, new SKRect(0, 0, info.Width, info.Height), sampling);
        canvas.Restore();
    }

    // Draws the background texture cover-scaled (uniform, centered, cropped so it fills without
    // distortion) and darkened so the foreground model stands out against it.
    private void DrawBackground(SKCanvas canvas, SKImageInfo info, SKSamplingOptions sampling)
    {
        if (_backgroundBitmap == null)
        {
            return;
        }

        var scale = Math.Max((float)info.Width / _backgroundBitmap.Width, (float)info.Height / _backgroundBitmap.Height);
        var drawWidth = _backgroundBitmap.Width * scale;
        var drawHeight = _backgroundBitmap.Height * scale;
        var left = (info.Width - drawWidth) / 2f;
        var top = (info.Height - drawHeight) / 2f;

        using var paint = new SKPaint();
        // Multiply by a dark grey to darken the texture (Modulate = per-channel multiply).
        paint.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(0x4D, 0x4D, 0x4D), SKBlendMode.Modulate);
        canvas.DrawBitmap(_backgroundBitmap, new SKRect(left, top, left + drawWidth, top + drawHeight), sampling, paint);
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
        var deltaYaw = (float)(x - _lastX) * OrbitDegreesPerPixel;
        var deltaPitch = (float)(y - _lastY) * OrbitDegreesPerPixel;
        _lastX = x;
        _lastY = y;
        // Grab-and-drag feel: dragging right rolls the model's near face to the right, and
        // dragging up rolls its top toward you.
        _engine.Camera.Orbit(-deltaYaw, deltaPitch);
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
    public void Zoom(double wheelDelta) => _engine.Camera.Zoom(wheelDelta > 0 ? 0.9f : 1.1f);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;

        _engine.Dispose();
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
    }
}
