using CodeBrix.Platform.Simple;
using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using CodeBrix.Platform.UI.VideoPlayer.Skia;
using CodeBrixVideoTool.Playback.Services;
using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Operations;
using CodeBrixVideoTool.Services;
using CodeBrixVideoTool.Smoke;
using CodeBrixVideoTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace CodeBrixVideoTool.Views;

public sealed partial class MainPage : Page, ISmokeSurface
{
    private VideoPlayerSurface surface;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
            WireViewModel();
        };

        //Optional scripted run: import, play and report without anyone touching the window.
        if (SmokeOptions.FromEnvironment() is { } smoke)
        {
            Loaded += (_, _) => StartSmokeRun(smoke);
        }

        this.InitializeComponent(); //Leave this line last
    }

    private MainViewModel ViewModel => DataContext as MainViewModel;

    private void WireViewModel()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        surface ??= new VideoPlayerSurface(Player);
        viewModel.Playback.AttachSurface(surface);

        //Each bridge is handed over through the interface that declares it rather than through the
        //view model's own type, so what the page has to supply is the contract and nothing more.
        if (DataContext is IMediaFileBridge mediaFile)
        {
            mediaFile.PickMediaFileAsync = PickMediaFileAsync;
        }

        if (viewModel.Conversion is IOutputPathBridge outputPath)
        {
            outputPath.PickOutputPathAsync = PickOutputPathAsync;
        }
    }

    #region | Player element events |

    private void Player_MediaOpened(object sender, EventArgs e) => surface?.RaiseMediaOpened();

    private void Player_PlaybackEnded(object sender, EventArgs e) => surface?.RaisePlaybackEnded();

    private void Player_MediaFailed(object sender, VideoPlayerFailedEventArgs e) => surface?.RaiseMediaFailed(e.Message);

    #endregion

    #region | Head-capability bridges |

    private static async Task<string> PickMediaFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };

            foreach (var extension in MediaFormats.ImportExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".webm");
            picker.FileTypeFilter.Add(".cbv");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (NotSupportedException)
        {
            //A head with no windowing system registers no picker extensions.
            return null;
        }
    }

    private static async Task<string> PickOutputPathAsync(string suggestedFileName, string extension)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                SuggestedFileName = suggestedFileName,
                DefaultFileExtension = extension
            };
            picker.FileTypeChoices.Add(
                MediaFormats.DescribeExtension(extension), new List<string> { extension });

            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
        catch (NotSupportedException)
        {
            //As above: no dialog here, so the caller writes beside the source instead.
            return null;
        }
    }

    #endregion

    #region | The player element behind the playback view model's interface |

    /// <summary>
    /// The player element, behind the small interface the playback view model drives it through.
    /// The element is a XAML control and can only live here; every decision about it lives in the
    /// view model.
    /// </summary>
    private sealed class VideoPlayerSurface : IVideoPlayerSurface
    {
        private readonly VideoPlayer player;

        internal VideoPlayerSurface(VideoPlayer player)
        {
            this.player = player;
            player.RegisterPropertyChangedCallback(
                VideoPlayer.IsPlayingProperty, (_, _) => PlayStateChanged?.Invoke(this, EventArgs.Empty));
            player.ChapterChanged += (_, _) => ChapterChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler MediaOpened;

        public event EventHandler PlaybackEnded;

        public event EventHandler<string> MediaFailed;

        public event EventHandler PlayStateChanged;

        public event EventHandler ChapterChanged;

        public TimeSpan Duration => player.Duration;

        public bool IsPlaying => player.IsPlaying;

        public IReadOnlyList<Chapter> Chapters => player.Chapters;

        public IReadOnlyList<CaptionTrack> CaptionTracks => player.CaptionTracks;

        public int CurrentChapterIndex => player.CurrentChapter?.Index ?? -1;

        public void Open(string path)
        {
            //The source has to be unloaded before anything read at open time is changed, and the
            //real path comes last.
            player.Source = "";
            player.AutoPlay = false;
            player.Source = path;
        }

        public void Close() => player.Source = "";

        public void Play() => player.Play();

        public void Pause() => player.Pause();

        public void Stop() => player.Stop();

        public void SeekToChapter(int index) => player.SeekToChapter(index);

        public void SelectCaptionTrack(CaptionTrack track) => player.SelectedCaptionTrack = track;

        internal void RaiseMediaOpened() => MediaOpened?.Invoke(this, EventArgs.Empty);

        internal void RaisePlaybackEnded() => PlaybackEnded?.Invoke(this, EventArgs.Empty);

        internal void RaiseMediaFailed(string message) => MediaFailed?.Invoke(this, message);
    }

    #endregion

    #region | What a scripted run can only read off the screen |

    /// <summary>
    /// Starts the scripted run. The run itself is <see cref="SmokeRun" /> in the Core library; the
    /// page starts it and answers the handful of questions only the window can answer.
    /// </summary>
    private async void StartSmokeRun(SmokeOptions options)
    {
        try
        {
            await new SmokeRun(options, this).RunAsync(ViewModel);
        }
        catch (Exception exception)
        {
            //An event handler cannot hand a failure back to a caller, and the run reports its own,
            //so this is only here to be certain nothing escapes into the dispatcher unobserved.
            SmokeRun.ReportFailure(exception);
        }
    }

    int ISmokeSurface.QualityChoiceCount => QualityBox.Items.Count;

    object ISmokeSurface.SelectedQualityChoice => QualityBox.SelectedItem;

    bool ISmokeSurface.NotesPanelIsShown => LastRunNotesList.Visibility == Visibility.Visible;

    int ISmokeSurface.NotesPanelLineCount => LastRunNotesList.Items.Count;

    double ISmokeSurface.PlayerDurationSeconds => Player.DurationSeconds;

    double ISmokeSurface.PlayerPositionSeconds => Player.PositionSeconds;

    int ISmokeSurface.PlayerChapterCount => Player.Chapters.Count;

    int ISmokeSurface.PlayerCaptionTrackCount => Player.CaptionTracks.Count;

    SmokeFrameCounts ISmokeSurface.FrameCounts
    {
        get
        {
            var statistics = Player.FrameStatistics;
            return new SmokeFrameCounts(statistics.Posted, statistics.Presented, statistics.Dropped);
        }
    }

    void ISmokeSurface.LayOutLibraryList() => LibraryList.UpdateLayout();

    double? ISmokeSurface.ShownRowOpacity(object item)
    {
        if (item is null || LibraryList.ContainerFromItem(item) is not DependencyObject container)
        {
            return null;
        }

        return FindLibraryRow(container)?.Opacity;
    }

    private static FrameworkElement FindLibraryRow(DependencyObject node)
    {
        var children = VisualTreeHelper.GetChildrenCount(node);
        for (var index = 0; index < children; index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);
            if (child is FrameworkElement { Name: "LibraryRow" } row)
            {
                return row;
            }

            if (FindLibraryRow(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    #endregion
}
