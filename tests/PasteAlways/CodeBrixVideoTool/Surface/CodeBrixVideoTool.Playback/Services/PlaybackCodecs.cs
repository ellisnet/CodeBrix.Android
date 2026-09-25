using CodeBrix.Audio.Opus;
using CodeBrix.VideoPlayback.Dav1d;

namespace CodeBrixVideoTool.Playback.Services;

/// <summary>
/// Turns on the two codecs this application's player needs, once, at start-up.
/// </summary>
/// <remarks>
/// <para>
/// These are the APPLICATION's dependencies, never the player add-in's: AV1 decoding is BSD-2-Clause
/// and Opus is BSD-3-Clause while the add-in is Apache-2.0, so each ships as its own package and an
/// application that wants them references them and calls Register once. The add-in resolves codecs
/// through the playback session's registries, so it plays them with no change and no reference of
/// its own.
/// </para>
/// <para>
/// Dav1d is not optional here: every one of the four supported formats carries AV1, so nothing plays
/// without it. Opus is needed for WebM, Matroska and Mode 1; Mode 2 carries Vorbis, which the
/// playback core decodes itself. There is deliberately no module initializer doing this - that would
/// work in a debug build and silently not run in a trimmed publish.
/// </para>
/// </remarks>
public static class PlaybackCodecs
{
    private static readonly object Gate = new();

    /// <summary>True once both codecs have been turned on.</summary>
    public static bool IsRegistered { get; private set; }

    /// <summary>
    /// Turns on AV1 video and Opus audio. Safe to call more than once; only the first call does
    /// anything.
    /// </summary>
    public static void RegisterOnce()
    {
        lock (Gate)
        {
            if (IsRegistered)
            {
                return;
            }

            CodeBrixVideoPlaybackDav1d.Register();
            CodeBrixAudioOpus.Register();
            IsRegistered = true;
        }
    }
}
