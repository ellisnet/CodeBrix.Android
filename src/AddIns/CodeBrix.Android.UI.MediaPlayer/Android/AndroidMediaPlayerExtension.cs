using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using CodeBrix.Platform.Media.Playback;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using AHandler = global::Android.OS.Handler;
using ALooper = global::Android.OS.Looper;
using ATextureView = global::Android.Views.TextureView;
using PlaybackState = Windows.Media.Playback.MediaPlaybackState;
using WMediaPlayer = Windows.Media.Playback.MediaPlayer;

namespace CodeBrix.Android.UI.MediaPlayer.Android;

/// <summary>
/// The Android <see cref="IMediaPlayerExtension"/> (one per Windows.Media.Playback.MediaPlayer, created by Core through
/// ApiExtensibility): an AndroidX Media3 ExoPlayer on the main looper, following the contract every CodeBrix.Platform
/// engine follows - a new source raises SourceChanged and moves the session to Opening; the prepared media raises
/// NaturalVideoDimensionChanged, NaturalDurationChanged and MediaOpened (its first picture is shown without playing);
/// Play/Pause/Stop and the session's PlaybackState (Buffering / Playing / Paused / None) follow the engine; PositionChanged
/// is raised while playing; the end raises MediaEnded (then loops, or steps a playlist); a failure raises MediaFailed
/// naming the source. The picture goes to the TextureView the presenter extension attaches.
/// </summary>
internal sealed class AndroidMediaPlayerExtension : IMediaPlayerExtension
{
    private const int StateIdle = 1;
    private const int StateBuffering = 2;
    private const int StateReady = 3;
    private const int StateEnded = 4;
    private const int TrackTypeVideo = 2;
    private const long TimeUnset = long.MinValue + 1;

    private static readonly ConditionalWeakTable<WMediaPlayer, AndroidMediaPlayerExtension> _byPlayer = new();

    private readonly WMediaPlayer _player;
    private readonly AHandler _main = new(ALooper.MainLooper);
    private readonly IExoPlayer _exo;
    private readonly Listener _listener;
    private MediaPlaybackList _playlist;
    private int _playlistIndex = -1;
    private Uri _uri;
    private bool _opened;
    private bool _ticking;
    private bool _updatingPositionFromNative;
    private TimeSpan _position;
    private TimeSpan _duration;
    private bool? _isVideo;
    private ATextureView _surface;

    /// <summary>Creates the engine of one MediaPlayer.</summary>
    /// <param name="player">The MediaPlayer.</param>
    internal AndroidMediaPlayerExtension(WMediaPlayer player)
    {
        _player = player;
        _exo = new ExoPlayerBuilder(global::Android.App.Application.Context).SetLooper(ALooper.MainLooper).Build();
        _exo.PlayWhenReady = false;
        _listener = new Listener(this);
        _exo.AddListener(_listener);
        _byPlayer.AddOrUpdate(player, this);
    }

    /// <summary>Raised when the media is found to have (or not have) a picture.</summary>
    internal event EventHandler<bool?> IsVideoChanged;

    /// <summary>Raised when the decoded picture's size changes.</summary>
    internal event EventHandler VideoSizeChanged;

    /// <inheritdoc />
    public IMediaPlayerEventsExtension Events { get; set; }

    /// <summary>The picture's size in pixels (0 x 0 while unknown).</summary>
    internal Size VideoSize
    {
        get
        {
            var size = _exo.VideoSize;
            return size == null || size.Width <= 0 ? default : new Size(size.Width * (size.PixelWidthHeightRatio > 0 ? size.PixelWidthHeightRatio : 1), size.Height);
        }
    }

    /// <inheritdoc />
    public double PlaybackRate
    {
        get => _exo.PlaybackParameters?.Speed ?? 1.0;
        set => _exo.SetPlaybackSpeed((float)value);
    }

    /// <inheritdoc />
    public bool IsLoopingEnabled { get; set; }

    /// <inheritdoc />
    public bool IsLoopingAllEnabled { get; set; }

    /// <inheritdoc />
    public MediaPlayerState CurrentState => default;

    /// <inheritdoc />
    public TimeSpan NaturalDuration => _duration;

    /// <inheritdoc />
    public bool IsProtected => false;

    /// <inheritdoc />
    public double BufferingProgress => _exo.BufferedPercentage / 100.0;

    /// <inheritdoc />
    public bool CanPause => true;

    /// <inheritdoc />
    public bool CanSeek => _exo.IsCurrentMediaItemSeekable;

    /// <inheritdoc />
    public MediaPlayerAudioDeviceType AudioDeviceType { get; set; }

    /// <inheritdoc />
    public MediaPlayerAudioCategory AudioCategory { get; set; }

    /// <inheritdoc />
    public TimeSpan TimelineControllerPositionOffset
    {
        get => Position;
        set => Position = value;
    }

    /// <inheritdoc />
    public bool RealTimePlayback { get; set; }

    /// <inheritdoc />
    public double AudioBalance { get; set; }

