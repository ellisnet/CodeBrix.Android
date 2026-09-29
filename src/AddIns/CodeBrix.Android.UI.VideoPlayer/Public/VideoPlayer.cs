// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/VideoPlayer.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// The same public type in the same namespace (it is not in the VideoPlayer Core): a pasted page's VideoPlayer is this one on Android.
// Changes are marked ANDROID PORT: (1) an ms-appx:/// source is copied out of the APK before it is opened (PackagedVideoAssets);
// (2) the using of this assembly's namespace.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;
using CodeBrix.Android.UI.VideoPlayer.Android; // ANDROID PORT (2)
using CodeBrix.VideoPlayback;
using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using CodeBrix.VideoPlayback.Containers;
using CodeBrix.VideoPlayback.Effects;
using CodeBrix.VideoPlayback.Playback;
using CodeBrix.VideoPlayback.Presentation;
using CodeBrix.VideoPlayback.Rendering;
using CodeBrix.VideoPlayback.Sources;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia;

/// <summary>
/// A XAML-declarable video player for WebM and Matroska files carrying AV1 video, and for CodeBrix
/// ".cbv" video files. Give it a size in a page, point <see cref="Source"/> at a file path, an
/// http(s):// address, an ms-appx:/// asset URI or an embedded://Assembly/Resource.Name embedded
/// resource - or load a stream with <see cref="SetSourceStream"/> - and control playback with
/// <see cref="Play"/> / <see cref="Pause"/> / <see cref="Stop"/> / <see cref="Seek"/>.
///
/// The transport is the AudioPlayer element's, member for member, so one scrubber markup drives
/// either: while playing, <see cref="Position"/> and <see cref="PositionSeconds"/> update on the UI
/// thread every <see cref="PositionUpdateInterval"/>, and both are two-way bindable - writing them
/// (from a Slider the user drags, say) seeks the video, debounced so that releasing the slider
/// lands a single seek. <see cref="Duration"/> / <see cref="DurationSeconds"/> are available as
/// soon as a source is loaded, so a Slider's Maximum can bind with no converter.
///
/// AV1 decoding is BSD-2-Clause rather than Apache-2.0, so it is NOT part of this package: an
/// application references CodeBrix.VideoPlayback.Dav1d.BsdLicenseForever and calls
/// CodeBrixVideoPlaybackDav1d.Register() once at start-up, and Ogg Opus soundtracks likewise need
/// CodeBrix.Audio.Opus.BsdLicenseForever and CodeBrixAudioOpus.Register(). Until then every AV1 file
/// fails with a <see cref="MediaFailed"/> message that names the package and the call; only an
/// uncompressed (V_UNCOMPRESSED) track plays with nothing registered, because that decoder is built
/// into the playback core.
///
/// The picture is composed on the graphics device wherever the running head can supply a context
/// and on the processor everywhere else; <see cref="RenderPath"/> says which is wanted and
/// <see cref="ActiveRenderPath"/> says which is running.
/// </summary>
/// <remarks>
/// This element hosts its picture on a single internal child, so it is a Panel; adding children of
/// your own is not supported. Put an overlay in a Grid cell above it, or - to draw INSIDE the
/// picture, in video pixels, before it reaches the screen - add an
/// <see cref="IVideoLayer"/> to <see cref="Layers"/> or
/// handle <see cref="Composing"/>.
/// </remarks>
[Bindable]
public sealed partial class VideoPlayer : Panel
{
	// A Slider drag writes the bound position on every tick of thumb travel; the seek runs
	// only after the value has been stable for this long, landing one seek per gesture.
	private static readonly TimeSpan SeekDebounceInterval = TimeSpan.FromMilliseconds(200);

	private readonly VideoPlaybackSession _session = new();
	private readonly VideoSurfaceElement _surface;
	private readonly VideoRenderDriver _driver;
	private readonly ObservableCollection<IVideoFrameEffect> _effects = new();
	private readonly ObservableCollection<IVideoLayer> _layers = new();

	private DispatcherQueueTimer? _positionTimer;
	private DispatcherQueueTimer? _seekDebounceTimer;
	private bool _updatingFromPlayback; // set while playback progress writes the position DPs
	private bool _syncingPositionPair;  // set while Position and PositionSeconds mirror each other
	private bool _revertingRenderPath;  // set while an illegal RenderPath write is being undone
	private TimeSpan _pendingSeek;
	private bool _isSourceLoaded;
	private bool _isDisposed;
	private string _lastFailureMessage = "";

	/// <summary>Creates the element. Nothing is opened and no device is touched until a source is set.</summary>
	public VideoPlayer()
	{
		// The shared compositor, taken from this element's own visual rather than from
		// Compositor.GetSharedCompositor, which is internal to the Composition assembly - this
		// AddIn holds InternalsVisibleTo grants from Platform.UI and Platform.UWP only.
		_surface = new VideoSurfaceElement(Visual.Compositor);
		Children.Add(_surface);

		_driver = new VideoRenderDriver(this, _surface, ConfigurePresenter, PublishRenderPath, ReportFailure);

		_effects.CollectionChanged += OnEffectsChanged;
		_layers.CollectionChanged += OnLayersChanged;

		_session.MediaOpened += OnSessionMediaOpened;
		_session.PlaybackEnded += OnSessionPlaybackEnded;
		_session.MediaFailed += OnSessionMediaFailed;
		_session.CaptionCuesChanged += OnSessionCaptionCuesChanged;
		_session.ChapterChanged += OnSessionChapterChanged;

		Unloaded += (_, _) => Pause();
	}

