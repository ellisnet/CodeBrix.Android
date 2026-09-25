using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.WinUI.Graphics3DGL.Handlers;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Graphics;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using CodeBrix.Platform.WinUI.Graphics3DGL.Contracts;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Android;

/// <summary>
/// Registers the Android Graphics3DGL platform: IGLCanvasPlatform (GLCanvasElement's PlatformContract loads this
/// assembly by name and runs this module initializer), the off-screen OpenGL context the Core asks the head for
/// (INativeOpenGLWrapper: an EGL / OpenGL ES 3 context, created per request - never before an element needs one),
/// and the handlers of GLCanvasElement and SkiaGLCanvasElement.
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>True once registered.</summary>
    internal static bool IsRegistered
    {
        get
        {
            lock (_gate)
            {
                return _registered;
            }
        }
    }

#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The framework first (a no-op once the application started it).
        UIBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            ApiExtensibility.Register(typeof(IGLCanvasPlatform), _ => new GLCanvasAndroidPlatform());
            if (!ApiExtensibility.IsRegistered<INativeOpenGLWrapper>())
            {
                ApiExtensibility.Register(typeof(INativeOpenGLWrapper), _ => new AndroidEglOpenGLWrapper());
            }

            CodeBrixHandlers.Register<GLCanvasElement>(_ => new GLCanvasHandler());
            CodeBrixHandlers.Register<SkiaGLCanvasElement>(_ => new SkiaGLCanvasHandler());
            _registered = true;
        }
    }
}
