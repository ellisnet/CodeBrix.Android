using System;
using System.Numerics;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// A swappable 3D rendering engine: it draws a <see cref="LoadedModel"/> off-screen with an
/// <see cref="OrbitCamera"/> and hands back the resulting pixels, which the higher-level
/// <see cref="ModelScenePainter"/> then composites onto the app's Skia canvas.
/// <para>
/// This interface is the <b>seam that makes the graphics API replaceable</b>. Two engines
/// implement it - <see cref="OpenGlModelRenderEngine"/> (OpenGL via the head's native GL
/// context, the default) and <see cref="VulkanModelRenderEngine"/> (Vulkan via Silk.NET) - and the user picks between
/// them at runtime through <see cref="IModelRenderEngineSelector"/> with no change to the
/// painter, camera, mesh loading, or UI. Everything <i>below</i> this seam is
/// graphics-API-specific (GL/Vulkan contexts, shaders/pipelines, pixel readback); everything
/// <i>above</i> it (models, camera, pointer input, Skia compositing) is API-agnostic.
/// </para>
/// </summary>
public interface IModelRenderEngine : IDisposable
{
    /// <summary>
    /// The orbit camera the engine renders from. Drive it from pointer input, and set its
    /// framing (fov / margin / bias) before calling <see cref="SetModel"/>.
    /// </summary>
    OrbitCamera Camera { get; }

    /// <summary>
    /// A fixed world-space key-light direction for solid-shape shading, or <see langword="null"/>
    /// for a camera headlight (which double-sides the lighting, better for flat/foliage models).
    /// </summary>
    Vector3? FixedLightDirection { get; set; }

    /// <summary>
    /// Sets (or clears with <see langword="null"/>) the model to draw, re-framing the camera to
    /// it on the next render. Safe to call from any thread; the GPU upload happens at render time.
    /// </summary>
    void SetModel(LoadedModel model);

    /// <summary>
    /// Renders the current model at the given pixel size over the given background colour and
    /// returns the frame's pixels. Called on the render thread (the UI thread, from the Skia
    /// paint callback).
    /// </summary>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="background">The clear colour as linear RGBA in [0, 1]; an alpha of 0 clears transparent so a background can show through.</param>
    RenderedFrame RenderFrame(int width, int height, (float R, float G, float B, float A) background);
}

/// <summary>
/// The pixels produced by one <see cref="IModelRenderEngine.RenderFrame"/> call: tightly packed
/// RGBA bytes (4 per pixel, row-major).
/// </summary>
public readonly struct RenderedFrame
{
    /// <summary>Creates a rendered frame.</summary>
    /// <param name="rgba">The RGBA pixels (<paramref name="width"/> * <paramref name="height"/> * 4 bytes).</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="isBottomUp">Whether the first pixel row is the bottom of the image (OpenGL's convention).</param>
    public RenderedFrame(byte[] rgba, int width, int height, bool isBottomUp)
    {
        Rgba = rgba;
        Width = width;
        Height = height;
        IsBottomUp = isBottomUp;
    }

    /// <summary>The RGBA pixels (<see cref="Width"/> * <see cref="Height"/> * 4 bytes).</summary>
    public byte[] Rgba { get; }

    /// <summary>The frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>The frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// Whether the first pixel row is the bottom of the image. OpenGL reads bottom-up, so its
    /// engine returns <see langword="true"/> and the Skia bridge flips the image vertically to
    /// match Skia's top-down surface. A top-down engine (e.g. some Vulkan setups) returns false.
    /// </summary>
    public bool IsBottomUp { get; }
}
