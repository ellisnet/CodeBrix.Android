using System;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Graphics;
using CodeBrix.Platform.OpenGL;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace CodeBrix.Platform.WinUI.Graphics3DGL;

/// <summary>
/// An off-screen OpenGL context of the head, with a GL binding on it, for code that renders without a
/// GLCanvasElement (SkiaGLCanvasElement, SkiaGpuContext, application renderers). The same public type as the
/// CodeBrix.Platform flavor of the add-in (ported behaviour for behaviour from the pinned flavor assembly); on
/// Android the context is OpenGL ES 3 on an EGL pbuffer, so <see cref="CreateGrContext"/> takes the GLES path.
/// </summary>
public sealed class OffscreenGLContext : IDisposable
{
    private readonly INativeOpenGLWrapper _wrapper;
    private bool _disposed;

    private OffscreenGLContext(INativeOpenGLWrapper wrapper, GL gl)
    {
        _wrapper = wrapper;
        Gl = gl;
    }

    /// <summary>The GL binding on this context (make the context current before calling it).</summary>
    public GL Gl { get; }

    /// <summary>Creates an off-screen context for the window of <paramref name="xamlRoot"/>.</summary>
    /// <param name="xamlRoot">The XamlRoot the context is for.</param>
    /// <param name="context">The context, or null.</param>
    /// <returns>True when the head gave a context.</returns>
    public static bool TryCreate(XamlRoot xamlRoot, out OffscreenGLContext context)
    {
        context = null;
        if (xamlRoot == null)
        {
            return false;
        }

        if (!ApiExtensibility.CreateInstance<INativeOpenGLWrapper>(xamlRoot, out var wrapper) || wrapper == null)
        {
            return false;
        }

        return TryFinishCreate(wrapper, out context);
    }

    /// <summary>Makes the context current on the calling thread until the returned scope is disposed.</summary>
    /// <returns>The scope (restores the previously current context).</returns>
    public IDisposable MakeCurrent() => _wrapper.MakeCurrent();

    /// <summary>The address of a GL entry point (IntPtr.Zero when the driver has none).</summary>
    /// <param name="name">The entry point's name.</param>
    /// <returns>The address, or IntPtr.Zero.</returns>
    public IntPtr GetProcAddress(string name) => _wrapper.TryGetProcAddress(name, out var address) ? address : IntPtr.Zero;

    /// <summary>Creates a Skia GRContext on this context (GLES first on Android, then desktop GL).</summary>
    /// <returns>The GRContext.</returns>
    /// <exception cref="InvalidOperationException">Neither GL interface could be created.</exception>
    public GRContext CreateGrContext()
    {
        using (_wrapper.MakeCurrent())
        {
            return TryCreateGrContext(useGles: true)
                ?? TryCreateGrContext(useGles: false)
                ?? throw new InvalidOperationException(
                    "Failed to create a GRContext on the off-screen OpenGL context: both the GLES and the desktop-GL "
                    + "GRGlInterface/GRContext creation paths returned null.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        using (_wrapper.MakeCurrent())
        {
            Gl.Dispose();
        }

        _wrapper.Dispose();
    }

    private static bool TryFinishCreate(INativeOpenGLWrapper wrapper, out OffscreenGLContext context)
    {
        context = null;
        try
        {
            var gl = GL.GetApi(wrapper.GetProcAddress);
            context = new OffscreenGLContext(wrapper, gl);
            return true;
        }
        catch
        {
            wrapper.Dispose();
            throw;
        }
    }

    private GRContext TryCreateGrContext(bool useGles)
    {
        var glInterface = useGles ? GRGlInterface.CreateGles(GetProcAddress) : GRGlInterface.Create(GetProcAddress);
        if (glInterface == null)
        {
            return null;
        }

        var grContext = GRContext.CreateGl(glInterface);
        if (grContext == null)
        {
            glInterface.Dispose();
        }

        return grContext;
    }
}
