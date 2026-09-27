using System;
using System.Numerics;
using CodeBrix.Platform.OpenGL;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// The OpenGL implementation of <see cref="IModelRenderEngine"/> - the app's default 3D
/// graphics backend, available on every head. Each frame it:
/// <list type="number">
///   <item>makes an off-screen native GL context current (Graphics3DGL's
///   <see cref="OffscreenGLContext"/>);</item>
///   <item>draws the model with the shader-based <see cref="GlModelSceneRenderer"/> into a
///   framebuffer object (FBO) with colour + depth attachments;</item>
///   <item>reads the pixels back to CPU memory with <c>glReadPixels</c>;</item>
///   <item>restores the host head's GL context.</item>
/// </list>
/// This whole class is the swappable, API-specific layer: a Vulkan engine would replace the
/// context, the renderer, and the readback here while implementing the same interface, and the
/// painter/camera/loaders/UI above it would not change.
/// <para>
/// The context comes from <see cref="OffscreenGLContext"/>, which resolves the same per-head
/// native OpenGL machinery that backs Graphics3DGL's on-screen <c>GLCanvasElement</c> - WGL on
/// the Windows heads, GLX on X11, EGL on Wayland/FrameBuffer, CGL on macOS - so this engine
/// renders on every head, not just Linux. The app never P/Invokes a platform GL loader itself.
/// </para>
/// <para>
/// The context is created <b>lazily</b> on the first <see cref="RenderFrame"/> call, because a GL
/// context must be created and used on the same thread that renders (the UI thread, inside the
/// Skia paint callback). Constructing the engine itself is cheap and thread-agnostic - it only
/// allocates the CPU-side renderer (and its camera) and captures the <see cref="XamlRoot"/>
/// accessor the context is created from.
/// </para>
/// </summary>
public sealed class OpenGlModelRenderEngine : IModelRenderEngine
{
    // The framework-free shader renderer. It needs a live GL handle and a bound framebuffer;
    // it owns the OrbitCamera and knows nothing about the context, framebuffers, or Skia.
    private readonly GlModelSceneRenderer _renderer = new();

    // Supplies the XamlRoot the offscreen context is associated with. Invoked lazily on the
    // render thread at first paint (never at construction), by which point the hosting page has
    // set it. OffscreenGLContext resolves the native GL wrapper the head registered for this root.
    private readonly Func<XamlRoot> _getXamlRoot;

    // The off-screen native GL context; null until the first render creates it on the GL thread.
    private OffscreenGLContext _context;
    private bool _rendererInitialized;

    // The off-screen framebuffer we render into, plus its colour and depth renderbuffers. These
    // are recreated whenever the requested size changes.
    private uint _framebuffer;
    private uint _colorBuffer;
    private uint _depthBuffer;
    private uint _fbWidth;
    private uint _fbHeight;

    private bool _disposed;

    /// <summary>
    /// Creates the engine. The <paramref name="getXamlRoot"/> accessor is invoked lazily on the
    /// render thread at first paint to obtain the <see cref="XamlRoot"/> the offscreen GL context
    /// is created from; it is never called here.
    /// </summary>
    /// <param name="getXamlRoot">Returns the hosting page's <see cref="XamlRoot"/>.</param>
    public OpenGlModelRenderEngine(Func<XamlRoot> getXamlRoot) =>
        _getXamlRoot = getXamlRoot ?? throw new ArgumentNullException(nameof(getXamlRoot));

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
        // Create the off-screen context on first use, from the hosting page's XamlRoot. This runs
        // on the render thread (the Skia paint callback), so the XamlRoot accessor is valid here.
        if (_context == null)
        {
            var xamlRoot = _getXamlRoot()
                ?? throw new InvalidOperationException(
                    "A XamlRoot is required to create the offscreen OpenGL context, but none was available.");
            if (!OffscreenGLContext.TryCreate(xamlRoot, out _context))
            {
                throw new InvalidOperationException(
                    "The running head does not provide a native OpenGL context for offscreen rendering.");
            }
        }

        // MakeCurrent() saves whatever context the host head had current and restores it when the
        // returned scope is disposed, so this engine never disturbs the head's own renderer even
        // though they share a thread.
        using (_context.MakeCurrent())
        {
            var gl = _context.Gl;

            // One-time GL setup: compile shaders and link the program.
            if (!_rendererInitialized)
            {
                _renderer.Initialize(gl);
                _rendererInitialized = true;
            }

            // Make sure we have a framebuffer (colour + depth) of the requested size.
            EnsureFramebuffer(gl, (uint)width, (uint)height);

            // Draw the model into our framebuffer over the requested background colour.
            _renderer.BackgroundColor = background;
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            _renderer.Render(gl, (uint)width, (uint)height);

            // Copy the rendered pixels from the GPU back to CPU memory (RGBA8). This GPU->CPU
            // sync is the main per-frame cost - it is why interaction drops frames rather than
            // lowering resolution.
            var pixels = new byte[width * height * 4];
            gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // OpenGL's first pixel row is the bottom of the image; flag that so the Skia bridge
            // flips it to match Skia's top-down surface.
            return new RenderedFrame(pixels, width, height, isBottomUp: true);
            // Disposing the MakeCurrent() scope hands the thread's GL state back to the host head.
        }
    }

    // Creates (or re-creates on a size change) the off-screen framebuffer: a colour renderbuffer
    // to draw into and read back, plus a depth renderbuffer so 3D geometry occludes correctly.
    private void EnsureFramebuffer(GL gl, uint width, uint height)
    {
        if (_framebuffer != 0 && _fbWidth == width && _fbHeight == height)
        {
            return;
        }

        DeleteFramebuffer(gl);

        _framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        _colorBuffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorBuffer);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, width, height);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, _colorBuffer);

        _depthBuffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent16, width, height);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, _depthBuffer);

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException("The offscreen framebuffer is incomplete.");
        }

        _fbWidth = width;
        _fbHeight = height;
    }

    private void DeleteFramebuffer(GL gl)
    {
        if (_colorBuffer != 0) { gl.DeleteRenderbuffer(_colorBuffer); _colorBuffer = 0; }
        if (_depthBuffer != 0) { gl.DeleteRenderbuffer(_depthBuffer); _depthBuffer = 0; }
        if (_framebuffer != 0) { gl.DeleteFramebuffer(_framebuffer); _framebuffer = 0; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;

        if (_context != null)
        {
            // GL resources must be freed on the GL thread with the context current; disposing the
            // MakeCurrent() scope restores the previously-current context afterwards.
            using (_context.MakeCurrent())
            {
                DeleteFramebuffer(_context.Gl);
                if (_rendererInitialized)
                {
                    _renderer.Uninitialize(_context.Gl);
                    _rendererInitialized = false;
                }
            }

            _context.Dispose();
            _context = null;
        }
    }
}
