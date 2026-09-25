using System.Numerics;
using CodeBrix.Platform.OpenGL;
using SkiaSharp;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Renders staged product shots of a model to PNG bytes, off screen. Give it a current
/// OpenGL context (the app hands one over from <c>OffscreenGLContext</c>; tests and tools
/// use a headless EGL context) and it drives a private <see cref="GlModelSceneRenderer"/>
/// into its own framebuffer: scene in, poster-resolution PNG out. Shots are rendered
/// supersampled and downscaled, standing in for the multisampling a print-quality still
/// deserves but a compatibility-first GL context may not offer.
/// </summary>
/// <remarks>
/// All members must be called on the thread where the owning GL context is current —
/// including <see cref="Dispose"/>, which frees the renderer's GL resources.
/// </remarks>
public sealed class ModelShotRenderer : IDisposable
{
    //Rendered pixels per output pixel, per axis. 2 gives 4 samples per output pixel.
    private const uint Supersample = 2;

    //A conservative ceiling for the supersampled framebuffer, kept below every desktop
    //  GL/GLES 3.0 implementation's minimum guarantees.
    private const uint MaxFramebufferSide = 4096;

    private readonly GL _gl;
    private readonly GlModelSceneRenderer _renderer = new();
    private bool _disposed;

    /// <summary>Creates the shot renderer on a current GL context.</summary>
    public ModelShotRenderer(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        _renderer.Initialize(gl);
    }

    /// <summary>
    /// Renders one product shot to PNG bytes. The scene is re-uploaded per call (its stage
    /// normals are re-aimed at this shot's light), so alternating between scenes is fine.
    /// </summary>
    /// <param name="scene">The staged scene from <see cref="ShotSceneBuilder.Build"/>.</param>
    /// <param name="stage">The stage the scene was built with (supplies the above-cove clear color).</param>
    /// <param name="angle">The camera/light preset for this shot.</param>
    /// <param name="width">The output PNG width in pixels.</param>
    /// <param name="height">The output PNG height in pixels.</param>
    public byte[] RenderPng(ShotScene scene, ShotStage stage, ShotAngle angle, uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(angle);
        ArgumentOutOfRangeException.ThrowIfZero(width);
        ArgumentOutOfRangeException.ThrowIfZero(height);

        //Aim the camera first: the headlight direction depends on the eye position.
        var camera = _renderer.Camera;
        camera.FovDegrees = 40f;
        camera.YawDegrees = angle.YawDegrees;
        camera.PitchDegrees = angle.PitchDegrees;
        camera.VerticalFramingBias = 0f;
        camera.Target = scene.Composite.Pivot ?? scene.Composite.BoundsCenter;
        FrameToSubject(camera, scene.SubjectPrimitives, width / (float)height, angle.FitMargin);

        //Light the shot; the stage's normals chase the light so it stays evenly lit.
        Vector3 lightDirection;
        if (angle.LightMode == ShotLightMode.Key)
        {
            lightDirection = angle.KeyLightDirection();
            _renderer.FixedLightDirection = lightDirection;
        }
        else
        {
            lightDirection = Vector3.Normalize(camera.GetEyePosition() - camera.Target);
            _renderer.FixedLightDirection = null;
        }
        scene.AimStageAtLight(lightDirection);

        //Anything above the cove's rim clears to the backdrop's top color, so the gradient
        //  continues seamlessly out of frame.
        _renderer.BackgroundColor = (
            stage.BackdropTop.R / 255f, stage.BackdropTop.G / 255f, stage.BackdropTop.B / 255f, 1f);

        //Re-set the scene every shot: the stage normals changed and must re-upload.
        _renderer.SetModel(scene.Composite, frameCamera: false);

        var frameWidth = Math.Min(width * Supersample, MaxFramebufferSide);
        var frameHeight = Math.Min(height * Supersample, MaxFramebufferSide);

        var pixels = RenderToPixels(frameWidth, frameHeight);
        return EncodePng(pixels, (int)frameWidth, (int)frameHeight, (int)width, (int)height);
    }

    //How much vertex mass may hang outside the frame on each side. Trimming the outer
    //  1.5% per side crops only sparse extremities (a strap's tail, an antenna tip), the
    //  way a product photographer fills the frame with the subject.
    private const float FramePercentile = 0.015f;

    //Cap on the vertices sampled for framing; big models are strided over.
    private const int MaxFramingSamples = 20000;

