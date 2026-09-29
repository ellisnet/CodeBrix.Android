using System.Runtime.CompilerServices;
using CodeBrix.Android.SkiaSharp.Views.Handlers;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Audio.Android;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Contracts;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;
using AApplication = Android.App.Application;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.VideoPlayer.Android;

/// <summary>
/// Registers the Android side of the VideoPlayer add-in: CodeBrix.Audio's Android backend
/// (<see cref="CodeBrixAndroidAudio.Initialize"/>: a video's sound plays through CodeBrix.Audio's shared output), the Core's
/// IAssetLocation (<see cref="AssetLocationAndroidPlatform"/>) and the handler of the picture's surface
/// (<see cref="VideoSurfaceElement"/>: the SkiaSharp.Views add-in's canvas-element handler, a native Skia view), after the
/// canvas add-in whose canvas-host factory the surface paints through. Idempotent; runs as the module initializer (the
/// VideoPlayer Core loads this assembly by name the first time it needs IAssetLocation, and the CodeBrix.Android.UI
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

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The Skia canvas first: the picture paints through its canvas-host factory.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            // The Android audio backend before any session opens the shared output (harmless when repeated).
            CodeBrixAndroidAudio.Initialize(AApplication.Context);

            if (!ApiExtensibility.IsRegistered<IAssetLocation>())
            {
                var assets = new AssetLocationAndroidPlatform();
                ApiExtensibility.Register(typeof(IAssetLocation), _ => assets);
            }

            CodeBrixHandlers.Register<VideoSurfaceElement>(_ => new SkiaCanvasElementHandler());
            _registered = true;
        }
    }
}