    /// <inheritdoc />
    public TimeSpan Position
    {
        get
        {
            if (ALooper.MyLooper() == ALooper.MainLooper)
            {
                _position = TimeSpan.FromMilliseconds(Math.Max(0, _exo.CurrentPosition));
            }

            return _position;
        }

        set
        {
            // Core writes the position back while it handles PositionChanged: that is not a seek (the Linux engine
            // ignores it the same way).
            if (_updatingPositionFromNative || _duration.TotalMilliseconds <= 0)
            {
                return;
            }

            var target = Math.Clamp(value.TotalMilliseconds, 0, _duration.TotalMilliseconds);
            _position = TimeSpan.FromMilliseconds(target);
            _exo.SeekTo((long)target);
        }
    }

    /// <inheritdoc />
    public bool? IsVideo => _isVideo;

    /// <summary>The engine of a MediaPlayer (null when none was created).</summary>
    /// <param name="player">The MediaPlayer.</param>
    /// <returns>The engine, or null.</returns>
    internal static AndroidMediaPlayerExtension For(WMediaPlayer player) =>
        player != null && _byPlayer.TryGetValue(player, out var extension) ? extension : null;

    /// <summary>Shows the picture in <paramref name="surface"/> (null stops showing it).</summary>
    /// <param name="surface">The presenter's TextureView.</param>
    internal void AttachSurface(ATextureView surface)
    {
        if (ReferenceEquals(_surface, surface))
        {
            return;
        }

        if (_surface != null)
        {
            _exo.ClearVideoTextureView(_surface);
        }

        _surface = surface;
        if (surface != null)
        {
            _exo.SetVideoTextureView(surface);
        }
    }

    /// <inheritdoc />
    public void SetTransportControlsBounds(Rect bounds)
    {
    }

    /// <inheritdoc />
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void InitializeSource()
    {
        _playlistIndex = -1;
        _playlist = null;
        switch (_player.Source)
        {
            case MediaPlaybackItem item:
                SetUri(item.Source?.Uri);
                break;
            case MediaSource source:
                SetUri(source.Uri);
                break;
            case MediaPlaybackList list:
                _playlist = list;
                _playlistIndex = list.Items.Count > 0 ? 0 : -1;
                SetUri(list.Items.FirstOrDefault()?.Source?.Uri);
                break;
            default:
                SetUri(null);
                break;
        }
    }

    /// <inheritdoc />
    public void SetUriSource(Uri value) => throw new NotImplementedException();

    /// <inheritdoc />
    public void SetFileSource(IStorageFile file) => throw new NotImplementedException();

    /// <inheritdoc />
    public void SetStreamSource(IRandomAccessStream stream) => throw new NotImplementedException();

    /// <inheritdoc />
    public void SetMediaSource(IMediaSource source) => throw new NotImplementedException();

    /// <inheritdoc />
    public void StepForwardOneFrame() => _exo.SeekTo(_exo.CurrentPosition + 33);

    /// <inheritdoc />
    public void StepBackwardOneFrame() => _exo.SeekTo(Math.Max(0, _exo.CurrentPosition - 33));

    /// <inheritdoc />
    public void SetSurfaceSize(Size size)
    {
    }

    /// <inheritdoc />
    public void Play()
    {
        if (_exo.PlaybackState == StateEnded)
        {
            _exo.SeekTo(0);
        }

        _exo.Play();
    }

    /// <inheritdoc />
    public void Pause() => _exo.Pause();

    /// <inheritdoc />
    public void Stop()
    {
        _exo.Pause();
        _exo.SeekTo(0);
        SetSessionState(PlaybackState.None);
    }

    /// <inheritdoc />
    public void ToggleMute() => ApplyVolume();

    /// <inheritdoc />
    public void OnVolumeChanged() => ApplyVolume();

    /// <inheritdoc />
    public void OnOptionChanged(string name, object value)
    {
    }

    /// <inheritdoc />
    public void PreviousTrack()
    {
        if (_playlist != null && _playlistIndex > 0)
        {
            _playlistIndex--;
            SetUri(_playlist.Items[_playlistIndex].Source?.Uri);
            Play();
        }
    }

