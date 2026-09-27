using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Android.UI.Portable.TextInput;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.TerminalView;
using CodeBrix.Platform.UI.TerminalView.Contracts;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.TerminalView.Android;

/// <summary>
/// Registers the Android side of the TerminalView add-in: the canvas supply (IRenderCanvasPlatform,
/// <see cref="RenderCanvasAndroidPlatform"/>), the handler of its drawing surface (the SkiaSharp.Views canvas-element
/// handler) and the soft-keyboard profile of TerminalControl (a terminal: every key at once, no suggestions), after the
/// canvas add-in whose canvas-host factory the surface paints through. Idempotent; runs as the module initializer
/// (TerminalView.Core loads this assembly by name the first time it needs its canvas, and the CodeBrix.Android.UI
/// bootstrap loads it at start-up).
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
        // The Skia canvas first: the drawing surface paints through its canvas-host factory.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IRenderCanvasPlatform>())
            {
                // One canvas supply for the process; it creates one element per terminal.
                var canvas = new RenderCanvasAndroidPlatform();
                CanvasPlatform = canvas;
                ApiExtensibility.Register(typeof(IRenderCanvasPlatform), _ => canvas);
            }

            CodeBrixHandlers.Register<TerminalCanvasElement>(_ => new TerminalCanvasHandler());
            CoreTextInput.RegisterProfile(typeof(TerminalControl), CoreTextInputProfile.Terminal);
            _registered = true;
        }
    }
}
