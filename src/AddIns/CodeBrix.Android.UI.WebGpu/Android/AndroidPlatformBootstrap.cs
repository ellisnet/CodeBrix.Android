using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.Handlers;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using Microsoft.UI.Xaml;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.WebGpu.Android;

/// <summary>
/// Registers the Android side of the WebGpu add-in: the frame timer the Core's scheduler runs on
/// (<see cref="IWebGpuFrameTimer"/>, one <see cref="WebGpuFrameTimerAndroidPlatform"/> per view) and the handler of the
/// <see cref="WebGpuRenderView"/> (the SkiaSharp.Views add-in's canvas-element handler, a native Skia view: the view IS
/// a canvas-host element whose visual comes from that add-in's factory), after the canvas add-in. Idempotent; runs as
/// the module initializer (the WebGpu Core loads this assembly by name the first time it needs the frame timer, and the
/// CodeBrix.Android.UI bootstrap loads it at start-up).
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Gets a value indicating whether the registrations have run.</summary>
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

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The Skia canvas first: the view paints through its canvas-host factory.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IWebGpuFrameTimer>())
            {
                ApiExtensibility.Register<FrameworkElement>(typeof(IWebGpuFrameTimer), owner => new WebGpuFrameTimerAndroidPlatform(owner));
            }

            // Decided per view when its handler is created (after the application's OnCreate, where the one-way latch is
            // set): with direct GPU rendering on, the view group that can host the DirectSurface path's SurfaceView under
            // the same canvas; otherwise exactly the canvas handler.
            CodeBrixHandlers.Register<WebGpuRenderView>(_ => DirectGpuRenderingMode.IsEnabled
                ? new WebGpuSurfaceHostHandler()
                : new SkiaCanvasElementHandler());
            _registered = true;
        }
    }
}