    /// <inheritdoc />
    public void NextTrack()
    {
        if (_playlist != null && _playlist.Items.Count > 0 && _playlistIndex + 1 < _playlist.Items.Count)
        {
            _playlistIndex++;
            SetUri(_playlist.Items[_playlistIndex].Source?.Uri);
            Play();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ticking = false;
        _byPlayer.Remove(_player);
        AttachSurface(null);
        _exo.RemoveListener(_listener);
        _exo.Release();
    }

    private void SetUri(Uri value)
    {
        if (value != _uri)
        {
            SetIsVideo(null);
            _uri = value;
            Events?.RaiseSourceChanged();
        }

        _opened = false;
        _duration = TimeSpan.Zero;
        _position = TimeSpan.Zero;
        _exo.Stop();
        _exo.ClearMediaItems();
        if (_uri == null)
        {
            return;
        }

        SetSessionState(PlaybackState.Opening);
        _exo.SetMediaItem(MediaItem.FromUri(MediaUris.ToEngineUri(_uri)));
        _exo.PlayWhenReady = false;
        _exo.Prepare();
    }

    private void ApplyVolume() => _exo.Volume = _player.IsMuted ? 0f : (float)Math.Clamp(_player.Volume, 0, 1);

    private void SetSessionState(PlaybackState state)
    {
        if (_player.PlaybackSession is { } session && session.PlaybackState != state)
        {
            session.PlaybackState = state;
        }
    }

    private void SetIsVideo(bool? value)
    {
        if (_isVideo != value)
        {
            _isVideo = value;
            IsVideoChanged?.Invoke(this, value);
        }
    }

    private void OnStateChanged(int state)
    {
        switch (state)
        {
            case StateBuffering:
                if (_opened)
                {
                    SetSessionState(PlaybackState.Buffering);
                }

                break;
            case StateReady:
                if (!_opened)
                {
                    _opened = true;
                    var duration = _exo.Duration;
                    _duration = duration == TimeUnset || duration < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(duration);
                    SetIsVideo(_exo.CurrentTracks?.ContainsType(TrackTypeVideo) == true);
                    Events?.RaiseNaturalVideoDimensionChanged();
                    Events?.NaturalDurationChanged();
                    Events?.RaiseMediaOpened();
                }

                SetSessionState(_exo.IsPlaying ? PlaybackState.Playing : PlaybackState.Paused);
                break;
            case StateEnded:
                OnEnded();
                break;
        }
    }

    private void OnEnded()
    {
        _ticking = false;
        _position = _duration;
        Events?.RaiseMediaEnded();
        SetSessionState(PlaybackState.None);
        if (IsLoopingEnabled && !IsLoopingAllEnabled)
        {
            _exo.SeekTo(0);
            _exo.Play();
        }
        else if (_playlist != null && _playlistIndex + 1 < _playlist.Items.Count)
        {
            NextTrack();
        }
        else if (_playlist != null && IsLoopingAllEnabled && _playlist.Items.Count > 0)
        {
            _playlistIndex = 0;
            SetUri(_playlist.Items[0].Source?.Uri);
            Play();
        }
    }

    private void OnIsPlayingChanged(bool isPlaying)
    {
        if (isPlaying)
        {
            SetSessionState(PlaybackState.Playing);
            StartTicking();
        }
        else if (_exo.PlaybackState == StateReady)
        {
            SetSessionState(PlaybackState.Paused);
        }
    }

    private void StartTicking()
    {
        if (_ticking)
        {
            return;
        }

        _ticking = true;
        _main.Post(Tick);
    }

    private void Tick()
    {
        if (!_ticking)
        {
            return;
        }

        _position = TimeSpan.FromMilliseconds(Math.Max(0, _exo.CurrentPosition));
        RaisePositionChangedFromNative();
        if (_exo.IsPlaying)
        {
            _main.PostDelayed(Tick, 100);
        }
        else
        {
            _ticking = false;
        }
    }

    private void RaisePositionChangedFromNative()
    {
        _updatingPositionFromNative = true;
        try
        {
            Events?.RaisePositionChanged();
        }
        finally
        {
            _updatingPositionFromNative = false;
        }
    }

    private void OnError(PlaybackException error)
    {
        _ticking = false;
        var source = _uri?.OriginalString ?? string.Empty;
        var message = (error?.Message ?? "The media could not be played.") + (source.Length > 0 ? " (" + source + ")" : string.Empty);
        Events?.RaiseMediaFailed(MediaPlayerError.Unknown, message, error == null ? null : new InvalidOperationException(message));
        SetSessionState(PlaybackState.None);
    }

    private void OnSeekDone()
    {
        _position = TimeSpan.FromMilliseconds(Math.Max(0, _exo.CurrentPosition));
        RaisePositionChangedFromNative();
        Events?.RaiseSeekCompleted();
    }

    private sealed class Listener : global::Java.Lang.Object, IPlayerListener
    {
        private readonly WeakReference<AndroidMediaPlayerExtension> _owner;

        internal Listener(AndroidMediaPlayerExtension owner) => _owner = new WeakReference<AndroidMediaPlayerExtension>(owner);

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                owner.OnStateChanged(playbackState);
            }
        }

        public void OnIsPlayingChanged(bool isPlaying)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                owner.OnIsPlayingChanged(isPlaying);
            }
        }

        public void OnPlayerError(PlaybackException error)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                owner.OnError(error);
            }
        }

        public void OnVideoSizeChanged(VideoSize videoSize)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                owner.Events?.RaiseNaturalVideoDimensionChanged();
                owner.VideoSizeChanged?.Invoke(owner, EventArgs.Empty);
            }
        }

        public void OnPositionDiscontinuity(PlayerPositionInfo oldPosition, PlayerPositionInfo newPosition, int reason)
        {
            // DISCONTINUITY_REASON_SEEK = 1.
            if (reason == 1 && _owner.TryGetTarget(out var owner))
            {
                owner.OnSeekDone();
            }
        }
    }
}
