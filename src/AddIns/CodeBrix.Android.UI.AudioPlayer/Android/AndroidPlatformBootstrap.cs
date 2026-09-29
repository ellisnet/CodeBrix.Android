using System.Runtime.CompilerServices;
using CodeBrix.Audio.Android;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts;
using AApplication = Android.App.Application;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.AudioPlayer.Android;

/// <summary>
/// Registers the Android side of the AudioPlayer add-in: CodeBrix.Audio's Android backend
/// (<see cref="CodeBrixAndroidAudio.Initialize"/>, so SharedAudioOutput and everything built on it - AudioFilePlayer,
/// SoundEffectClip, the MIDI synthesizers - plays through Android's audio path), then the three contracts the
/// AudioPlayer Core resolves: IAudioPlayerPlatform (one <see cref="AudioPlayerAndroidPlatform"/> per element),
/// IAudioOutputPlatform (<see cref="AudioOutputAndroidPlatform"/>) and IAssetLocation
/// (<see cref="AssetLocationAndroidPlatform"/>). Idempotent; runs as the module initializer (AudioPlayer.Core loads this
/// assembly by name the first time it needs a contract, and the CodeBrix.Android.UI bootstrap loads it at start-up).
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

            // The Android audio backend first: no player may open the shared output before it (harmless when repeated;
            // it opens no device and asks for nothing).
            CodeBrixAndroidAudio.Initialize(AApplication.Context);

            if (!ApiExtensibility.IsRegistered<IAudioPlayerPlatform>())
            {
                ApiExtensibility.Register(typeof(IAudioPlayerPlatform), _ => new AudioPlayerAndroidPlatform());
            }

            if (!ApiExtensibility.IsRegistered<IAudioOutputPlatform>())
            {
                var output = new AudioOutputAndroidPlatform();
                ApiExtensibility.Register(typeof(IAudioOutputPlatform), _ => output);
            }

            if (!ApiExtensibility.IsRegistered<IAssetLocation>())
            {
                var assets = new AssetLocationAndroidPlatform();
                ApiExtensibility.Register(typeof(IAssetLocation), _ => assets);
            }

            _registered = true;
        }
    }
}
