using CodeBrix.Platform.Simple;
using CodeBrixVideoTool.Playback.Models;
using CodeBrixVideoTool.Playback.Services;
using CodeBrixVideoTool.Processing.Probing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;

namespace CodeBrixVideoTool.Playback.ViewModels;

/// <summary>
/// Everything the player half of the application decides: what is open, whether it can be played at
/// all, what the transport may do right now, and which chapter and caption track are showing.
/// </summary>
/// <remarks>
/// The player element itself is a XAML control and stays in the view, reached through
/// <see cref="IVideoPlayerSurface" />. The scrubber, the timecodes and the volume slider bind
/// straight to the element's own dependency properties, so a position tick never travels through
/// this class; what does travel through it is every decision - which is why the transport buttons,
/// the two drop-downs and the status line are all driven from here.
/// </remarks>
[Microsoft.UI.Xaml.Data.Bindable]
public class PlaybackViewModel : SimpleViewModel
{
    private IVideoPlayerSurface surface;
    private bool suppressSelectionChanges;

    /// <summary>Creates the view model.</summary>
    public PlaybackViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor
    }

    #region | Bindable properties |

    /// <summary>The file the player is showing, or null when nothing is open.</summary>
    [AffectsCommands(nameof(PlayCommand), nameof(PauseCommand), nameof(StopCommand))]
    public SourceMediaInfo CurrentItem
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(CurrentItemName));
            NotifyPropertyChanged(nameof(PlayerPlaceholderVisibility));
        }
    }

    /// <summary>The open file's name, or a placeholder.</summary>
    public string CurrentItemName => CurrentItem?.FileName ?? "Nothing open";

    /// <summary>What the player is doing, in a sentence.</summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Open a file to play it.";

    /// <summary>True once a file has been handed to the player and it has reported its duration.</summary>
    [AffectsCommands(nameof(PlayCommand), nameof(PauseCommand), nameof(StopCommand))]
    public bool IsOpen
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(TransportVisibility));
            NotifyPropertyChanged(nameof(PlayerPlaceholderVisibility));
        }
    }

    /// <summary>True while the picture is moving.</summary>
    [AffectsCommands(nameof(PlayCommand), nameof(PauseCommand), nameof(StopCommand))]
    public bool IsPlaying
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// True when the file that is selected cannot be played in this application at all - which is
    /// only ever an MP4, because the player decodes AV1 and nothing else.
    /// </summary>
    public bool IsUnplayableFormat
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(UnplayableNoticeVisibility));
            NotifyPropertyChanged(nameof(PlayerPlaceholderVisibility));
        }
    }

    /// <summary>The chapters the open file carries, if any.</summary>
    public ObservableCollection<ChapterEntry> Chapters { get; } = new();

    /// <summary>The chapter the player is in, or the one a person just chose.</summary>
    public ChapterEntry SelectedChapter
    {
        get;
        set
        {
            SetProperty(ref field, value);
            if (!suppressSelectionChanges && value is not null)
            {
                surface?.SeekToChapter(value.Index);
            }
        }
    }

    /// <summary>The caption tracks the open file carries, with an "off" row in front of them.</summary>
    public ObservableCollection<CaptionEntry> CaptionTracks { get; } = new();

    /// <summary>The caption track that is showing, or the "off" row.</summary>
    public CaptionEntry SelectedCaptionTrack
    {
        get;
        set
        {
            SetProperty(ref field, value);
            if (!suppressSelectionChanges)
            {
                surface?.SelectCaptionTrack(value?.Track);
            }
        }
    }

    /// <summary>Whether to show the chapter drop-down: only when the file has chapters.</summary>
    public Visibility ChapterVisibility => GetVisibility(PlaybackSelection.ShouldShowChapters(Chapters.Count));

    /// <summary>Whether to show the caption drop-down: only when the file has caption tracks.</summary>
    public Visibility CaptionVisibility => GetVisibility(PlaybackSelection.ShouldShowCaptions(CaptionTracks.Count));

    /// <summary>Whether to show the transport bar: only when something is open.</summary>
    public Visibility TransportVisibility => GetVisibility(IsOpen);

    /// <summary>Whether to show the notice explaining that MP4 files are not played here.</summary>
    public Visibility UnplayableNoticeVisibility => GetVisibility(IsUnplayableFormat);

    /// <summary>Whether to show the empty-player message in place of a picture.</summary>
    public Visibility PlayerPlaceholderVisibility => GetVisibility(!IsOpen);

    #endregion

    #region | Commands and their implementations |

    /// <summary>Starts or resumes playback.</summary>
    public SimpleCommand PlayCommand => field ??= new SimpleCommand(() => IsOpen && !IsPlaying, _ => DoPlay());

    /// <summary>Holds playback where it is.</summary>
    public SimpleCommand PauseCommand => field ??= new SimpleCommand(() => IsOpen && IsPlaying, _ => DoPause());

    /// <summary>Stops playback and returns to the start.</summary>
    public SimpleCommand StopCommand => field ??= new SimpleCommand(() => IsOpen, _ => DoStop());

    private void DoPlay()
    {
        surface?.Play();
        RefreshPlayState();
    }

    private void DoPause()
    {
        surface?.Pause();
        RefreshPlayState();
    }

    private void DoStop()
    {
        surface?.Stop();
        RefreshPlayState();
        StatusText = "Stopped.";
    }

    #endregion

    #region | Wiring the player element in |

    /// <summary>
    /// Gives the view model the player element to drive. The page calls this once, when its data
    /// context is set.
    /// </summary>
    /// <param name="playerSurface">The element, behind its interface.</param>
    /// <exception cref="ArgumentNullException">The surface is null.</exception>
    public void AttachSurface(IVideoPlayerSurface playerSurface)
    {
        ArgumentNullException.ThrowIfNull(playerSurface);

        if (surface is not null)
        {
            surface.MediaOpened -= OnMediaOpened;
            surface.PlaybackEnded -= OnPlaybackEnded;
            surface.MediaFailed -= OnMediaFailed;
            surface.PlayStateChanged -= OnPlayStateChanged;
            surface.ChapterChanged -= OnChapterChanged;
        }

        surface = playerSurface;
        surface.MediaOpened += OnMediaOpened;
        surface.PlaybackEnded += OnPlaybackEnded;
        surface.MediaFailed += OnMediaFailed;
        surface.PlayStateChanged += OnPlayStateChanged;
        surface.ChapterChanged += OnChapterChanged;
    }

    /// <summary>
    /// Shows a file in the player, or explains why it cannot be shown. An MP4 is never opened: this
    /// application's player decodes AV1, so an imported or exported MP4 is simply not offered.
    /// </summary>
    /// <param name="item">What probing found in the file, or null to close the player.</param>
    public void Open(SourceMediaInfo item)
    {
        Close();

        if (item is null)
        {
            return;
        }

        CurrentItem = item;

        if (!PlaybackSelection.CanOpen(item))
        {
            IsUnplayableFormat = true;
            StatusText = PlaybackSelection.DescribeUnplayable(item);
            return;
        }

        if (surface is null)
        {
            StatusText = "The player is not ready yet.";
            return;
        }

        StatusText = $"Opening {item.FileName}...";
        surface.Open(item.Path);
    }

    /// <summary>Unloads whatever is open and empties the drop-downs.</summary>
    public void Close()
    {
        surface?.Close();

        suppressSelectionChanges = true;
        try
        {
            Chapters.Clear();
            CaptionTracks.Clear();
            SelectedChapter = null;
            SelectedCaptionTrack = null;
        }
        finally
        {
            suppressSelectionChanges = false;
        }

        IsOpen = false;
        IsPlaying = false;
        IsUnplayableFormat = false;
        CurrentItem = null;
        NotifyPropertyChanged(nameof(ChapterVisibility));
        NotifyPropertyChanged(nameof(CaptionVisibility));
        StatusText = "Open a file to play it.";
    }

    private void OnMediaOpened(object sender, EventArgs e)
    {
        IsOpen = true;
        RefreshChapters();
        RefreshCaptionTracks();
        RefreshPlayState();

        StatusText = PlaybackSelection.DescribeOpened(
            CurrentItemName, surface?.Duration ?? TimeSpan.Zero, Chapters.Count, CaptionTracks.Count);
    }

    private void OnPlaybackEnded(object sender, EventArgs e)
    {
        RefreshPlayState();
        StatusText = "Playback ended.";
    }

    private void OnMediaFailed(object sender, string message)
    {
        IsOpen = false;
        IsPlaying = false;
        StatusText = "The player could not open that file: " + message;
    }

    private void OnPlayStateChanged(object sender, EventArgs e) => RefreshPlayState();

    private void OnChapterChanged(object sender, EventArgs e)
    {
        var index = surface?.CurrentChapterIndex ?? -1;
        if (index < 0 || index >= Chapters.Count)
        {
            return;
        }

        //The drop-down follows playback; setting it here must not seek back to where it already is.
        suppressSelectionChanges = true;
        try
        {
            SelectedChapter = Chapters[index];
        }
        finally
        {
            suppressSelectionChanges = false;
        }
    }

    private void RefreshPlayState() => IsPlaying = surface is { IsPlaying: true };

    private void RefreshChapters()
    {
        suppressSelectionChanges = true;
        try
        {
            Chapters.Clear();
            foreach (var row in PlaybackSelection.BuildChapterRows(surface?.Chapters))
            {
                Chapters.Add(row);
            }

            SelectedChapter = Chapters.Count > 0 ? Chapters[0] : null;
        }
        finally
        {
            suppressSelectionChanges = false;
        }

        NotifyPropertyChanged(nameof(ChapterVisibility));
    }

    private void RefreshCaptionTracks()
    {
        suppressSelectionChanges = true;
        try
        {
            CaptionTracks.Clear();
            foreach (var row in PlaybackSelection.BuildCaptionRows(surface?.CaptionTracks))
            {
                CaptionTracks.Add(row);
            }

            SelectedCaptionTrack = CaptionTracks[0];
        }
        finally
        {
            suppressSelectionChanges = false;
        }

        NotifyPropertyChanged(nameof(CaptionVisibility));
    }

    #endregion
}
