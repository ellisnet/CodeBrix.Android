using CodeBrix.Platform.OpenGL;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// A framework-free OpenGL renderer for previewing one <see cref="LoadedModel"/> with an
/// orbit camera. The lifecycle mirrors the Graphics3DGL <c>GLCanvasElement</c> contract:
/// the host element calls <see cref="Initialize"/> from its <c>Init(GL)</c>,
/// <see cref="Render"/> from <c>RenderOverride(GL)</c>, and <see cref="Uninitialize"/>
/// from <c>OnDestroy(GL)</c> — all on the GL thread. <see cref="SetModel"/> may be called
/// from any thread; the model is uploaded on the next render.
/// </summary>
public interface IModelSceneRenderer
{
    /// <summary>The orbit camera driven by the host's pointer/scroll input.</summary>
    OrbitCamera Camera { get; }

    /// <summary>The background clear color as linear RGBA in [0, 1].</summary>
    (float R, float G, float B, float A) BackgroundColor { get; set; }

    /// <summary>
    /// Sets the model to display (or <see langword="null"/> to clear). Takes effect on the
    /// next <see cref="Render"/>; when <paramref name="frameCamera"/> is true the camera is
    /// re-framed to the model's bounds at that time.
    /// </summary>
    void SetModel(LoadedModel? model, bool frameCamera = true);

    /// <summary>Compiles shaders and creates GL resources. Call once, on the GL thread.</summary>
    void Initialize(GL gl);

    /// <summary>Renders the scene into the currently bound framebuffer at the given pixel size.</summary>
    void Render(GL gl, uint width, uint height);

    /// <summary>Deletes all GL resources. Call once when the canvas is destroyed, on the GL thread.</summary>
    void Uninitialize(GL gl);
}