	#region | Events |

	/// <summary>
	/// Raised (on the UI thread) once the container has been read and the tracks, duration and
	/// chapters are known.
	/// </summary>
	public event EventHandler? MediaOpened;

	/// <summary>
	/// Raised (on the UI thread) when playback reaches the natural end of the video. Not raised
	/// when <see cref="IsLooping"/> is true, when <see cref="Stop"/> is called, or when playback
	/// fails.
	/// </summary>
	public event EventHandler? PlaybackEnded;

	/// <summary>
	/// Raised (on the UI thread) when a source fails to load or play - for example a missing file,
	/// a container this family does not read, or a codec whose decoder the application has not
	/// registered.
	/// </summary>
	public event EventHandler<VideoPlayerFailedEventArgs>? MediaFailed;

	/// <summary>
	/// Raised (on the UI thread) when the render path settles and again whenever it changes -
	/// including the moment the graphics path falls back to the processor.
	/// </summary>
	public event EventHandler<VideoPlayerRenderPathChangedEventArgs>? RenderPathChanged;

	/// <summary>Raised (on the UI thread) when the set of captions that should be on screen has changed.</summary>
	public event EventHandler? CaptionCuesChanged;

	/// <summary>Raised (on the UI thread) when playback crosses into a different chapter.</summary>
	public event EventHandler<ChapterChangedEventArgs>? ChapterChanged;

	/// <summary>
	/// Raised after the video and every registered layer have been drawn on the composition
	/// surface, and before it reaches the screen - the ad-hoc alternative to writing an
	/// <see cref="IVideoLayer"/>.
	/// </summary>
	/// <remarks>
	/// Raised on the thread that composes (the UI thread), inside the composition. The canvas is
	/// valid only for the duration of the call, and its coordinates are the video's, not the
	/// element's.
	/// </remarks>
	public event EventHandler<VideoComposingEventArgs>? Composing;

	#endregion

	#region | Dependency properties |

