using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.Handlers;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.WinUI.Graphics2DSK;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.WinUI.Graphics2DSK.Android;

/// <summary>
/// Registers the Android side of the Graphics2DSK add-in: the canvas add-in (whose SKCanvasVisualBaseFactory
/// SKCanvasElement paints through) and SKCanvasElement's handler - the SkiaSharp.Views canvas-element handler (a
/// leaf Skia view; the element's RenderOverride runs on each draw with one canvas unit = one DIP, clipped to the
/// element, on a canvas nobody cleared of what is under it). Idempotent; runs as the module initializer (the
/// CodeBrix.Android.UI bootstrap loads this assembly by name at start-up).
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
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            CodeBrixHandlers.Register<SKCanvasElement>(_ => new SkiaCanvasElementHandler());
            _registered = true;
        }
    }
}