    //Frames the camera on the model's vertex mass: the subject's camera-space extents are
    //  measured at the (1.5, 98.5) percentiles, the look-at point recenters on that core,
    //  and the distance is solved so the core fills the frame with `margin` of padding.
    //  A bounding-volume fit would leave a wide, flat model (a camera with a sprawling
    //  strap) tiny in the frame; this fills the frame for any shape.
    private static void FrameToSubject(
        OrbitCamera camera, IReadOnlyList<ModelPrimitive> subject, float aspect, float margin)
    {
        var yaw = camera.YawDegrees * MathF.PI / 180f;
        var pitch = camera.PitchDegrees * MathF.PI / 180f;
        var toEye = new Vector3(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Cos(yaw));

        //The camera basis: forward looks at the target; right/up span the film plane.
        var forward = -toEye;
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);

        //Project every sampled vertex (relative to the current target) onto the basis.
        var totalVertices = 0;
        foreach (var primitive in subject) { totalVertices += primitive.VertexCount; }
        if (totalVertices == 0) { return; }
        var stride = Math.Max(1, totalVertices / MaxFramingSamples);

        var rights = new List<float>(totalVertices / stride + subject.Count);
        var ups = new List<float>(rights.Capacity);
        var forwards = new List<float>(rights.Capacity);
        foreach (var primitive in subject)
        {
            var positions = primitive.Positions;
            for (var v = 0; v < positions.Length; v += 3 * stride)
            {
                var point = new Vector3(positions[v], positions[v + 1], positions[v + 2]) - camera.Target;
                rights.Add(Vector3.Dot(point, right));
                ups.Add(Vector3.Dot(point, up));
                forwards.Add(Vector3.Dot(point, forward));
            }
        }

        var (rightLow, rightHigh) = PercentileRange(rights);
        var (upLow, upHigh) = PercentileRange(ups);
        var (forwardLow, forwardHigh) = PercentileRange(forwards);

        //Recenter the look-at point on the subject core, so the frame balances around the
        //  body of the model rather than its centroid.
        camera.Target += right * ((rightLow + rightHigh) * 0.5f) + up * ((upLow + upHigh) * 0.5f);
        var halfRight = (rightHigh - rightLow) * 0.5f;
        var halfUp = (upHigh - upLow) * 0.5f;

        var tanVertical = MathF.Tan(camera.FovDegrees * MathF.PI / 360f);
        var tanHorizontal = tanVertical * aspect;

        //A point at lateral offset |x| and depth (distance + along) is inside the frustum
        //  when |x| <= tan(fov/2) * (distance + along) — solve for the distance, taking
        //  the worst case over both film axes at both depth extremes.
        var required = camera.MinDistance;
        foreach (var along in (ReadOnlySpan<float>)[forwardLow, forwardHigh])
        {
            required = MathF.Max(required, halfRight * margin / tanHorizontal - along);
            required = MathF.Max(required, halfUp * margin / tanVertical - along);
        }

        camera.Distance = required;
    }

    private static (float Low, float High) PercentileRange(List<float> values)
    {
        values.Sort();
        var lowIndex = Math.Clamp((int)(values.Count * FramePercentile), 0, values.Count - 1);
        var highIndex = Math.Clamp(values.Count - 1 - lowIndex, 0, values.Count - 1);
        return (values[lowIndex], values[highIndex]);
    }

    private byte[] RenderToPixels(uint frameWidth, uint frameHeight)
    {
        var gl = _gl;
        var framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);

        var colorBuffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, colorBuffer);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, frameWidth, frameHeight);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, colorBuffer);

        var depthBuffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthBuffer);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent16, frameWidth, frameHeight);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, depthBuffer);

        try
        {
            var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException($"The shot framebuffer is incomplete: {status}.");
            }

            _renderer.Render(gl, frameWidth, frameHeight);

            var pixels = new byte[frameWidth * frameHeight * 4];
            gl.ReadPixels(0, 0, frameWidth, frameHeight, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());
            return pixels;
        }
        finally
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.DeleteRenderbuffer(colorBuffer);
            gl.DeleteRenderbuffer(depthBuffer);
            gl.DeleteFramebuffer(framebuffer);
        }
    }

    //Flips the GL-oriented (bottom-up) pixels the right way up, downscales the supersampled
    //  frame to the requested output size, and encodes a PNG.
    private static unsafe byte[] EncodePng(byte[] pixels, int frameWidth, int frameHeight, int width, int height)
    {
        using var frame = new SKBitmap(new SKImageInfo(frameWidth, frameHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var destination = new Span<byte>((void*)frame.GetPixels(), frame.ByteCount);
        var rowBytes = frameWidth * 4;
        for (var row = 0; row < frameHeight; row++)
        {
            var sourceRow = pixels.AsSpan((frameHeight - 1 - row) * rowBytes, rowBytes);
            sourceRow.CopyTo(destination.Slice(row * rowBytes, rowBytes));
        }

        using var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        frame.ScalePixels(output, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return encoded.ToArray();
    }

    /// <summary>Frees the renderer's GL resources. The owning context must be current.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _renderer.Uninitialize(_gl);
    }
}