	/// <summary>Identifies the <see cref="Source"/> dependency property.</summary>
	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
		nameof(Source), typeof(string), typeof(VideoPlayer),
		new PropertyMetadata("", (o, e) => ((VideoPlayer)o).OnSourceChanged((string)e.NewValue)));

	/// <summary>
	/// The video source: a WebM, Matroska or .cbv file path, an http:// or https:// address, an
	/// ms-appx:/// asset URI, or an embedded://Assembly/Resource.Name embedded-resource URI.
	/// Setting it opens the file (making <see cref="Duration"/> available immediately) and, when
	/// <see cref="AutoPlay"/> is true, starts playback. Set an empty string to unload.
	/// </summary>
	public string Source
	{
		get => (string)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	/// <summary>Identifies the <see cref="SourceMode"/> dependency property.</summary>
	public static readonly DependencyProperty SourceModeProperty = DependencyProperty.Register(
		nameof(SourceMode), typeof(FileSourceMode), typeof(VideoPlayer),
		new PropertyMetadata(FileSourceMode.Streaming));

	/// <summary>
	/// How a LOCAL FILE source is read: streamed from disk (the default), memory-mapped, or read
	/// into memory once before playback starts. Preloading is what makes a short clip loop with no
	/// disk access at all; it costs the file's size in memory. Ignored for streams and addresses,
	/// and read when a source is opened, so set it before <see cref="Source"/>.
	/// </summary>
	public FileSourceMode SourceMode
	{
		get => (FileSourceMode)GetValue(SourceModeProperty);
		set => SetValue(SourceModeProperty, value);
	}

	/// <summary>Identifies the <see cref="AutoPlay"/> dependency property.</summary>
	public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(
		nameof(AutoPlay), typeof(bool), typeof(VideoPlayer), new PropertyMetadata(false));

	/// <summary>When true, playback starts as soon as a source is loaded. Defaults to false.</summary>
	public bool AutoPlay
	{
		get => (bool)GetValue(AutoPlayProperty);
		set => SetValue(AutoPlayProperty, value);
	}

	/// <summary>Identifies the <see cref="Position"/> dependency property.</summary>
	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
		nameof(Position), typeof(TimeSpan), typeof(VideoPlayer),
		new PropertyMetadata(TimeSpan.Zero, (o, e) => ((VideoPlayer)o).OnPositionChanged((TimeSpan)e.NewValue)));

	/// <summary>
	/// The current playback timecode. Updated on the UI thread while playing (bind an indicator
	/// one-way to follow playback); writing it seeks the video, debounced for seek-on-release
	/// scrubbing. <see cref="PositionSeconds"/> is the same value in seconds.
	/// </summary>
	public TimeSpan Position
	{
		get => (TimeSpan)GetValue(PositionProperty);
		set => SetValue(PositionProperty, value);
	}

	/// <summary>Identifies the <see cref="PositionSeconds"/> dependency property.</summary>
	public static readonly DependencyProperty PositionSecondsProperty = DependencyProperty.Register(
		nameof(PositionSeconds), typeof(double), typeof(VideoPlayer),
		new PropertyMetadata(0.0, (o, e) => ((VideoPlayer)o).OnPositionSecondsChanged((double)e.NewValue)));

	/// <summary>
	/// <see cref="Position"/> expressed in seconds, so a Slider's Value can two-way bind with
	/// no converter: the slider follows playback, and dragging it seeks (on release).
	/// </summary>
	public double PositionSeconds
	{
		get => (double)GetValue(PositionSecondsProperty);
		set => SetValue(PositionSecondsProperty, value);
	}

	/// <summary>Identifies the <see cref="Duration"/> dependency property.</summary>
	public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
		nameof(Duration), typeof(TimeSpan), typeof(VideoPlayer), new PropertyMetadata(TimeSpan.Zero));

	/// <summary>
	/// The total duration of the loaded video (read-only; <see cref="TimeSpan.Zero"/> while no
	/// source is loaded, or when the container does not say).
	/// </summary>
	public TimeSpan Duration
	{
		get => (TimeSpan)GetValue(DurationProperty);
		private set => SetValue(DurationProperty, value);
	}

	/// <summary>Identifies the <see cref="DurationSeconds"/> dependency property.</summary>
	public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
		nameof(DurationSeconds), typeof(double), typeof(VideoPlayer), new PropertyMetadata(0.0));

	/// <summary>
	/// <see cref="Duration"/> expressed in seconds (read-only) - bind a Slider's Maximum to it
	/// with no converter.
	/// </summary>
	public double DurationSeconds
	{
		get => (double)GetValue(DurationSecondsProperty);
		private set => SetValue(DurationSecondsProperty, value);
	}

	/// <summary>Identifies the <see cref="IsPlaying"/> dependency property.</summary>
	public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
		nameof(IsPlaying), typeof(bool), typeof(VideoPlayer), new PropertyMetadata(false));

	/// <summary>True while video is playing (read-only).</summary>
	public bool IsPlaying
	{
		get => (bool)GetValue(IsPlayingProperty);
		private set => SetValue(IsPlayingProperty, value);
	}

	/// <summary>Identifies the <see cref="Volume"/> dependency property.</summary>
	public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
		nameof(Volume), typeof(double), typeof(VideoPlayer),
		new PropertyMetadata(1.0, (o, e) => ((VideoPlayer)o)._session.Volume = (float)Math.Clamp((double)e.NewValue, 0.0, 1.0)));

	/// <summary>Soundtrack volume from 0.0 (silent) to 1.0 (unity gain, the default).</summary>
	public double Volume
	{
		get => (double)GetValue(VolumeProperty);
		set => SetValue(VolumeProperty, value);
	}

	/// <summary>Identifies the <see cref="IsMuted"/> dependency property.</summary>
	public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
		nameof(IsMuted), typeof(bool), typeof(VideoPlayer),
		new PropertyMetadata(false, (o, e) => ((VideoPlayer)o)._session.IsMuted = (bool)e.NewValue));

	/// <summary>True to silence the soundtrack without losing the <see cref="Volume"/> setting.</summary>
	public bool IsMuted
	{
		get => (bool)GetValue(IsMutedProperty);
		set => SetValue(IsMutedProperty, value);
	}

	/// <summary>Identifies the <see cref="IsLooping"/> dependency property.</summary>
	public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
		nameof(IsLooping), typeof(bool), typeof(VideoPlayer),
		new PropertyMetadata(false, (o, e) => ((VideoPlayer)o)._session.IsLooping = (bool)e.NewValue));

	/// <summary>When true, playback restarts from the beginning at the end of the video.</summary>
	public bool IsLooping
	{
		get => (bool)GetValue(IsLoopingProperty);
		set => SetValue(IsLoopingProperty, value);
	}

	/// <summary>Identifies the <see cref="PositionUpdateInterval"/> dependency property.</summary>
	public static readonly DependencyProperty PositionUpdateIntervalProperty = DependencyProperty.Register(
		nameof(PositionUpdateInterval), typeof(TimeSpan), typeof(VideoPlayer),
		new PropertyMetadata(TimeSpan.FromMilliseconds(150), (o, e) => ((VideoPlayer)o).OnPositionUpdateIntervalChanged((TimeSpan)e.NewValue)));

	/// <summary>
	/// How often <see cref="Position"/> / <see cref="PositionSeconds"/> refresh while playing.
	/// Defaults to 150 milliseconds.
	/// </summary>
	public TimeSpan PositionUpdateInterval
	{
		get => (TimeSpan)GetValue(PositionUpdateIntervalProperty);
		set => SetValue(PositionUpdateIntervalProperty, value);
	}

	/// <summary>Identifies the <see cref="Stretch"/> dependency property.</summary>
	public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
		nameof(Stretch), typeof(Stretch), typeof(VideoPlayer),
		new PropertyMetadata(Stretch.Uniform, (o, e) => ((VideoPlayer)o)._surface.Stretch = (Stretch)e.NewValue));

	/// <summary>
	/// How the picture fits the element: None keeps its pixel size, Fill takes the whole element,
	/// Uniform (the default) fits it inside with letterbox bars, and UniformToFill covers the
	/// element and crops. Applied at paint time, so changing it costs nothing but a repaint.
	/// </summary>
	public Stretch Stretch
	{
		get => (Stretch)GetValue(StretchProperty);
		set => SetValue(StretchProperty, value);
	}

	/// <summary>Identifies the <see cref="RenderPath"/> dependency property.</summary>
	public static readonly DependencyProperty RenderPathProperty = DependencyProperty.Register(
		nameof(RenderPath), typeof(VideoRenderPath), typeof(VideoPlayer),
		new PropertyMetadata(VideoRenderPath.GpuAuto, (o, e) => ((VideoPlayer)o).OnRenderPathChanged((VideoRenderPath)e.OldValue, (VideoRenderPath)e.NewValue)));

	/// <summary>
	/// Which render path this player wants, and what to do when the graphics one cannot be had:
	/// GpuAuto (the default) takes the graphics device where a context can be created and quietly
	/// falls back to the processor where it cannot; GpuNoFallback fails with a message instead of
	/// degrading, for an application whose picture is meaningless without its effect chain; Cpu
	/// forces the processor path even where a graphics device exists.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the value is changed while a source is open. The path is chosen once, before
	/// anything is opened - exactly as the game engine's canvas chooses its render tier before its
	/// pipeline exists.
	/// </exception>
	public VideoRenderPath RenderPath
	{
		get => (VideoRenderPath)GetValue(RenderPathProperty);
		set => SetValue(RenderPathProperty, value);
	}

	/// <summary>Identifies the <see cref="AllowEffectsOnCpu"/> dependency property.</summary>
	public static readonly DependencyProperty AllowEffectsOnCpuProperty = DependencyProperty.Register(
		nameof(AllowEffectsOnCpu), typeof(bool), typeof(VideoPlayer),
		new PropertyMetadata(false, (o, e) => ((VideoPlayer)o).OnAllowEffectsOnCpuChanged((bool)e.NewValue)));

	/// <summary>
	/// True to apply <see cref="Effects"/> on the processor path too. Defaults to false, in which
	/// case a configured chain stays configured but is not applied when the processor path is what
	/// is running - and <see cref="EffectsActive"/> says so. Turning it on costs a table lookup per
	/// pixel of every frame.
	/// </summary>
	public bool AllowEffectsOnCpu
	{
		get => (bool)GetValue(AllowEffectsOnCpuProperty);
		set => SetValue(AllowEffectsOnCpuProperty, value);
	}

	/// <summary>Identifies the <see cref="ActiveRenderPath"/> dependency property.</summary>
	public static readonly DependencyProperty ActiveRenderPathProperty = DependencyProperty.Register(
		nameof(ActiveRenderPath), typeof(VideoRenderBackend), typeof(VideoPlayer),
		new PropertyMetadata(VideoRenderBackend.Cpu));

	/// <summary>Which render path is actually running (read-only).</summary>
	public VideoRenderBackend ActiveRenderPath
	{
		get => (VideoRenderBackend)GetValue(ActiveRenderPathProperty);
		private set => SetValue(ActiveRenderPathProperty, value);
	}

	/// <summary>Identifies the <see cref="EffectsActive"/> dependency property.</summary>
	public static readonly DependencyProperty EffectsActiveProperty = DependencyProperty.Register(
		nameof(EffectsActive), typeof(bool), typeof(VideoPlayer), new PropertyMetadata(false));

	/// <summary>True when the configured <see cref="Effects"/> are actually being applied (read-only).</summary>
	public bool EffectsActive
	{
		get => (bool)GetValue(EffectsActiveProperty);
		private set => SetValue(EffectsActiveProperty, value);
	}

	/// <summary>Identifies the <see cref="SelectedCaptionTrack"/> dependency property.</summary>
	public static readonly DependencyProperty SelectedCaptionTrackProperty = DependencyProperty.Register(
		nameof(SelectedCaptionTrack), typeof(CaptionTrack), typeof(VideoPlayer),
		new PropertyMetadata(null, (o, e) => ((VideoPlayer)o)._session.SelectedCaptionTrack = (CaptionTrack)e.NewValue));

	/// <summary>
	/// The caption track whose cues should surface in <see cref="ActiveCues"/>, or null for none.
	/// </summary>
	/// <remarks>
	/// Captions are DATA here: the player carries them and says which are current, and the
	/// application decides how - and whether - to draw them (an <see cref="IVideoLayer"/> is the
	/// natural place).
	/// </remarks>
	public CaptionTrack? SelectedCaptionTrack
	{
		get => (CaptionTrack?)GetValue(SelectedCaptionTrackProperty);
		set => SetValue(SelectedCaptionTrackProperty, value);
	}

	/// <summary>Identifies the <see cref="ShowForcedCaptions"/> dependency property.</summary>
	public static readonly DependencyProperty ShowForcedCaptionsProperty = DependencyProperty.Register(
		nameof(ShowForcedCaptions), typeof(bool), typeof(VideoPlayer),
		new PropertyMetadata(true, (o, e) => ((VideoPlayer)o)._session.ShowForcedCaptions = (bool)e.NewValue));

	/// <summary>
	/// True to surface a forced caption track's cues even when no track is selected - the signs and
	/// foreign dialogue a viewer is meant to read whatever they chose. Defaults to true.
	/// </summary>
	public bool ShowForcedCaptions
	{
		get => (bool)GetValue(ShowForcedCaptionsProperty);
		set => SetValue(ShowForcedCaptionsProperty, value);
	}

	#endregion

	#region | Effects, layers and composition |

	/// <summary>
	/// The ordered chain of frame effects applied to the picture - colour lookup tables, first and
	/// foremost. However many are in the chain, they are composed into ONE resultant table and cost
	/// a single lookup per pixel.
	/// </summary>
	/// <remarks>
	/// Applied on the graphics path; on the processor path they are ignored unless
	/// <see cref="AllowEffectsOnCpu"/> is true. <see cref="EffectsActive"/> is what says which of
	/// those is happening. The collection belongs to this element and keeps its identity for the
	/// element's whole life, so a binding to it never goes stale.
	/// </remarks>
	public ObservableCollection<IVideoFrameEffect> Effects => _effects;

	/// <summary>
	/// The ordered layers drawn OVER the video, inside the composition, before the picture reaches
	/// the screen: subtitles, a heads-up display, annotation, a webcam picture-in-picture.
	/// </summary>
	/// <remarks>
	/// A layer draws in video coordinates, so what it draws is part of the picture -
	/// <see cref="CapturePresentedFrame"/> captures it too. For a one-off, handle
	/// <see cref="Composing"/> instead of writing a layer.
	/// </remarks>
	public ObservableCollection<IVideoLayer> Layers => _layers;

	#endregion

	#region | What is playing |

	/// <summary>Every track the container declares - video, audio and captions together.</summary>
	public IReadOnlyList<MediaTrackInfo> Tracks => _session.Tracks;

	/// <summary>The file's text caption tracks.</summary>
	public IReadOnlyList<CaptionTrack> CaptionTracks => _session.CaptionTracks;

	/// <summary>The captions that should be on screen right now. Never null; usually empty.</summary>
	public IReadOnlyList<CaptionCue> ActiveCues => _session.ActiveCues;

	/// <summary>The file's chapters, in order. Empty when the file has none.</summary>
	public IReadOnlyList<Chapter> Chapters => _session.Chapters;

	/// <summary>The chapter playback is inside, or null when it is not inside one.</summary>
	public Chapter? CurrentChapter => _session.CurrentChapter;

	/// <summary>
	/// How many frames the decoder has posted, presented and dropped so far - the numbers a
	/// diagnostic readout wants.
	/// </summary>
	public VideoFramePresenterStatistics FrameStatistics => _session.Presenter.GetStatistics();

	#endregion

	#region | Transport |

	/// <summary>Starts or resumes playback of the loaded source.</summary>
	public void Play()
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		try
		{
			_session.Play();
		}
		catch (Exception e)
		{
			ReportFailure("Playback could not be started.", e);
			return;
		}
		IsPlaying = true;
		StartPositionTimer();
	}

	/// <summary>Pauses playback, keeping the current position and leaving the picture on screen.</summary>
	public void Pause()
	{
		if (_isDisposed)
		{
			return;
		}

		_session.Pause();
		IsPlaying = false;
		StopPositionTimer();
		RefreshPositionFromPlayback();
	}

	/// <summary>Stops playback and rewinds to the beginning.</summary>
	public void Stop()
	{
		if (_isDisposed)
		{
			return;
		}

		try
		{
			_session.Stop();
		}
		catch (Exception e)
		{
			ReportFailure("Playback could not be stopped.", e);
		}

		IsPlaying = false;
		StopPositionTimer();
		RefreshPositionFromPlayback();
		_driver.PresentPausedFrame();
	}

	/// <summary>
	/// Jumps playback to <paramref name="position"/> immediately (no debounce); playback continues
	/// from there when playing, and the frame at that moment is presented when paused.
	/// </summary>
	/// <param name="position">Where to move to. Values outside the video are clamped into it.</param>
	public void Seek(TimeSpan position)
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		_seekDebounceTimer?.Stop();
		SeekCore(position);
	}

	/// <summary>
	/// Loads a source from a stream, in any container this player reads (for sources that are
	/// neither files nor embedded resources). The stream should be seekable; the player takes
	/// ownership and closes it when another source is loaded. Clears <see cref="Source"/>.
	/// </summary>
	/// <param name="stream">The bytes to play.</param>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
	public void SetSourceStream(Stream stream)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		Source = "";
		LoadCore(() => _session.Open(stream), "stream");
	}

	/// <summary>Jumps to the start of a chapter.</summary>
	/// <param name="index">The chapter's index in <see cref="Chapters"/>.</param>
	public void SeekToChapter(int index)
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		try
		{
			_session.SeekToChapter(index);
			AfterSeek();
		}
		catch (Exception e)
		{
			ReportFailure($"Could not move to chapter {index}.", e);
		}
	}

	/// <summary>Jumps to the start of the next chapter.</summary>
	/// <returns>True when there was one to move to.</returns>
	public bool NextChapter() => MoveChapter(_session.NextChapter);

	/// <summary>Jumps to the start of the previous chapter.</summary>
	/// <returns>True when there was one to move to.</returns>
	public bool PreviousChapter() => MoveChapter(_session.PreviousChapter);

	/// <summary>
	/// Returns an independent copy of the picture on screen - the composed frame, with the effect
	/// chain and every layer already in it - which the caller owns and must dispose, or null when
	/// nothing has been presented yet.
	/// </summary>
	/// <remarks>
	/// Safe to call from any thread: it is taken under the same gate the present path uses. This is
	/// the screenshot hook, and the way a headless verification proves that pixels actually flowed.
	/// </remarks>
	public SKImage? CapturePresentedFrame() => _surface.CapturePresentedFrame();

	#endregion

	#region | Source loading |

	private void OnSourceChanged(string newSource)
	{
		if (string.IsNullOrEmpty(newSource))
		{
			UnloadSource();
			return;
		}

		LoadCore(
			() =>
			{
				// ANDROID PORT (1): an ms-appx:/// video is an APK asset; copy it out before it is opened by path.
				PackagedVideoAssets.EnsureCopiedOutForSource(newSource);
				var (pathOrUrl, stream) = VideoSourceResolver.Resolve(newSource);
				if (pathOrUrl is not null)
				{
					_session.Open(pathOrUrl, SourceMode);
				}
				else
				{
					_session.Open(stream!);
				}
			},
			newSource);
	}

	private void LoadCore(Action load, string sourceDescription)
	{
		StopPositionTimer();
		IsPlaying = false;
		_surface.ClearFrame();

		try
		{
			RunOffSynchronizationContext(load);
		}
		catch (Exception e)
		{
			_isSourceLoaded = false;
			Duration = TimeSpan.Zero;
			DurationSeconds = 0.0;

			// The engine's own message names the missing piece and the call to make; Amend adds the
			// NuGet package id, which is the part somebody has to type, and leaves every other
			// failure untouched.
			ReportFailure(
				VideoFailureExplanation.Amend($"The video source '{sourceDescription}' could not be opened.", e.Message),
				e);
			return;
		}

		_isSourceLoaded = true;
		_session.Volume = (float)Math.Clamp(Volume, 0.0, 1.0);
		_session.IsMuted = IsMuted;
		_session.IsLooping = IsLooping;
		_session.ShowForcedCaptions = ShowForcedCaptions;

		Duration = _session.Duration;
		DurationSeconds = _session.Duration.TotalSeconds;
		RefreshPositionFromPlayback();

		// The presenter reads the session's mailbox; a new session state means re-attaching it and
		// settling which path will run before the first frame arrives.
		if (_driver.Presenter is { } presenter)
		{
			presenter.Attach(_session.Presenter);
		}
		_driver.ResolveRenderPath();

		// The container is read - and its first frame decoded and posted to the session's mailbox -
		// BEFORE the presenter above was attached to that mailbox, so the notification that came
		// with it reached nobody and the driver has not been told there is a picture waiting.
		// Ask for one frame now: a player that has opened a source shows that source's first
		// picture, whether or not it is ever played.
		_driver.PresentPausedFrame();

		if (AutoPlay)
		{
			Play();
		}
	}

	/// <summary>
	/// Runs a source open with no <see cref="SynchronizationContext"/> in scope, then waits for it.
	/// </summary>
	/// <remarks>
	/// Opening reads the container's header and index and starts the soundtrack's device, some of
	/// which is asynchronous work waited on from a synchronous entry point - which deadlocks if a
	/// UI synchronization context is in scope. Streaming a file (the default
	/// <see cref="SourceMode"/>) reads only the header and index, so the wait is short; preloading
	/// deliberately reads the whole file and takes as long as that takes.
	/// </remarks>
	private static void RunOffSynchronizationContext(Action load)
	{
		if (SynchronizationContext.Current is null)
		{
			load();
			return;
		}

		// GetAwaiter().GetResult() rethrows the original exception rather than an AggregateException,
		// so LoadCore's catch block still sees the real failure.
		Task.Run(load).GetAwaiter().GetResult();
	}

	private void UnloadSource()
	{
		StopPositionTimer();
		IsPlaying = false;
		_isSourceLoaded = false;
		_session.Close();
		_surface.ClearFrame();
		Duration = TimeSpan.Zero;
		DurationSeconds = 0.0;
		RefreshPositionFromPlayback();
	}

	private void OnSessionMediaOpened(object? sender, EventArgs e) =>
		OnUiThread(() =>
		{
			Duration = _session.Duration;
			DurationSeconds = _session.Duration.TotalSeconds;
			MediaOpened?.Invoke(this, EventArgs.Empty);
		});

	private void OnSessionPlaybackEnded(object? sender, EventArgs e) =>
		OnUiThread(() =>
		{
			IsPlaying = false;
			StopPositionTimer();
			RefreshPositionFromPlayback();
			PlaybackEnded?.Invoke(this, EventArgs.Empty);
		});

	private void OnSessionMediaFailed(object? sender, MediaFailedEventArgs e) =>
		OnUiThread(() => ReportFailure(VideoFailureExplanation.Amend(e.Message, e.Message), e.Exception));

	private void OnSessionCaptionCuesChanged(object? sender, EventArgs e) =>
		OnUiThread(() => CaptionCuesChanged?.Invoke(this, EventArgs.Empty));

	private void OnSessionChapterChanged(object? sender, ChapterChangedEventArgs e) =>
		OnUiThread(() => ChapterChanged?.Invoke(this, e));

	/// <summary>
	/// Runs <paramref name="action"/> on the UI thread. The playback session raises its events from
	/// whichever of its own threads noticed the change, so everything this element does with them
	/// has to be marshalled.
	/// </summary>
	private void OnUiThread(Action action)
	{
		var dispatcherQueue = DispatcherQueue;
		if (dispatcherQueue is null || dispatcherQueue.HasThreadAccess)
		{
			action();
			return;
		}

		dispatcherQueue.TryEnqueue(() => action());
	}

	private void ReportFailure(string message, Exception? error)
	{
		// A failure on the frame path repeats on every frame; saying the same sentence sixty times
		// a second helps nobody.
		if (string.Equals(message, _lastFailureMessage, StringComparison.Ordinal))
		{
			return;
		}
		_lastFailureMessage = message;

		if (this.Log().IsEnabled(LogLevel.Error))
		{
			this.Log().Error(message, error);
		}
		MediaFailed?.Invoke(this, new VideoPlayerFailedEventArgs(message, error));
	}

	#endregion

	#region | Render path and composition wiring |

	// Applies this element's settings to a presenter the driver has just created. Called on the UI
	// thread, before the presenter composes anything.
	private void ConfigurePresenter(VideoPresenter presenter)
	{
		presenter.RenderPath = RenderPath;
		presenter.AllowEffectsOnCpu = AllowEffectsOnCpu;
		presenter.RenderPathChanged += (_, _) => OnUiThread(PublishRenderPath);
		presenter.Composing += (_, args) => Composing?.Invoke(this, args);

		CopyInto(presenter.Effects, _effects);
		CopyInto(presenter.Layers, _layers);

		if (_isSourceLoaded)
		{
			presenter.Attach(_session.Presenter);
		}
	}

	private static void CopyInto<T>(ObservableCollection<T> target, ObservableCollection<T> source)
	{
		target.Clear();
		foreach (var item in source)
		{
			target.Add(item);
		}
	}

	// The chains are short and change rarely, so replacing them wholesale is simpler - and cheaper
	// to reason about - than replaying each collection change.
	private void OnEffectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (_driver.Presenter is { } presenter)
		{
			CopyInto(presenter.Effects, _effects);
		}

		// EffectsActive is a function of the chain's length, the settled backend and
		// AllowEffectsOnCpu - so a chain edit changes it without any RenderPathChanged from the
		// presenter. Publish it now (on the user-interface thread: it writes dependency
		// properties) rather than leaving the read-only property stale until the next event.
		OnUiThread(PublishRenderPath);

		if (!IsPlaying)
		{
			_driver.PresentPausedFrame();
		}
	}

	private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (_driver.Presenter is { } presenter)
		{
			CopyInto(presenter.Layers, _layers);
		}

		if (!IsPlaying)
		{
			_driver.PresentPausedFrame();
		}
	}

	private void OnRenderPathChanged(VideoRenderPath oldValue, VideoRenderPath newValue)
	{
		if (_revertingRenderPath || oldValue == newValue)
		{
			return;
		}

		if (!VideoPlayerRules.IsRenderPathChangeAllowed(_isSourceLoaded, oldValue, newValue))
		{
			_revertingRenderPath = true;
			RenderPath = oldValue;
			_revertingRenderPath = false;

			throw new InvalidOperationException(
				VideoPlayerRules.RenderPathChangeRefusal(nameof(RenderPath), nameof(Source)));
		}

		if (_driver.Presenter is { } presenter)
		{
			presenter.RenderPath = newValue;
		}
		_driver.ResolveRenderPath();
	}

	private void OnAllowEffectsOnCpuChanged(bool newValue)
	{
		if (_driver.Presenter is { } presenter)
		{
			presenter.AllowEffectsOnCpu = newValue;
		}

		// Same reason as OnEffectsChanged: this flips EffectsActive on the processor path with no
		// event from the presenter.
		OnUiThread(PublishRenderPath);

		if (!IsPlaying)
		{
			_driver.PresentPausedFrame();
		}
	}

	// Publishes what is actually running. Called on the UI thread after every resolve.
	private void PublishRenderPath()
	{
		var presenter = _driver.Presenter;
		var backend = presenter?.ActiveRenderPath ?? VideoRenderBackend.Cpu;
		var effectsActive = presenter?.EffectsActive ?? false;

		if (ActiveRenderPath == backend && EffectsActive == effectsActive)
		{
			return;
		}

		ActiveRenderPath = backend;
		EffectsActive = effectsActive;
		RenderPathChanged?.Invoke(this, new VideoPlayerRenderPathChangedEventArgs(backend, effectsActive));
	}

	#endregion

	#region | Layout |

	/// <inheritdoc/>
	protected override Size MeasureOverride(Size availableSize)
	{
		// The picture surface takes whatever the element is given; the element itself asks for
		// nothing, so a page decides how big the video is (as an Image with Stretch does).
		foreach (var child in Children)
		{
			child.Measure(availableSize);
		}

		return new Size(0, 0);
	}

	/// <inheritdoc/>
	protected override Size ArrangeOverride(Size finalSize)
	{
		foreach (var child in Children)
		{
			child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
		}

		return finalSize;
	}

	#endregion

	#region | Position updates and debounced seeking |

	private void OnPositionChanged(TimeSpan newPosition)
	{
		if (_syncingPositionPair)
		{
			return;
		}

		_syncingPositionPair = true;
		PositionSeconds = newPosition.TotalSeconds;
		_syncingPositionPair = false;

		if (!_updatingFromPlayback)
		{
			QueueSeek(newPosition);
		}
	}

	private void OnPositionSecondsChanged(double newSeconds)
	{
		if (_syncingPositionPair)
		{
			return;
		}

		_syncingPositionPair = true;
		Position = TimeSpan.FromSeconds(newSeconds);
		_syncingPositionPair = false;

		if (!_updatingFromPlayback)
		{
			QueueSeek(TimeSpan.FromSeconds(newSeconds));
		}
	}

	private void QueueSeek(TimeSpan position)
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		_pendingSeek = position;
		if (_seekDebounceTimer is null)
		{
			_seekDebounceTimer = DispatcherQueue.CreateTimer();
			_seekDebounceTimer.Interval = SeekDebounceInterval;
			_seekDebounceTimer.IsRepeating = false;
			_seekDebounceTimer.Tick += (_, _) =>
			{
				if (_isSourceLoaded)
				{
					SeekCore(_pendingSeek);
				}
			};
		}

		// Restarting on every write coalesces a whole slider drag into one seek on release.
		_seekDebounceTimer.Stop();
		_seekDebounceTimer.Start();
	}

	private void SeekCore(TimeSpan position)
	{
		try
		{
			_session.Seek(ClampToDuration(position));
			AfterSeek();
		}
		catch (Exception e)
		{
			ReportFailure("The video could not be moved to that position.", e);
		}
	}

	// A seek while paused has to reach the screen: the decoder posts the frame at the new position
	// and this is what asks for it to be composed and presented without playback running.
	private void AfterSeek()
	{
		RefreshPositionFromPlayback();
		if (!IsPlaying)
		{
			_driver.PresentPausedFrame();
		}
	}

	private bool MoveChapter(Func<bool> move)
	{
		if (!_isSourceLoaded)
		{
			return false;
		}

		try
		{
			var moved = move();
			if (moved)
			{
				AfterSeek();
			}
			return moved;
		}
		catch (Exception e)
		{
			ReportFailure("The video could not be moved to that chapter.", e);
			return false;
		}
	}

	private void StartPositionTimer()
	{
		if (_positionTimer is null)
		{
			_positionTimer = DispatcherQueue.CreateTimer();
			_positionTimer.Interval = PositionUpdateInterval;
			_positionTimer.Tick += (_, _) => RefreshPositionFromPlayback();
		}
		_positionTimer.Start();
	}

	private void StopPositionTimer() => _positionTimer?.Stop();

	private void OnPositionUpdateIntervalChanged(TimeSpan newInterval)
	{
		if (_positionTimer is not null)
		{
			_positionTimer.Interval = newInterval;
		}
	}

	private void RefreshPositionFromPlayback()
	{
		_updatingFromPlayback = true;
		Position = _isSourceLoaded && !_isDisposed ? _session.Position : TimeSpan.Zero;
		_updatingFromPlayback = false;
	}

	private TimeSpan ClampToDuration(TimeSpan position) =>
		VideoPlayerRules.ClampToDuration(position, _session.Duration);

	#endregion

	#region | Teardown |

	/// <summary>
	/// Stops playback and releases everything this player owns: the decode threads, the soundtrack,
	/// the composition surface and the graphics context. The element cannot play again afterwards.
	/// </summary>
	/// <remarks>
	/// Optional. The element already gives up its graphics resources when it leaves the visual tree
	/// (which is the only moment the underlying window is reliably still alive), and pauses
	/// playback with it. Call this when a page wants the decode threads and the audio device gone
	/// at a moment of its choosing rather than at the next collection. (The name is Close rather
	/// than Shutdown because UIElement already has an internal Shutdown of its own, which nothing
	/// may hide.)
	/// </remarks>
	public void Close()
	{
		if (_isDisposed)
		{
			return;
		}
		_isDisposed = true;

		StopPositionTimer();
		_seekDebounceTimer?.Stop();

		_session.MediaOpened -= OnSessionMediaOpened;
		_session.PlaybackEnded -= OnSessionPlaybackEnded;
		_session.MediaFailed -= OnSessionMediaFailed;
		_session.CaptionCuesChanged -= OnSessionCaptionCuesChanged;
		_session.ChapterChanged -= OnSessionChapterChanged;

		_effects.CollectionChanged -= OnEffectsChanged;
		_layers.CollectionChanged -= OnLayersChanged;

		// The driver goes first: it is the one holding surfaces on a graphics context, and it needs
		// the presenter still reading a live mailbox while it releases them.
		_driver.Dispose();
		_surface.Teardown();
		_session.Dispose();

		_isSourceLoaded = false;
		IsPlaying = false;
	}

	#endregion
}
