using CodeBrix.Android.UI.MediaPlayer.Hosting;
using CodeBrix.Android.UI.MediaPlayer.Portable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.MediaPlayer.Android;

/// <summary>
/// The Android <see cref="IMediaPlayerPresenterExtension"/> (one per MediaPlayerPresenter): puts a
/// <see cref="VideoHostElement"/> into the presenter (collapsed until the media is known to have a picture, as on every
/// CodeBrix.Platform head - a clip with sound only keeps the poster), follows the presenter's MediaPlayer and Stretch,
/// and reports the picture's natural size.
/// </summary>
internal sealed class AndroidMediaPlayerPresenterExtension : IMediaPlayerPresenterExtension
{
    private readonly MediaPlayerPresenter _presenter;
    private readonly VideoHostElement _element;
    private AndroidMediaPlayerExtension _engine;

    /// <summary>Creates the extension of one presenter.</summary>
    /// <param name="presenter">The presenter.</param>
    internal AndroidMediaPlayerPresenterExtension(MediaPlayerPresenter presenter)
    {
        _presenter = presenter;
        _element = new VideoHostElement { Visibility = Visibility.Collapsed };
        presenter.Child = _element;
        StretchChanged();
    }

    /// <inheritdoc />
    public uint NaturalVideoHeight => _engine == null ? 0 : (uint)_engine.VideoSize.Height;

    /// <inheritdoc />
    public uint NaturalVideoWidth => _engine == null ? 0 : (uint)_engine.VideoSize.Width;

    /// <inheritdoc />
    public void MediaPlayerChanged()
    {
        var engine = AndroidMediaPlayerExtension.For(_presenter.MediaPlayer);
        if (ReferenceEquals(engine, _engine))
        {
            return;
        }

        if (_engine != null)
        {
            _engine.IsVideoChanged -= OnIsVideoChanged;
        }

        _engine = engine;
        _element.Engine = engine;
        if (engine != null)
        {
            engine.IsVideoChanged += OnIsVideoChanged;
            OnIsVideoChanged(engine, engine.IsVideo);
        }

        _element.RaiseEngineChanged();
    }

    /// <inheritdoc />
    public void StretchChanged() => _element.Stretch = _presenter.Stretch switch
    {
        Stretch.None => VideoStretch.None,
        Stretch.Fill => VideoStretch.Fill,
        Stretch.UniformToFill => VideoStretch.UniformToFill,
        _ => VideoStretch.Uniform,
    };

    /// <inheritdoc />
    public void RequestFullScreen()
    {
    }

    /// <inheritdoc />
    public void ExitFullScreen()
    {
    }

    /// <inheritdoc />
    public void RequestCompactOverlay()
    {
    }

    /// <inheritdoc />
    public void ExitCompactOverlay()
    {
    }

    private void OnIsVideoChanged(object sender, bool? isVideo)
    {
        _element.Visibility = isVideo == true ? Visibility.Visible : Visibility.Collapsed;
        if (isVideo == true)
        {
            StretchChanged();
        }
    }
}
