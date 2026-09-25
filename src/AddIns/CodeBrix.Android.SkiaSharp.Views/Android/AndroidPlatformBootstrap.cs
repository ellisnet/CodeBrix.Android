using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.CanvasHost;
using CodeBrix.Android.SkiaSharp.Views.Handlers;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Graphics;
using SkiaSharp.Views.Windows;
using SkiaSharp.Views.Windows.Contracts;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.SkiaSharp.Views.Android;

/// <summary>
/// Registers the Android side of the SkiaSharp.Views add-in: the per-element canvas contracts
/// (ISKXamlCanvasPlatform, ISKSwapChainPanelPlatform), the Skia canvas-host seam's factory
/// (SKCanvasVisualBaseFactory - the one registration every Skia-drawing add-in paints through) and
/// the handlers of SKXamlCanvas and SKCanvasHostElement. Idempotent; runs as the module initializer
/// (the Core loads this assembly BY NAME and runs it; the CodeBrix.Android.UI bootstrap also loads it
/// at start-up) and from the bootstraps of the add-ins that draw on the canvas.
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>The canvas-host factory this assembly registers.</summary>
    internal static AndroidSKCanvasVisualFactory CanvasFactory { get; } = new();

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

    /// <summary>Registers every contract and handler of the add-in (once).</summary>
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

            ApiExtensibility.Register<SKXamlCanvas>(typeof(ISKXamlCanvasPlatform), canvas => new SKXamlCanvasAndroidPlatform(canvas));
            ApiExtensibility.Register(typeof(ISKSwapChainPanelPlatform), _ => new SKSwapChainPanelAndroidPlatform());
            if (!ApiExtensibility.IsRegistered<SKCanvasVisualBaseFactory>())
            {
                ApiExtensibility.Register(typeof(SKCanvasVisualBaseFactory), _ => CanvasFactory);
            }

            CodeBrixHandlers.Register<SKXamlCanvas>(_ => new SKXamlCanvasHandler());
            CodeBrixHandlers.Register<SKCanvasHostElement>(_ => new SkiaCanvasElementHandler());
            _registered = true;
        }
    }
}
