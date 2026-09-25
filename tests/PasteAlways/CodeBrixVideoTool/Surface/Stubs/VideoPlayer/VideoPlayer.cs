// STUB (paste always): CodeBrix.Platform.UI.VideoPlayer.Skia.VideoPlayer and VideoPlayerFailedEventArgs (package
// CodeBrix.Platform.VideoPlayer.ApacheLicenseForever, assembly CodeBrix.Platform.UI.VideoPlayer.Skia, a
// CodeBrix.Platform repo add-in); it has no Android flavor yet. Same namespace and type names; the members
// CodeBrixVideoTool's MainPage sets, binds and calls (member names and types from the add-in's public API).
using System;
using System.Collections.Generic;
using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using CodeBrix.VideoPlayback.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia;

/// <summary>Stand-in for the video player element (compile-only).</summary>
public partial class VideoPlayer : Control
{
    /// <summary>Identifies the <see cref="Source"/> dependency property.</summary>
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(string), typeof(VideoPlayer), new PropertyMetadata(""));

    /// <summary>Identifies the <see cref="AutoPlay"/> dependency property.</summary>
    public static readonly DependencyProperty AutoPlayProperty =
        DependencyProperty.Register(nameof(AutoPlay), typeof(bool), typeof(VideoPlayer), new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="PositionSeconds"/> dependency property.</summary>
    public static readonly DependencyProperty PositionSecondsProperty =
        DependencyProperty.Register(nameof(PositionSeconds), typeof(double), typeof(VideoPlayer), new PropertyMetadata(0.0));

    /// <summary>Identifies the <see cref="Duration"/> dependency property.</summary>
    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(VideoPlayer), new PropertyMetadata(TimeSpan.Zero));

    /// <summary>Identifies the <see cref="DurationSeconds"/> dependency property.</summary>
    public static readonly DependencyProperty DurationSecondsProperty =
        DependencyProperty.Register(nameof(DurationSeconds), typeof(double), typeof(VideoPlayer), new PropertyMetadata(0.0));

    /// <summary>Identifies the <see cref="IsPlaying"/> dependency property.</summary>
    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(VideoPlayer), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="Stretch"/> dependency property.</summary>
    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(VideoPlayer), new PropertyMetadata(Stretch.Uniform));

    /// <summary>Identifies the <see cref="SelectedCaptionTrack"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedCaptionTrackProperty =
        DependencyProperty.Register(nameof(SelectedCaptionTrack), typeof(CaptionTrack), typeof(VideoPlayer), new PropertyMetadata(null));

    /// <summary>Raised when a source has been opened.</summary>
    public event EventHandler MediaOpened;

    /// <summary>Raised when playback reaches the end.</summary>
    public event EventHandler PlaybackEnded;

    /// <summary>Raised when a source cannot be opened or played.</summary>
    public event EventHandler<VideoPlayerFailedEventArgs> MediaFailed;

    /// <summary>Raised when playback enters another chapter.</summary>
    public event EventHandler ChapterChanged;

    /// <summary>The video source (a path or URI; empty to unload).</summary>
    public string Source { get => (string)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Whether setting a source starts playback.</summary>
    public bool AutoPlay { get => (bool)GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <summary>The playback position in seconds.</summary>
    public double PositionSeconds { get => (double)GetValue(PositionSecondsProperty); set => SetValue(PositionSecondsProperty, value); }

    /// <summary>The duration of the loaded video.</summary>
    public TimeSpan Duration => (TimeSpan)GetValue(DurationProperty);

    /// <summary>The duration in seconds.</summary>
    public double DurationSeconds => (double)GetValue(DurationSecondsProperty);

    /// <summary>Whether the player is playing.</summary>
    public bool IsPlaying => (bool)GetValue(IsPlayingProperty);

    /// <summary>How the picture fills the element.</summary>
    public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }

    /// <summary>The caption track shown.</summary>
    public CaptionTrack SelectedCaptionTrack { get => (CaptionTrack)GetValue(SelectedCaptionTrackProperty); set => SetValue(SelectedCaptionTrackProperty, value); }

    /// <summary>The file's caption tracks.</summary>
    public IReadOnlyList<CaptionTrack> CaptionTracks { get; } = Array.Empty<CaptionTrack>();

    /// <summary>The file's chapters.</summary>
    public IReadOnlyList<Chapter> Chapters { get; } = Array.Empty<Chapter>();

    /// <summary>The chapter playback is inside, or null.</summary>
    public Chapter CurrentChapter => null;

    /// <summary>Frames posted, presented and dropped so far.</summary>
    public VideoFramePresenterStatistics FrameStatistics => new VideoFramePresenterStatistics(0, 0, 0, 0);

    /// <summary>Starts or resumes playback.</summary>
    public void Play() { }

    /// <summary>Pauses playback.</summary>
    public void Pause() { }

    /// <summary>Stops playback.</summary>
    public void Stop() { }

    /// <summary>Seeks to the start of a chapter.</summary>
    public void SeekToChapter(int index) { }

    /// <summary>Raises the events (keeps them used).</summary>
    protected void RaiseAll()
    {
        MediaOpened?.Invoke(this, EventArgs.Empty);
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
        MediaFailed?.Invoke(this, new VideoPlayerFailedEventArgs(string.Empty, null));
        ChapterChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Stand-in for the media-failed event arguments (compile-only).</summary>
public sealed class VideoPlayerFailedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    public VideoPlayerFailedEventArgs(string message, Exception error)
    {
        Message = message;
        Error = error;
    }

    /// <summary>What went wrong, for a person.</summary>
    public string Message { get; }

    /// <summary>The underlying exception, if any.</summary>
    public Exception Error { get; }
}
