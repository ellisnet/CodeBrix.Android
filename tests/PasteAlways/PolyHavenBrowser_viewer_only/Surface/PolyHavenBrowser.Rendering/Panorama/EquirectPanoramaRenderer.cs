using System.Numerics;
using SkiaSharp;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// A CPU renderer for interactive drag-to-look viewing of equirectangular HDR panoramas
/// (Poly Haven HDRIs). Each output pixel casts a camera ray, samples the panorama
/// bilinearly, and tone-maps to sRGB; rows render in parallel, which is fast enough for
/// interactive viewing at typical preview sizes on every CodeBrix.Platform head —
/// including heads with no GPU at all.
/// </summary>
public sealed class EquirectPanoramaRenderer
{
    private readonly FloatImage _panorama;

    /// <summary>Creates a renderer over a decoded equirectangular panorama.</summary>
    public EquirectPanoramaRenderer(FloatImage panorama)
    {
        _panorama = panorama ?? throw new ArgumentNullException(nameof(panorama));
    }

    /// <summary>The view state (yaw/pitch/fov) the next render uses.</summary>
    public PanoramaCamera Camera { get; } = new();

    /// <summary>The linear exposure multiplier applied before tone mapping. Defaults to 1.</summary>
    public float Exposure { get; set; } = 1f;

    /// <summary>The tone-map operator. Defaults to <see cref="ToneMapOperator.AcesFilmic"/>.</summary>
    public ToneMapOperator Operator { get; set; } = ToneMapOperator.AcesFilmic;

    /// <summary>Renders the current view into a new bitmap of the given size.</summary>
    public SKBitmap Render(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        RenderTo(bitmap);
        return bitmap;
    }

    /// <summary>
    /// Renders the current view into an existing RGBA bitmap (reuse the bitmap across
    /// frames while dragging to avoid per-frame allocations).
    /// </summary>
    public unsafe void RenderTo(SKBitmap target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.ColorType != SKColorType.Rgba8888)
        {
            throw new ArgumentException($"Target bitmap must be {SKColorType.Rgba8888}.", nameof(target));
        }

        var width = target.Width;
        var height = target.Height;

        // Camera basis from yaw/pitch: yaw 0 / pitch 0 looks down -Z, which samples the
        // horizontal center of the panorama.
        var yaw = Camera.YawDegrees * MathF.PI / 180f;
        var pitch = Camera.PitchDegrees * MathF.PI / 180f;
        var forward = new Vector3(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            -MathF.Cos(pitch) * MathF.Cos(yaw));
        var right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
        var up = Vector3.Cross(right, forward);

        var tanHalfFov = MathF.Tan(Camera.FovDegrees * MathF.PI / 360f);
        var aspect = (float)width / height;
        var exposure = Exposure;
        var op = Operator;

        var pixels = (byte*)target.GetPixels();

        Parallel.For(0, height, y =>
        {
            var ndcY = 1f - 2f * (y + 0.5f) / height; // +1 at top
            var rowUp = up * (ndcY * tanHalfFov);
            var dst = pixels + (long)y * width * 4;

            for (var x = 0; x < width; x++)
            {
                var ndcX = 2f * (x + 0.5f) / width - 1f;
                var direction = forward + right * (ndcX * tanHalfFov * aspect) + rowUp;

                SampleDirection(direction, out var r, out var g, out var b);
                ToneMapper.MapPixel(r, g, b, exposure, op, out dst[0], out dst[1], out dst[2]);
                dst[3] = 255;
                dst += 4;
            }
        });
    }

    /// <summary>
    /// Samples the panorama in the given world direction (need not be normalized) with
    /// bilinear filtering; X wraps, Y clamps.
    /// </summary>
    internal void SampleDirection(Vector3 direction, out float r, out float g, out float b)
    {
        var d = Vector3.Normalize(direction);

        // Equirect mapping: u from the heading (0.5 at -Z), v from the elevation (0 at top).
        var u = 0.5f + MathF.Atan2(d.X, -d.Z) / (2f * MathF.PI);
        var v = 0.5f - MathF.Asin(Math.Clamp(d.Y, -1f, 1f)) / MathF.PI;

        var fx = u * _panorama.Width - 0.5f;
        var fy = v * _panorama.Height - 0.5f;
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var tx = fx - x0;
        var ty = fy - y0;

        var x1 = WrapX(x0 + 1);
        var y1 = Math.Clamp(y0 + 1, 0, _panorama.Height - 1);
        x0 = WrapX(x0);
        y0 = Math.Clamp(y0, 0, _panorama.Height - 1);

        _panorama.GetPixel(x0, y0, out var r00, out var g00, out var b00);
        _panorama.GetPixel(x1, y0, out var r10, out var g10, out var b10);
        _panorama.GetPixel(x0, y1, out var r01, out var g01, out var b01);
        _panorama.GetPixel(x1, y1, out var r11, out var g11, out var b11);

        var w00 = (1f - tx) * (1f - ty);
        var w10 = tx * (1f - ty);
        var w01 = (1f - tx) * ty;
        var w11 = tx * ty;

        r = r00 * w00 + r10 * w10 + r01 * w01 + r11 * w11;
        g = g00 * w00 + g10 * w10 + g01 * w01 + g11 * w11;
        b = b00 * w00 + b10 * w10 + b01 * w01 + b11 * w11;
    }

    private int WrapX(int x)
    {
        var width = _panorama.Width;
        var wrapped = x % width;
        return wrapped < 0 ? wrapped + width : wrapped;
    }
}
