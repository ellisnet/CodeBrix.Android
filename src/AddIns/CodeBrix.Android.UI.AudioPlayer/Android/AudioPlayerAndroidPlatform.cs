// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3):
// src/AddIns/Platform.UI.AudioPlayer.Skia/Skia/AudioPlayerSkiaPlatform.skia.cs (branch platform-split @ ea87ba01).
// The same code over CodeBrix.Audio's AudioFilePlayer, which CodeBrix.Audio.Android plays through Android's audio path.
// ANDROID PORT: Load(path) first copies an ms-appx asset out of the APK (PackagedAudioAssets).

using System;
using System.IO;
using CodeBrix.Audio.Playback;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts;

namespace CodeBrix.Android.UI.AudioPlayer.Android;

/// <summary>
/// The Android implementation of IAudioPlayerPlatform: one CodeBrix.Audio <see cref="AudioFilePlayer"/> per AudioPlayer
/// element, playing through the shared device output (CodeBrix.Audio.Android's engine).
/// </summary>
internal sealed class AudioPlayerAndroidPlatform : IAudioPlayerPlatform
{
    private readonly AudioFilePlayer _player = new();

    /// <inheritdoc/>
    public void Load(string filePath)
    {
        // ANDROID PORT: an ms-appx path is under the asset copy folder; copy the asset out on first use.
        PackagedAudioAssets.EnsureCopiedOut(filePath);
        _player.Load(filePath);
    }

    /// <inheritdoc/>
    public void Load(Stream stream) => _player.Load(stream);

    /// <inheritdoc/>
    public void Play() => _player.Play();

    /// <inheritdoc/>
    public void Pause() => _player.Pause();

    /// <inheritdoc/>
    public void Stop() => _player.Stop();

    /// <inheritdoc/>
    public void Seek(TimeSpan position) => _player.Seek(position);

    /// <inheritdoc/>
    public float Volume
    {
        get => _player.Volume;
        set => _player.Volume = value;
    }

    /// <inheritdoc/>
    public bool IsLooping
    {
        get => _player.IsLooping;
        set => _player.IsLooping = value;
    }

    /// <inheritdoc/>
    public TimeSpan Duration => _player.Duration;

    /// <inheritdoc/>
    public TimeSpan Position => _player.Position;

    /// <inheritdoc/>
    public event EventHandler PlaybackEnded
    {
        add => _player.PlaybackEnded += value;
        remove => _player.PlaybackEnded -= value;
    }
}
