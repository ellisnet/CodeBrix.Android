using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using System;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Playback.Services;

/// <summary>
/// Everything the playback view model needs from the player element, and nothing it does not.
/// </summary>
/// <remarks>
/// The element itself is a XAML control and can only live in the view layer, so the page implements
/// this and hands it to the view model. Position, duration and volume are deliberately absent: those
/// are dependency properties on the element, and the scrubber and the volume slider bind straight to
/// them, which is both simpler and smoother than routing every tick through a view model. What the
/// view model owns is everything that is a decision rather than a value.
/// </remarks>
public interface IVideoPlayerSurface
{
    /// <summary>Opens a file and leaves it paused at the start.</summary>
    /// <param name="path">The file to open.</param>
    void Open(string path);

    /// <summary>Unloads whatever is open.</summary>
    void Close();

    /// <summary>Starts or resumes playback.</summary>
    void Play();

    /// <summary>Holds playback where it is.</summary>
    void Pause();

    /// <summary>Stops playback and returns to the start.</summary>
    void Stop();

    /// <summary>Jumps to the start of one chapter.</summary>
    /// <param name="index">The chapter's position in the file, from zero.</param>
    void SeekToChapter(int index);

    /// <summary>Shows one caption track, or none.</summary>
    /// <param name="track">The track to show, or null to show none.</param>
    void SelectCaptionTrack(CaptionTrack track);

    /// <summary>How long the open file runs.</summary>
    TimeSpan Duration { get; }

    /// <summary>Whether the open file is playing right now.</summary>
    bool IsPlaying { get; }

    /// <summary>The chapters the open file carries.</summary>
    IReadOnlyList<Chapter> Chapters { get; }

    /// <summary>The caption tracks the open file carries.</summary>
    IReadOnlyList<CaptionTrack> CaptionTracks { get; }

    /// <summary>Which chapter playback is inside now, or -1 when there are no chapters.</summary>
    int CurrentChapterIndex { get; }

    /// <summary>Raised once a file is open and its duration, chapters and captions are known.</summary>
    event EventHandler MediaOpened;

    /// <summary>Raised when playback reaches the end.</summary>
    event EventHandler PlaybackEnded;

    /// <summary>Raised when a file cannot be opened or played, with the reason.</summary>
    event EventHandler<string> MediaFailed;

    /// <summary>Raised when playback starts or stops, so the transport can enable and disable itself.</summary>
    event EventHandler PlayStateChanged;

    /// <summary>Raised when playback moves into a different chapter, so the drop-down can follow it.</summary>
    event EventHandler ChapterChanged;
}
