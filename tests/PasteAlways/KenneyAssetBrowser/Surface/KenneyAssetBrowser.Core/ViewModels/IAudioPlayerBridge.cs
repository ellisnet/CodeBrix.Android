using System;
using System.IO;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// The head-capability bridge for audio playback: the page fills these in with calls on its
/// AudioPlayer element. The view model must behave sensibly when a delegate is <c>null</c>.
/// </summary>
public interface IAudioPlayerBridge
{
    /// <summary>
    /// Hands the player a seekable stream of an audio file it can decode (Ogg Vorbis, WAV, MP3
    /// or FLAC); the player takes ownership of it.
    /// </summary>
    Action<Stream> LoadAudioSource { get; set; }

    /// <summary>Starts (or resumes) playback.</summary>
    Action PlayAudio { get; set; }

    /// <summary>Pauses playback, keeping the position.</summary>
    Action PauseAudio { get; set; }

    /// <summary>Stops playback and rewinds.</summary>
    Action StopAudio { get; set; }

    /// <summary>Sets whether playback loops.</summary>
    Action<bool> SetAudioLooping { get; set; }

    /// <summary>Whether the player is currently advancing.</summary>
    Func<bool> IsAudioPlaying { get; set; }

    /// <summary>The player's position within the clip.</summary>
    Func<TimeSpan> AudioPosition { get; set; }

    /// <summary>The loaded clip's duration.</summary>
    Func<TimeSpan> AudioDuration { get; set; }

    /// <summary>Moves the player to a position within the clip.</summary>
    Action<TimeSpan> SeekAudio { get; set; }
}
