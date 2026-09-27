using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Simple;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace MediaPlayerDemo.ViewModels;

[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel
{
    private const string DefaultMediaAddress =
        "https://mdn.github.io/learning-area/html/multimedia-and-embedding/video-and-audio-content/rabbit320.mp4";

    //The application's configured logger factory, which the heads set up in App.InitializeLogging().
    //  NullLogger covers the design-mode path, where the constructor returns before this is assigned.
    private ILogger _log = NullLogger.Instance;

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        _log = LogExtensionPoint.AmbientLoggerFactory.CreateLogger<MainViewModel>();
        _log.LogInformation("Main view model startup.");

        //Load (and, because the player has AutoPlay enabled, start) the default media on startup
        LoadMedia();
    }

    #region | Bindable properties |

    [AffectsCommands(nameof(LoadCommand))]
    public string MediaAddress
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = DefaultMediaAddress;

    public IMediaPlaybackSource PlayerSource
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = "Ready";

    //The stretch modes offered by the ComboBox. The Stretch enum's member names ("Uniform",
    //  "UniformToFill", "Fill", "None") are exactly the text we want shown, so the ComboBox can
    //  bind straight to the enum values with no separate label list.
    public IReadOnlyList<Stretch> StretchOptions { get; } =
    [
        Stretch.Uniform,
        Stretch.UniformToFill,
        Stretch.Fill,
        Stretch.None
    ];

    //The player's stretch mode, two-way bound to the ComboBox's SelectedItem.
    public Stretch SelectedStretch
    {
        get;
        set => SetEnumProperty(ref field, value);
    } = Stretch.Uniform;

    #endregion

    #region | Commands and their implementations |

    #region LoadCommand

    private SimpleCommand _loadCommand;
    public SimpleCommand LoadCommand =>
        (_loadCommand ??= new SimpleCommand(CanLoad, DoLoad));

    private bool CanLoad() => !string.IsNullOrWhiteSpace(MediaAddress);

    private void DoLoad()
    {
        if (CanLoad())
        {
            LoadMedia();
        }
    }

    #endregion

    #endregion

    #region | Owning the playback source |

    private void LoadMedia()
    {
        try
        {
            var uri = new Uri(MediaAddress);
            SetPlayerSource(MediaSource.CreateFromUri(uri));
            StatusText = $"Loaded: {uri}";
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cannot load '{MediaAddress}'.", MediaAddress);
            StatusText = $"Cannot load '{MediaAddress}': {ex.Message}";
        }
    }

    //The view model creates every source it hands to the element, so it also releases them. The
    //  new source is published first: the element follows the binding and moves off the old one
    //  before the old one is disposed.
    private void SetPlayerSource(IMediaPlaybackSource source)
    {
        var previous = PlayerSource;
        PlayerSource = source;

        if (!ReferenceEquals(previous, source))
        {
            (previous as IDisposable)?.Dispose();
        }
    }

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        (PlayerSource as IDisposable)?.Dispose();
        _loadCommand?.Dispose();
        _loadCommand = null;
        base.Dispose();
    }

    #endregion
}
