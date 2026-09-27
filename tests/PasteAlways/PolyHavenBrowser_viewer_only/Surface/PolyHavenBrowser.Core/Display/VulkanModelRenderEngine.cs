using System;
using System.Numerics;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// The Vulkan implementation of <see cref="IModelRenderEngine"/> - a thin adapter over the
/// Rendering library's self-contained <see cref="VulkanSceneRenderer"/> (Silk.NET.Vulkan).
/// Unlike the OpenGL engine there is no context to make current or restore: Vulkan has no
/// ambient thread state, so the renderer owns its whole stack (instance, device, offscreen
/// images, readback) and cannot disturb the host head's own renderer. All Vulkan resources
/// are still created lazily on the first <see cref="RenderFrame"/> (on the render thread)
/// and freed on <see cref="Dispose"/>.
/// <para>
/// Only construct this engine on a platform <see cref="VulkanPlatformSupport"/> okays -
/// that gate is applied by <see cref="ModelRenderEngineSelector"/>.
/// </para>
/// </summary>
public sealed class VulkanModelRenderEngine : IModelRenderEngine
{
    private readonly VulkanSceneRenderer _renderer = new();

    /// <inheritdoc />
    public OrbitCamera Camera => _renderer.Camera;

    /// <inheritdoc />
    public Vector3? FixedLightDirection
    {
        get => _renderer.FixedLightDirection;
        set => _renderer.FixedLightDirection = value;
    }

    /// <inheritdoc />
    public void SetModel(LoadedModel model) => _renderer.SetModel(model);

    /// <inheritdoc />
    public RenderedFrame RenderFrame(int width, int height, (float R, float G, float B, float A) background)
    {
        var pixels = _renderer.RenderFrame(width, height, background);

        // The renderer keeps the camera's GL-convention matrices unmodified, and Vulkan's
        // clip-space Y points the other way - so its readback is a bottom-up image just like
        // GL's, and the same Skia flip applies (see the VulkanSceneRenderer class remarks).
        return new RenderedFrame(pixels, width, height, isBottomUp: true);
    }

    /// <inheritdoc />
    public void Dispose() => _renderer.Dispose();
}

/// <summary>Creates <see cref="VulkanModelRenderEngine"/> instances.</summary>
public sealed class VulkanModelRenderEngineFactory : IModelRenderEngineFactory
{
    /// <inheritdoc />
    public IModelRenderEngine Create() => new VulkanModelRenderEngine();
}
