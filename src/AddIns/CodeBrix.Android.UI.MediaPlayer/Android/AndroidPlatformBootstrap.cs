using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.MediaPlayer.Handlers;
using CodeBrix.Android.UI.MediaPlayer.Hosting;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Media.Playback;
using Microsoft.UI.Xaml.Controls;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;
using WMediaPlayer = Windows.Media.Playback.MediaPlayer;

namespace CodeBrix.Android.UI.MediaPlayer.Android;

/// <summary>
/// Registers the Android side of the MediaPlayer add-in: IMediaPlayerExtension (per MediaPlayer, over Media3 ExoPlayer),
/// IMediaPlayerPresenterExtension (per MediaPlayerPresenter) and the handler of the video host element. Idempotent; runs
/// as the module initializer (the CodeBrix.Android.UI bootstrap loads this assembly by name at start-up).
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
        UIBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IMediaPlayerExtension>())
            {
                ApiExtensibility.Register<WMediaPlayer>(typeof(IMediaPlayerExtension), player => new AndroidMediaPlayerExtension(player));
            }

            if (!ApiExtensibility.IsRegistered<IMediaPlayerPresenterExtension>())
            {
                ApiExtensibility.Register<MediaPlayerPresenter>(typeof(IMediaPlayerPresenterExtension), presenter => new AndroidMediaPlayerPresenterExtension(presenter));
            }

            CodeBrixHandlers.Register<VideoHostElement>(_ => new VideoHostHandler());
            _registered = true;
        }
    }
}
