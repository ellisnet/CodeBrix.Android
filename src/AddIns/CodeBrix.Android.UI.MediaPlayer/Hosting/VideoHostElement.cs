using CodeBrix.Android.UI.MediaPlayer.Portable;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.MediaPlayer.Hosting;

/// <summary>
/// The element the Android presenter extension puts into a MediaPlayerPresenter (as every CodeBrix.Platform head puts its
/// frame host there): Core lays it out over the presenter; its handler shows the engine's picture in a TextureView fitted
/// to it by the presenter's Stretch.
/// </summary>
internal sealed class VideoHostElement : FrameworkElement
{
    private VideoStretch _stretch = VideoStretch.Uniform;

    /// <summary>Raised when the stretch mode changes.</summary>
    internal event System.EventHandler StretchChanged;

    /// <summary>How the picture is fitted into the element.</summary>
    internal VideoStretch Stretch
    {
        get => _stretch;
        set
        {
            if (_stretch != value)
            {
                _stretch = value;
                StretchChanged?.Invoke(this, System.EventArgs.Empty);
            }
        }
    }

    /// <summary>The engine whose picture this element shows (set by the presenter extension).</summary>
    internal Android.AndroidMediaPlayerExtension Engine { get; set; }

    /// <summary>Raised when <see cref="Engine"/> changes.</summary>
    internal event System.EventHandler EngineChanged;

    /// <summary>Tells the handler the engine changed.</summary>
    internal void RaiseEngineChanged() => EngineChanged?.Invoke(this, System.EventArgs.Empty);

    /// <inheritdoc />
    internal override bool IsViewHit() => true;
}
