// STUB (paste always): PolyHavenBrowser.Rendering.VulkanSceneRenderer (CodeBrix.Samples PolyHavenBrowser_viewer_only
// src/libs/PolyHavenBrowser.Rendering/Vulkan/VulkanSceneRenderer.cs); the original draws through Silk.NET.Vulkan, a
// third-party package the paste-always heads may not take. Same namespace, type name and public members (the ones
// the app's VulkanModelRenderEngine calls). Never used on Android (VulkanPlatformSupport does not okay it there).
using System.Numerics;

namespace PolyHavenBrowser.Rendering;

/// <summary>Stand-in for the Vulkan offscreen scene renderer (compile-only).</summary>
public sealed class VulkanSceneRenderer : IDisposable
{
    /// <summary>The orbit camera driven by the host's pointer/scroll input.</summary>
    public OrbitCamera Camera { get; } = new();

    /// <summary>A fixed world-space light direction, or null for a camera headlight.</summary>
    public Vector3? FixedLightDirection { get; set; }

    /// <summary>Whether a usable Vulkan implementation is present at runtime (never, here).</summary>
    public static bool IsRuntimeAvailable() => false;

    /// <summary>Sets the model to display (or null to clear).</summary>
    public void SetModel(LoadedModel? model, bool frameCamera = true) { }

    /// <summary>Renders the current model (not supported here).</summary>
    public byte[] RenderFrame(int width, int height, (float R, float G, float B, float A) background) =>
        throw new PlatformNotSupportedException("Vulkan rendering is not available in this build.");

    /// <summary>Releases the renderer.</summary>
    public void Dispose() { }
}
