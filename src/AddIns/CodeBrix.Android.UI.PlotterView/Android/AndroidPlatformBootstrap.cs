using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.PlotterView.Contracts;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.PlotterView.Android;

/// <summary>
/// Registers the Android side of the PlotterView add-in: the canvas supply (IRenderCanvasPlatform,
/// <see cref="RenderCanvasAndroidPlatform"/>) and the handler of its drawing surface
/// (<see cref="PlotterCanvasHandler"/>, a leaf SkiaSharp.Views canvas view), after the canvas add-in whose view the
/// surface paints on.
/// Idempotent; runs as the module initializer (PlotterView.Core loads this assembly by name the first time it needs
/// its canvas, and the CodeBrix.Android.UI bootstrap loads it at start-up).
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

    /// <summary>The registered canvas supply (null until registered, or when another assembly registered one first).</summary>
    internal static RenderCanvasAndroidPlatform CanvasPlatform { get; private set; }

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The Skia canvas first: the drawing surface is its native view. (The font source is the TextLayout add-in's; the
        // PlotterView Core loads that assembly by name when the engine first asks for a typeface.)
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IRenderCanvasPlatform>())
            {
                // One canvas supply for the process; it creates one element per chart.
                var canvas = new RenderCanvasAndroidPlatform();
                CanvasPlatform = canvas;
                ApiExtensibility.Register(typeof(IRenderCanvasPlatform), _ => canvas);
            }

            CodeBrixHandlers.Register<PlotterCanvasElement>(_ => new PlotterCanvasHandler());
            _registered = true;
        }
    }
}
