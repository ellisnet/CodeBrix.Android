using CodeBrix.Platform.Simple;
using CodeBrixVideoTool.Playback.ViewModels;
using CodeBrixVideoTool.Processing;
using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Operations;
using CodeBrixVideoTool.Processing.Probing;
using CodeBrixVideoTool.Processing.Tools;
using CodeBrixVideoTool.Processing.ViewModels;
using CodeBrixVideoTool.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.ViewModels;

/// <summary>
/// The whole application in one object: the list of files it is working with, the player half and
/// the conversion half, and the one selection that ties them together.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IMediaFileBridge
{
    private readonly IMediaProbe probe;
    private readonly IExternalToolCheck toolCheck;
    private bool toolsChecked;

    /// <summary>Creates the view model and the two halves of the application under it.</summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        probe = GetService<IMediaProbe>() ?? new MediaProbe();
        toolCheck = GetService<IExternalToolCheck>() ?? new ExternalToolCheck();

        Playback = new PlaybackViewModel();
        Conversion = new ConversionViewModel();
        Conversion.ConversionFinished += OnConversionFinished;
    }

    #region | Bindable properties |

    /// <summary>The player half: what is open, the transport, the chapters and the captions.</summary>
    public PlaybackViewModel Playback { get; }

    /// <summary>The conversion half: the destination, the size, the action and the progress.</summary>
    public ConversionViewModel Conversion { get; }

    /// <summary>Every file this session has opened or produced, newest last.</summary>
    public ObservableCollection<SourceMediaInfo> Library { get; } = new();

    /// <summary>The file the player is showing and the conversion panel is set up for.</summary>
    [AffectsCommands(nameof(RemoveCommand))]
    public SourceMediaInfo SelectedItem
    {
        get;
        set
        {
            SetProperty(ref field, value);
            Conversion.Source = value;
            Playback.Open(value);
            NotifyPropertyChanged(nameof(EmptyLibraryVisibility));
        }
    }

    /// <summary>True while a file is being probed or a conversion is under way.</summary>
    [AffectsAllCommands]
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>What the application last did, for the status bar.</summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Open a video file to begin.";

    /// <summary>Whether to show the "nothing here yet" message in place of the file list.</summary>
    public Visibility EmptyLibraryVisibility => GetVisibility(Library.Count == 0);

    /// <summary>The application's name and what it is for, for the title bar.</summary>
    public string Title => "CodeBrix Video Tool";

    /// <summary>A one-line description of what the application does, for the header.</summary>
    public string Subtitle =>
        "Import, transcode, export and play MKV, WebM, CodeBrix Mode 1 and CodeBrix Mode 2 video.";

    #endregion

    #region | Commands and their implementations |

    /// <summary>Asks for a file and adds it to the list.</summary>
    public SimpleCommand OpenCommand => field ??= new SimpleCommand(
        () => !IsBusy, (Func<object, Task>)(_ => DoOpenAsync()));

    /// <summary>Takes the selected file out of the list. The file itself is left alone.</summary>
    public SimpleCommand RemoveCommand => field ??= new SimpleCommand(
        () => !IsBusy && SelectedItem is not null, _ => DoRemove());

    private async Task DoOpenAsync()
    {
        //The FIRST Open of the session checks the external tools, once, and warns about the first one that is
        //missing. The flag is set before the check runs, so a second click never checks or warns again. The
        //warning is dismissed with OK and the Open carries on: a .cbv file opens and plays without FFmpeg.
        if (!toolsChecked)
        {
            toolsChecked = true;
            await WarnAboutMissingToolsAsync();
        }

        if (PickMediaFileAsync is null)
        {
            StatusText = "This head has no file dialog, so a file cannot be chosen by hand.";
            return;
        }

        var path = await PickMediaFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await AddAsync(path, CancellationToken.None);
    }

    private async Task WarnAboutMissingToolsAsync()
    {
        var previousStatus = StatusText;
        string problem;

        IsBusy = true;
        StatusText = "Checking for FFmpeg, FFprobe and SVT-AV1...";
        try
        {
            problem = await toolCheck.FindProblemAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            //The check is a courtesy. If it cannot be made, the Open goes ahead and the work itself reports.
            problem = null;
            previousStatus = $"Could not check for FFmpeg: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        if (problem is null)
        {
            StatusText = previousStatus;
            return;
        }

        StatusText = problem;
        await ShowInfo(problem);
    }

    private void DoRemove()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        Library.Remove(item);
        SelectedItem = Library.LastOrDefault();
        NotifyPropertyChanged(nameof(EmptyLibraryVisibility));
        StatusText = $"Removed {item.FileName} from the list.";
    }

    #endregion

    #region | Head-capability bridges |

    /// <inheritdoc />
    public Func<Task<string>> PickMediaFileAsync { get; set; }

    #endregion

    /// <summary>
    /// Probes a file, adds it to the list and selects it. Used by the Open command, by a finished
    /// conversion, and by the scripted smoke run.
    /// </summary>
    /// <param name="path">The file to add.</param>
    /// <param name="cancellationToken">Stops the probe.</param>
    /// <returns>What probing found, or null when the file was refused.</returns>
    public async Task<SourceMediaInfo> AddAsync(string path, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var existing = Library.FirstOrDefault(i =>
                string.Equals(i.Path, path, StringComparison.Ordinal));
            if (existing is not null)
            {
                SelectedItem = existing;
                StatusText = $"{existing.FileName} is already in the list.";
                return existing;
            }

            var info = await probe.ProbeAsync(path, cancellationToken);
            Library.Add(info);
            NotifyPropertyChanged(nameof(EmptyLibraryVisibility));
            SelectedItem = info;
            StatusText = $"Opened {info.FileName} - {info}";
            return info;
        }
        catch (VideoToolProcessingException exception)
        {
            StatusText = exception.Message;
            return null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Adds a finished conversion's output to the list. This is an event handler, so it is
    /// <c>async void</c> and has no caller to hand a failure back to: the whole body is wrapped so
    /// that nothing can escape into the dispatcher, and what <see cref="AddAsync" /> does not already
    /// turn into a sentence ends up in the status bar as one.
    /// </summary>
    private async void OnConversionFinished(object sender, ConversionOutcome outcome)
    {
        try
        {
            if (!outcome.Succeeded)
            {
                StatusText = outcome.ToString();
                return;
            }

            //Every successful output joins the list, an exported .mp4 included: it can be re-imported,
            //so it is a source like any other. Nothing here plays one, so its row is dimmed (see
            //LibraryItemTemplate in MainPage.xaml) and selecting it makes the player say why.
            await AddAsync(outcome.OutputPath, CancellationToken.None);
            StatusText = outcome.ToString();
        }
        catch (Exception exception)
        {
            StatusText = $"The conversion finished, but its result could not be listed: {exception.Message}";
        }
    }
}
