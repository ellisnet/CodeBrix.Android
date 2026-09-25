namespace CodeBrix.Android.WinUI.Graphics3DGL.Portable;

/// <summary>
/// The EGL attribute lists of the Android off-screen context (pure data, host-free tested): an OpenGL ES 3 context
/// on a 1x1 pbuffer. The pbuffer only makes the context current; GLCanvasElement renders into its own framebuffer
/// object and SkiaGLCanvasElement into a Skia GPU surface, so no depth or stencil is asked of the pbuffer.
/// </summary>
internal static class EglAttributes
{
    /// <summary>EGL_NONE (list terminator).</summary>
    internal const int None = 0x3038;

    /// <summary>EGL_SUCCESS.</summary>
    internal const int Success = 0x3000;

    /// <summary>EGL_RED_SIZE.</summary>
    internal const int RedSize = 0x3024;

    /// <summary>EGL_GREEN_SIZE.</summary>
    internal const int GreenSize = 0x3023;

    /// <summary>EGL_BLUE_SIZE.</summary>
    internal const int BlueSize = 0x3022;

    /// <summary>EGL_ALPHA_SIZE.</summary>
    internal const int AlphaSize = 0x3021;

    /// <summary>EGL_SURFACE_TYPE.</summary>
    internal const int SurfaceType = 0x3033;

    /// <summary>EGL_PBUFFER_BIT.</summary>
    internal const int PbufferBit = 0x0001;

    /// <summary>EGL_RENDERABLE_TYPE.</summary>
    internal const int RenderableType = 0x3040;

    /// <summary>EGL_OPENGL_ES3_BIT (EGL_OPENGL_ES3_BIT_KHR).</summary>
    internal const int OpenGlEs3Bit = 0x0040;

    /// <summary>EGL_CONTEXT_CLIENT_VERSION (EGL_CONTEXT_MAJOR_VERSION).</summary>
    internal const int ContextClientVersion = 0x3098;

    /// <summary>EGL_WIDTH.</summary>
    internal const int Width = 0x3057;

    /// <summary>EGL_HEIGHT.</summary>
    internal const int Height = 0x3056;

    /// <summary>EGL_OPENGL_ES_API.</summary>
    internal const uint OpenGlEsApi = 0x30A0;

    /// <summary>EGL_DRAW.</summary>
    internal const int Draw = 0x3059;

    /// <summary>EGL_READ.</summary>
    internal const int Read = 0x305A;

    /// <summary>The OpenGL ES major version the context is created at (the Core's floor is 3.0).</summary>
    internal const int ClientVersion = 3;

    /// <summary>The config: 8-bit RGBA, pbuffer-capable, OpenGL ES 3 renderable.</summary>
    internal static int[] Config() =>
    [
        RenderableType, OpenGlEs3Bit,
        SurfaceType, PbufferBit,
        RedSize, 8,
        GreenSize, 8,
        BlueSize, 8,
        AlphaSize, 8,
        None,
    ];

    /// <summary>The context: OpenGL ES <see cref="ClientVersion"/>.</summary>
    internal static int[] Context() => [ContextClientVersion, ClientVersion, None];

    /// <summary>The 1x1 pbuffer the context is made current on.</summary>
    internal static int[] Pbuffer() => [Width, 1, Height, 1, None];
}
