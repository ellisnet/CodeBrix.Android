using System;
using System.Diagnostics;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using SkiaSharp;

namespace WebGpuDemo.Rendering;

/// <summary>
/// The Controls page's heads-up display, drawn by the view's Overlay over every frame with SkiaSharp: the measured
/// frame rate, the render path and the frame driver, on a dark panel with an amber edge. <see cref="Update"/> runs on
/// the frame's thread; <see cref="Draw"/> runs on the head's paint and only reads the text it was given - never a
/// WebGPU object. The text changes at most four times a second, so the frame path allocates nothing.
/// </summary>
public sealed class HudOverlay : IDisposable
{
    /// <summary>The panel's fill (opaque, so the tests can find it).</summary>
    public static readonly SKColor PanelColor = new(16, 20, 28);

    /// <summary>The panel's edge colour.</summary>
    public static readonly SKColor EdgeColor = new(255, 200, 87);

    /// <summary>The panel's place and size in the view, in effective pixels.</summary>
    public static readonly SKRect PanelBounds = new(12, 12, 332, 76);

    private readonly SKPaint _fill = new() { Color = PanelColor, IsAntialias = true };
    private readonly SKPaint _edge = new() { Color = EdgeColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    private readonly SKPaint _ink = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _font = new(SKTypeface.Default, 16);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _windowStart;
    private int _framesInWindow;
    private double _fps;
    private WebGpuRenderPath _path;
    private WebGpuFrameDriver _driver;
    private volatile string _line1 = "fps -";
    private volatile string _line2 = "";

    /// <summary>The first line the HUD draws (the frame rate).</summary>
    public string Line1 => _line1;

    /// <summary>The second line the HUD draws (render path and driver).</summary>
    public string Line2 => _line2;

    /// <summary>The measured frame rate over the last quarter second.</summary>
    public double FramesPerSecond => Volatile.Read(ref _fps);

    /// <summary>Counts a frame and refreshes the text when something it shows changed (the frame's thread).</summary>
    /// <param name="path">The view's render path.</param>
    /// <param name="driver">The view's frame driver.</param>
    public void Update(WebGpuRenderPath path, WebGpuFrameDriver driver)
    {
        _framesInWindow++;
        var now = _clock.ElapsedTicks;
        var elapsed = (now - _windowStart) / (double)Stopwatch.Frequency;
        var changed = path != _path || driver != _driver;
        if (elapsed >= 0.25)
        {
            Volatile.Write(ref _fps, _framesInWindow / elapsed);
            _framesInWindow = 0;
            _windowStart = now;
            _line1 = "fps " + Math.Round(_fps).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (changed || _line2.Length == 0)
        {
            _path = path;
            _driver = driver;
            _line2 = "path " + path + "   driver " + driver;
        }
    }

    /// <summary>Shows a path or driver change at once, also when no frame follows (Manual, OnDemand).</summary>
    /// <param name="path">The view's render path.</param>
    /// <param name="driver">The view's frame driver.</param>
    public void Describe(WebGpuRenderPath path, WebGpuFrameDriver driver)
    {
        _path = path;
        _driver = driver;
        _line2 = "path " + path + "   driver " + driver;
    }

    /// <summary>Draws the panel (the head's paint: SkiaSharp only).</summary>
    /// <param name="canvas">The overlay canvas.</param>
    public void Draw(SKCanvas canvas)
    {
        canvas.DrawRoundRect(PanelBounds, 8, 8, _fill);
        canvas.DrawRoundRect(PanelBounds, 8, 8, _edge);
        canvas.DrawText(_line1, PanelBounds.Left + 14, PanelBounds.Top + 26, SKTextAlign.Left, _font, _ink);
        canvas.DrawText(_line2, PanelBounds.Left + 14, PanelBounds.Top + 50, SKTextAlign.Left, _font, _ink);
    }

    /// <summary>Releases the paints.</summary>
    public void Dispose()
    {
        _fill.Dispose();
        _edge.Dispose();
        _ink.Dispose();
        _font.Dispose();
    }
}
