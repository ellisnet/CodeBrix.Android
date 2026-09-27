using System.Numerics;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// The Metal implementation of <see cref="IModelRenderEngine"/> - a thin adapter over the
/// Rendering library's self-contained <see cref="MetalSceneRenderer"/>, which renders
/// <b>direct to Metal</b> through the raw Objective-C runtime (no MoltenVK, no Silk.NET, no
/// NuGet packages). Like the Vulkan engine there is no context to make current or restore:
/// Metal has no ambient thread state, so the renderer owns its whole stack (device, queue,
/// pipelines, offscreen targets, readback) and cannot disturb the host head's own renderer.
/// All Metal resources are created lazily on the first <see cref="RenderFrame"/> and freed on
/// <see cref="Dispose"/>.
/// <para>
/// Only construct this engine on a platform <see cref="MetalPlatformSupport"/> okays (macOS on
/// Apple Silicon or Intel) - that gate is applied by <see cref="ModelRenderEngineSelector"/>.
/// </para>
/// </summary>
public sealed class MetalModelRenderEngine : IModelRenderEngine
{
    private readonly MetalSceneRenderer _renderer = new();

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

        // Unlike GL and Vulkan, Metal's clip-space Y points up while its framebuffer origin is
        // top-left, so the readback is already top-down - no Skia flip needed (see the
        // MetalSceneRenderer class remarks).
        return new RenderedFrame(pixels, width, height, isBottomUp: false);
    }

    /// <inheritdoc />
    public void Dispose() => _renderer.Dispose();
}

/// <summary>Creates <see cref="MetalModelRenderEngine"/> instances.</summary>
public sealed class MetalModelRenderEngineFactory : IModelRenderEngineFactory
{
    /// <inheritdoc />
    public IModelRenderEngine Create() => new MetalModelRenderEngine();
}
