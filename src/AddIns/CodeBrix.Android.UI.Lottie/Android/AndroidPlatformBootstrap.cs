using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.Handlers;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Lottie.Contracts;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.Lottie.Android;

/// <summary>
/// Registers the Android side of the Lottie add-in: the canvas supply (ILottieCanvasPlatform,
/// <see cref="LottieCanvasAndroidPlatform"/>), the frame clock (ILottieTickSourcePlatform, AP1.12:
/// <see cref="LottieTickSourceAndroidPlatform"/>, Choreographer frame callbacks) and the handler of its render surface (the SkiaSharp.Views
/// canvas-element handler), after the canvas add-in whose canvas-host factory the surface paints through.
/// Idempotent; runs as the module initializer (Lottie.Core loads this assembly by name the first time it needs its
/// canvas, and the CodeBrix.Android.UI bootstrap loads it at start-up).
/// </summary>
/// <remarks>
/// The animation-source provider (ILottieVisualSourceProvider, which the framework's ProgressRing asks for) is NOT
/// registered here: the Lottie Core declares it with an ApiExtension attribute, and an application's generated App
/// code registers it; registering it twice throws. On Android the ProgressRing is a native Material indicator and
/// does not ask for it.
/// </remarks>
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

    /// <summary>The registered canvas supply (null until registered).</summary>
    internal static LottieCanvasAndroidPlatform CanvasPlatform { get; private set; }

    /// <summary>The registered frame clock (AP1.12; null until registered, or when another one was registered first).</summary>
    internal static LottieTickSourceAndroidPlatform TickSourcePlatform { get; private set; }

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The Skia canvas first: the render surface paints through its canvas-host factory.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<ILottieCanvasPlatform>())
            {
                // One canvas supply for the process; it creates one element per animation.
                var canvas = new LottieCanvasAndroidPlatform();
                CanvasPlatform = canvas;
                ApiExtensibility.Register(typeof(ILottieCanvasPlatform), _ => canvas);
            }

            if (!ApiExtensibility.IsRegistered<ILottieTickSourcePlatform>())
            {
                // AP1.12 (WPE1-13 item b): the frame clock is the display's Choreographer, not a dispatcher timer.
                var ticks = new LottieTickSourceAndroidPlatform();
                TickSourcePlatform = ticks;
                ApiExtensibility.Register(typeof(ILottieTickSourcePlatform), _ => ticks);
            }

            CodeBrixHandlers.Register<LottieCanvasElement>(_ => new SkiaCanvasElementHandler());
            _registered = true;
        }
    }
}
