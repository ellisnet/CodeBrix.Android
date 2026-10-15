using System;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Native;

namespace WebGpuDemo.Rendering;

/// <summary>
/// The Triangle page's drawing, with nothing but the raw CodeBrix.WebGpu.Native binding the add-in brings: one WGSL
/// triangle in the middle of the frame whose colour walks round the colour wheel (a uniform written every frame). It
/// is drawn with LoadOp.Load, so the view's clear colour stays around it.
/// </summary>
public sealed class AnimatedTriangle : IDisposable
{
    private const string Wgsl = """
        struct Tint { color: vec4<f32> };
        @group(0) @binding(0) var<uniform> tint: Tint;

        @vertex fn vs(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {
            var corners = array<vec2<f32>, 3>(vec2<f32>(-0.55, -0.5), vec2<f32>(0.55, -0.5), vec2<f32>(0.0, 0.6));
            return vec4<f32>(corners[index], 0.0, 1.0);
        }

        @fragment fn fs() -> @location(0) vec4<f32> { return tint.color; }
        """;

    private readonly WgslPipeline _pipeline;

    /// <summary>Creates the pipeline on the view's device (call it from DeviceReady).</summary>
    /// <param name="device">The device the view handed over.</param>
    /// <param name="format">The view's PreferredFormat.</param>
    public AnimatedTriangle(IWebGpuDeviceSource device, TextureFormat format)
    {
        _pipeline = new WgslPipeline(device.Device, device.Queue, Wgsl, format, 16);
    }

    /// <summary>The colour of the last frame drawn, as 0-255 RGB (for the page's status line and the tests).</summary>
    public (byte R, byte G, byte B) LastColor { get; private set; }

    /// <summary>Records the triangle into the open frame, in the colour for the frame's time.</summary>
    /// <param name="frame">The open frame.</param>
    public void Draw(WebGpuFrame frame)
    {
        // A full turn of the colour wheel every four seconds, at full value: always bright, never the dark clear colour.
        var (r, g, b) = Hue((float)(frame.ElapsedSeconds * 0.25 % 1.0));
        Span<float> tint = stackalloc float[4] { r, g, b, 1f };
        _pipeline.WriteUniforms(tint);
        _pipeline.Draw(frame.Encoder, frame.TextureView, 3);
        LastColor = ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    /// <summary>Releases the pipeline.</summary>
    public void Dispose() => _pipeline.Dispose();

    // HSV with saturation 0.8 and value 1.
    private static (float R, float G, float B) Hue(float h)
    {
        const float s = 0.8f;
        var k = h * 6f;
        float Channel(float n)
        {
            var t = (n + k) % 6f;
            return 1f - s * Math.Clamp(Math.Min(t, 4f - t), 0f, 1f);
        }

        return (Channel(5f), Channel(3f), Channel(1f));
    }
}
