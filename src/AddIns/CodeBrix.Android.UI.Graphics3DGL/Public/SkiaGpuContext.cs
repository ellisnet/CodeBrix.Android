using System;
using CodeBrix.Platform.Graphics;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace CodeBrix.Platform.WinUI.Graphics3DGL;

/// <summary>
/// A Skia GPU context of the head (a GRContext and the frame scope to draw with it). The same public type as the
/// CodeBrix.Platform flavor of the add-in; on Android it is always the OpenGL backend (an
/// <see cref="OffscreenGLContext"/>; the flavor's native Metal path does not exist here).
/// </summary>
public sealed class SkiaGpuContext : IDisposable
{
    private readonly OffscreenGLContext _glContext;
    private bool _disposed;

    private SkiaGpuContext(GRContext grContext, SkiaGpuBackend backend, OffscreenGLContext glContext)
    {
        GrContext = grContext;
        Backend = backend;
        _glContext = glContext;
    }

    /// <summary>The GRContext.</summary>
    public GRContext GrContext { get; }

    /// <summary>The backend (OpenGL on Android).</summary>
    public SkiaGpuBackend Backend { get; }

    /// <summary>Creates a GPU context for the window of <paramref name="xamlRoot"/>.</summary>
    /// <param name="xamlRoot">The XamlRoot the context is for.</param>
    /// <param name="context">The context, or null.</param>
    /// <returns>True when the head gave a GPU context.</returns>
    public static bool TryCreate(XamlRoot xamlRoot, out SkiaGpuContext context)
    {
        context = null;
        if (xamlRoot == null || !OffscreenGLContext.TryCreate(xamlRoot, out var glContext))
        {
            return false;
        }

        try
        {
            var grContext = glContext.CreateGrContext();
            context = new SkiaGpuContext(grContext, SkiaGpuBackend.OpenGL, glContext);
            return true;
        }
        catch
        {
            glContext.Dispose();
            return false;
        }
    }

    /// <summary>Makes the context current for a frame of drawing until the returned scope is disposed.</summary>
    /// <returns>The scope.</returns>
    public IDisposable BeginFrame() => _glContext.MakeCurrent();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        using (BeginFrame())
        {
            GrContext.Dispose();
        }

        _glContext.Dispose();
    }
}
