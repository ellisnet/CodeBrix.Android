using System;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Native;
using SkiaSharp;

namespace WebGpuDemo.Rendering;

/// <summary>
/// The Renderer class page: an <see cref="IWebGpuRenderer"/> set on the view from XAML. Four squares - red, green,
/// blue and amber - turn slowly round the middle of the frame (one instanced draw of a WGSL quad), and the overlay
/// labels the picture. The view calls the renderer first and then raises its events, so a page can use both.
/// </summary>
public sealed class QuadrantSquaresRenderer : IWebGpuRenderer, IDisposable
{
    private const string Wgsl = """
        struct Params { angle: f32, aspect: f32, pad0: f32, pad1: f32 };
        @group(0) @binding(0) var<uniform> params: Params;

        struct Out { @builtin(position) position: vec4<f32>, @location(0) color: vec4<f32> };

        @vertex fn vs(@builtin(vertex_index) v: u32, @builtin(instance_index) i: u32) -> Out {
            var quad = array<vec2<f32>, 6>(vec2<f32>(-1.0, -1.0), vec2<f32>(1.0, -1.0), vec2<f32>(1.0, 1.0),
                                           vec2<f32>(-1.0, -1.0), vec2<f32>(1.0, 1.0), vec2<f32>(-1.0, 1.0));
            var centres = array<vec2<f32>, 4>(vec2<f32>(-0.45, 0.45), vec2<f32>(0.45, 0.45),
                                              vec2<f32>(-0.45, -0.45), vec2<f32>(0.45, -0.45));
            var colors = array<vec4<f32>, 4>(vec4<f32>(0.902, 0.224, 0.275, 1.0), vec4<f32>(0.165, 0.616, 0.561, 1.0),
                                             vec4<f32>(0.271, 0.482, 0.871, 1.0), vec4<f32>(0.914, 0.769, 0.416, 1.0));
            let c = cos(params.angle);
            let s = sin(params.angle);
            let p = centres[i] + quad[v] * 0.2;
            let turned = vec2<f32>(p.x * c - p.y * s, p.x * s + p.y * c);
            var out: Out;
            out.position = vec4<f32>(turned.x * params.aspect, turned.y, 0.0, 1.0);
            out.color = colors[i];
            return out;
        }

        @fragment fn fs(in: Out) -> @location(0) vec4<f32> { return in.color; }
        """;

    private readonly SKPaint _panel = new() { Color = new SKColor(0, 0, 0, 170), IsAntialias = true };
    private readonly SKPaint _text = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _font = new(SKTypeface.Default, 16);
    private WgslPipeline _pipeline;
    private long _framesRendered;
    private int _devicesReady;
    private volatile string _label = "IWebGpuRenderer";

    /// <summary>The turning speed in radians per second.</summary>
    public double RadiansPerSecond { get; set; } = 0.6;

    /// <summary>Frames this renderer has recorded (any thread may read it).</summary>
    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    /// <summary>How many devices this renderer has been handed (a new one after every reload or device loss).</summary>
    public int DevicesReady => Volatile.Read(ref _devicesReady);

    /// <summary>True between OnDeviceReady and OnDeviceLost.</summary>
    public bool HasPipeline => _pipeline is not null;

    /// <inheritdoc/>
    public void OnDeviceReady(IWebGpuDeviceSource device)
    {
        _pipeline?.Dispose();
        _pipeline = new WgslPipeline(device.Device, device.Queue, Wgsl, TextureFormat.BGRA8Unorm, 16);
        Interlocked.Increment(ref _devicesReady);
    }

    /// <inheritdoc/>
    public void OnRender(WebGpuFrame frame)
    {
        if (_pipeline is null) return;
        Span<float> parameters = stackalloc float[4]
        {
            (float)(frame.ElapsedSeconds * RadiansPerSecond),
            frame.Width > 0 ? (float)frame.Height / frame.Width : 1f,
            0f,
            0f,
        };
        _pipeline.WriteUniforms(parameters);
        _pipeline.Draw(frame.Encoder, frame.TextureView, 6, 4);
        var count = Interlocked.Increment(ref _framesRendered);
        // A string per second, not per frame: the overlay reads it on the head's paint.
        if (count % 60 == 1) _label = "IWebGpuRenderer - frame " + count;
    }

    /// <inheritdoc/>
    public void OnOverlay(SKCanvas canvas, SKSize size)
    {
        // The head's paint: SkiaSharp only, never a WebGPU object.
        canvas.DrawRoundRect(new SKRect(12, 12, 260, 44), 8, 8, _panel);
        canvas.DrawText(_label, 24, 34, SKTextAlign.Left, _font, _text);
    }

    /// <inheritdoc/>
    public void OnDeviceLost(string reason)
    {
        _pipeline?.Dispose();
        _pipeline = null;
    }

    /// <summary>Releases the pipeline and the overlay's paints.</summary>
    public void Dispose()
    {
        OnDeviceLost("disposed");
        _panel.Dispose();
        _text.Dispose();
        _font.Dispose();
    }
}
