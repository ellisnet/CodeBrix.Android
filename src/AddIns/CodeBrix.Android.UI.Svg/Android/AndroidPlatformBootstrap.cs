using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Svg;
using CodeBrix.Platform.UI.Xaml.Media.Imaging.Svg;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.Svg.Android;

/// <summary>
/// Registers the Android side of the Svg add-in: the composition hookup every CodeBrix.Platform head makes
/// (ISvgProvider -> the Core's SvgProvider, which parses the SVG with CodeBrix.SkiaSvg and draws it on the
/// Skia canvas-host seam), after the canvas add-in it draws through. Idempotent; runs as the module
/// initializer (the CodeBrix.Android.UI bootstrap loads this assembly by name at start-up).
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
        // The Skia canvas first: SvgCanvas is an SKCanvasHostElement.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<ISvgProvider>())
            {
                ApiExtensibility.Register(typeof(ISvgProvider), owner => new SvgProvider(owner));
            }

            _registered = true;
        }
    }
}
