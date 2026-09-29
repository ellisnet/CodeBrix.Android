// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.AudioPlayer.Skia/MidiPlayer.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// The same public type in the same namespace: a pasted page's MidiPlayer is this one on Android. CodeBrix.Audio.Core is the
// shared managed audio library (MIDI files, SoundFont/SFZ/Decent Sampler synthesizers); CodeBrix.Audio.Android plays the output.
// Changes are marked ANDROID PORT: (1) an ms-appx:/// sequence or instrument is copied out of the APK before it is opened
// (an SFZ/Decent Sampler preset with its folder) - PackagedAudioAssets; (2) usings of this assembly's namespace; (3) this assembly
// sees UI.Core's internals (InternalsVisibleTo), so three PropertyMetadata callbacks are cast to PropertyChangedCallback (an
// internal overload made them ambiguous) and IsLoading's hiding of the internal UIElement.IsLoading is acknowledged.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using CodeBrix.Audio.Playback;
using CodeBrix.Audio.Synth;
using CodeBrix.Audio.Synth.DecentSampler;
using CodeBrix.Audio.Synth.Mpe;
using CodeBrix.Audio.Synth.Sfz;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Internal;
using CodeBrix.Android.UI.AudioPlayer.Android; // ANDROID PORT (2)

namespace CodeBrix.Platform.UI.AudioPlayer.Skia;

/// <summary>
/// A non-visual, XAML-declarable player for MIDI music, rendered through a SoundFont (.sf2), an
/// SFZ (.sfz) or a Decent Sampler (.dspreset, .dslibrary, .dsbundle) instrument. It is the
/// synthesized counterpart of <see cref="AudioPlayer"/> and carries the same transport: point
/// <see cref="Source"/> at a MIDI file and <see cref="Instrument"/> at the instrument to render it
/// with, then control playback with <see cref="Play"/> / <see cref="Pause"/> / <see cref="Stop"/> /
/// <see cref="Seek"/>.
///
/// The bindable properties match <see cref="AudioPlayer"/> exactly - <see cref="Position"/> /
/// <see cref="PositionSeconds"/> follow playback and seek when written (debounced for
/// scrubbing), and <see cref="Duration"/> / <see cref="DurationSeconds"/> give a Slider its
/// Maximum - so the same scrubber markup drives either player. On top of those sit the things only
/// a sequence can offer: <see cref="Speed"/> (tempo with no pitch change), the per-channel mixing
/// calls, and <see cref="MidiMessageProcessed"/> for reacting to the notes as they play.
/// </summary>
/// <remarks>
/// <para>
/// LOADING IS ASYNCHRONOUS, which is the one way this differs from <see cref="AudioPlayer"/>.
/// Instruments are large - a sampled piano is hundreds of megabytes of decoded audio and takes
/// seconds to read - so setting <see cref="Source"/> or <see cref="Instrument"/> starts a background
/// load, raises <see cref="IsLoading"/>, and returns immediately. <see cref="Duration"/> is
/// available, and the transport works, once <see cref="MediaOpened"/> has been raised;
/// <see cref="MediaFailed"/> reports a load that did not get there. Both events are raised on the
/// UI thread.
/// </para>
/// <para>
/// Instruments are cached across every <see cref="MidiPlayer"/> in the application, so two players
/// sharing an instrument pay for it once. A Decent Sampler instrument is shared through the audio
/// engine's own process-wide cache, so a player and an application that pre-loaded the same path
/// through CodeBrix.Audio share one copy as well. That sharing has a consequence worth knowing:
/// a Decent Sampler instrument keeps its knob values on the instrument, so two players naming the
/// same preset also share one set of <see cref="InstrumentControls"/> values.
/// </para>
/// <para>
/// SOURCE FORMS: <see cref="Source"/> and a <c>.sf2</c> <see cref="Instrument"/> accept everything
/// <see cref="AudioPlayer.Source"/> does - a file path, an ms-appx:/// asset URI or an
/// embedded://Assembly/Resource.Name URI. An <c>.sfz</c> or Decent Sampler instrument accepts only
/// the forms that name something real on disk (a path or an ms-appx:/// URI): an SFZ instrument and
/// a <c>.dspreset</c> reference their samples as separate files beside them, and a <c>.dslibrary</c>
/// or <c>.dsbundle</c>, though it is one file, is read in place from the archive on disk.
/// </para>
/// </remarks>
[Bindable]
public sealed partial class MidiPlayer : FrameworkElement
{
	// Instruments are immutable once loaded and safe to share, and they are the expensive thing
	// here - so they are cached for the whole application rather than per player.
	private static readonly SfzInstrumentCache _sfzInstruments = new();
	private static readonly SoundFontCache _soundFonts = new();

	// A Slider drag writes the bound position on every tick of thumb travel; the seek runs only
	// after the value has been stable for this long, landing one seek per gesture.
	private static readonly TimeSpan SeekDebounceInterval = TimeSpan.FromMilliseconds(200);

	// The advice the audio engine writes into an instrument's problems when a preset asks for a
	// sound only the CodeBrix.Audio.ModestSynth add-on can make. This package does not reference
	// that add-on - the application does, exactly as it does for the Opus codec - so all this
	// element can do is pass the engine's own line on, once, as a warning.
	private const string AddOnMarker = "ModestSynth";

	private readonly MidiMusicPlayer _player = new();
	private readonly SemaphoreSlim _loadGate = new(1, 1);

	// A control can be moved by a modulator or a MIDI binding on the real-time AUDIO thread, many
	// times per rendered block, so changes are collected here and raised once per control per
	// dispatcher pass instead of once per movement.
	private readonly object _controlChangeGate = new();
	private readonly Dictionary<DecentSamplerControl, DecentSamplerControlChangedEventArgs> _pendingControlChanges = new();

	private DispatcherQueueTimer? _positionTimer;
	private DispatcherQueueTimer? _seekDebounceTimer;
	private DispatcherQueue? _uiQueue;  // captured on the UI thread; the audio thread marshals onto it
	private DecentSamplerInstrument? _decentSamplerInstrument;
	private bool _controlsSubscribed;
	private bool _controlFlushQueued;
	private bool _updatingFromPlayback; // set while playback progress writes the position DPs
	private bool _syncingPositionPair;  // set while Position and PositionSeconds mirror each other
	private TimeSpan _pendingSeek;
	private bool _isSourceLoaded;
	private bool _loadQueued;
	private int _loadGeneration;

	/// <summary>Creates a player with nothing loaded.</summary>
	public MidiPlayer()
	{
		Unloaded += (_, _) =>
		{
			Pause();

			// A Decent Sampler instrument outlives this element - it belongs to the audio engine's
			// process-wide cache - so an element leaving the tree lets go of its controls rather
			// than keeping itself, and its page, alive through their Changed handlers.
			DetachInstrumentControls();
		};
		Loaded += (_, _) => AttachInstrumentControls();
	}

	/// <summary>
	/// Raised (on the UI thread) when an instrument and sequence have finished loading and the
	/// transport is ready. <see cref="Duration"/> is set by the time this is raised.
	/// </summary>
	public event EventHandler? MediaOpened;

	/// <summary>
	/// Raised (on the UI thread) when the sequence reaches its end. Not raised when
	/// <see cref="IsLooping"/> is true, when <see cref="Stop"/> is called, or when playback fails.
	/// </summary>
	public event EventHandler? PlaybackEnded;

	/// <summary>
	/// Raised (on the UI thread) when a source or instrument fails to load or play - a missing
	/// file, an unreadable MIDI file, or an instrument format that must name a file on disk given in
	/// a form that cannot.
	/// </summary>
	public event EventHandler<AudioPlayerFailedEventArgs>? MediaFailed;

	/// <summary>
	/// Raised (on the UI thread) when a control of the loaded Decent Sampler instrument changes -
	/// because something set its value, or because a modulator or a MIDI binding inside the preset
	/// moved it while the music played.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Bindings can move a control on the real-time audio thread many times per rendered block, so
	/// changes are coalesced: each control that moved is reported once per dispatcher pass, carrying
	/// the last change seen for it. Read the current value from the control the event args carry.
	/// </para>
	/// <para>
	/// Never raised for a SoundFont or SFZ instrument, which have no control model. An element built
	/// outside a UI thread's dispatcher has nothing to marshal onto and raises this on the thread
	/// the change arrived on.
	/// </para>
	/// </remarks>
	public event EventHandler<DecentSamplerControlChangedEventArgs>? InstrumentControlChanged;

	#region | Dependency properties |

	/// <summary>Identifies the <see cref="Source"/> dependency property.</summary>
	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
		nameof(Source), typeof(string), typeof(MidiPlayer),
		new PropertyMetadata("", (PropertyChangedCallback)((o, e) => ((MidiPlayer)o).QueueLoad()))); // ANDROID PORT (3)

	/// <summary>
	/// The MIDI file to play: a file path, an ms-appx:/// asset URI, or an
	/// embedded://Assembly/Resource.Name embedded-resource URI. Playback needs an
	/// <see cref="Instrument"/> as well; loading starts once both are set. Set an empty string to
	/// unload.
	/// </summary>
	public string Source
	{
		get => (string)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	/// <summary>Identifies the <see cref="Instrument"/> dependency property.</summary>
	public static readonly DependencyProperty InstrumentProperty = DependencyProperty.Register(
		nameof(Instrument), typeof(string), typeof(MidiPlayer),
		new PropertyMetadata("", (PropertyChangedCallback)((o, e) => ((MidiPlayer)o).QueueLoad()))); // ANDROID PORT (3)

	/// <summary>
	/// The instrument the sequence is rendered through: a <c>.sf2</c> SoundFont, a <c>.sfz</c>
	/// instrument, or a Decent Sampler instrument as a <c>.dspreset</c>, a <c>.dslibrary</c>, a
	/// <c>.dsbundle</c> or a folder holding a <c>.dspreset</c>. What it names decides which
	/// synthesizer runs, and <see cref="InstrumentKind"/> reports which one did.
	/// </summary>
	/// <remarks>
	/// A <c>.sf2</c> accepts every form <see cref="Source"/> does. The other two formats accept a
	/// file path or an ms-appx:/// URI only - see the note on the class - and any other form fails
	/// with <see cref="MediaFailed"/> saying which format needs one and why. A folder is read as a
	/// Decent Sampler instrument when it holds a <c>.dspreset</c>, which is the shape a
	/// <c>.dsbundle</c> has on macOS and the shape an unpacked library has everywhere.
	/// </remarks>
	public string Instrument
	{
		get => (string)GetValue(InstrumentProperty);
		set => SetValue(InstrumentProperty, value);
	}

	/// <summary>Identifies the <see cref="InstrumentPreset"/> dependency property.</summary>
	public static readonly DependencyProperty InstrumentPresetProperty = DependencyProperty.Register(
		nameof(InstrumentPreset), typeof(string), typeof(MidiPlayer),
		new PropertyMetadata("", (PropertyChangedCallback)((o, e) => ((MidiPlayer)o).QueueLoad()))); // ANDROID PORT (3)

	/// <summary>
	/// Which preset to play from a Decent Sampler container that holds several - a
	/// <c>.dslibrary</c> with a preset menu, for instance. Empty, the default, takes the container's
	/// first preset. Changing it queues a load, exactly as changing <see cref="Instrument"/> does.
	/// </summary>
	/// <remarks>
	/// Ignored by SoundFont and SFZ instruments, which hold one instrument each. The name is part of
	/// what the shared instrument cache keys on, so two presets of one library are two cached
	/// instruments sharing the library's sample data.
	/// </remarks>
	public string InstrumentPreset
	{
		get => (string)GetValue(InstrumentPresetProperty);
		set => SetValue(InstrumentPresetProperty, value);
	}

	/// <summary>Identifies the <see cref="InstrumentKind"/> dependency property.</summary>
	public static readonly DependencyProperty InstrumentKindProperty = DependencyProperty.Register(
		nameof(InstrumentKind), typeof(MidiInstrumentKind), typeof(MidiPlayer),
		new PropertyMetadata(MidiInstrumentKind.None));

	/// <summary>
	/// Which instrument format the loaded <see cref="Instrument"/> turned out to be (read-only), or
	/// <see cref="MidiInstrumentKind.None"/> while nothing is loaded. Valid from
	/// <see cref="MediaOpened"/>.
	/// </summary>
	public MidiInstrumentKind InstrumentKind
	{
		get => (MidiInstrumentKind)GetValue(InstrumentKindProperty);
		private set => SetValue(InstrumentKindProperty, value);
	}

	/// <summary>Identifies the <see cref="DropAuxiliaryOutputs"/> dependency property.</summary>
	public static readonly DependencyProperty DropAuxiliaryOutputsProperty = DependencyProperty.Register(
		nameof(DropAuxiliaryOutputs), typeof(bool), typeof(MidiPlayer),
		new PropertyMetadata(false, (o, e) => ((MidiPlayer)o)._player.DropAuxiliaryOutputs = (bool)e.NewValue));

	/// <summary>
	/// Whether an instrument's auxiliary stereo outputs are thrown away instead of being folded into
	/// the mix. False by default, so nothing an instrument makes goes unheard.
	/// </summary>
	/// <remarks>
	/// A Decent Sampler preset can route a group, a zone or a bus to one of sixteen auxiliary
	/// outputs. This player has one stereo pair, so it adds them into the mix; set this when a
	/// preset uses them for something a listener should not hear - a cue feed, or a layer meant for
	/// an external processor. Formats without auxiliary outputs are unaffected.
	/// </remarks>
	public bool DropAuxiliaryOutputs
	{
		get => (bool)GetValue(DropAuxiliaryOutputsProperty);
		set => SetValue(DropAuxiliaryOutputsProperty, value);
	}

	/// <summary>Identifies the <see cref="AutoPlay"/> dependency property.</summary>
	public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(
		nameof(AutoPlay), typeof(bool), typeof(MidiPlayer), new PropertyMetadata(false));

	/// <summary>When true, playback starts as soon as a load completes. Defaults to false.</summary>
	public bool AutoPlay
	{
		get => (bool)GetValue(AutoPlayProperty);
		set => SetValue(AutoPlayProperty, value);
	}

	/// <summary>Identifies the <see cref="IsLoading"/> dependency property.</summary>
	public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
		nameof(IsLoading), typeof(bool), typeof(MidiPlayer), new PropertyMetadata(false));

	/// <summary>
	/// True while an instrument and sequence are being loaded in the background (read-only). Bind a
	/// ProgressRing or a status line to it: a large sampled instrument takes seconds.
	/// </summary>
	// ANDROID PORT (3): UI.Core grants this assembly its internals, so its internal UIElement.IsLoading is visible here.
#pragma warning disable CS0108
	public bool IsLoading
#pragma warning restore CS0108
	{
		get => (bool)GetValue(IsLoadingProperty);
		private set => SetValue(IsLoadingProperty, value);
	}

	/// <summary>Identifies the <see cref="Position"/> dependency property.</summary>
	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
		nameof(Position), typeof(TimeSpan), typeof(MidiPlayer),
		new PropertyMetadata(TimeSpan.Zero, (o, e) => ((MidiPlayer)o).OnPositionChanged((TimeSpan)e.NewValue)));

	/// <summary>
	/// The current playback timecode. Updated on the UI thread while playing (bind an indicator
	/// one-way to follow playback); writing it seeks the sequence, debounced for seek-on-release
	/// scrubbing. <see cref="PositionSeconds"/> is the same value in seconds.
	/// </summary>
	public TimeSpan Position
	{
		get => (TimeSpan)GetValue(PositionProperty);
		set => SetValue(PositionProperty, value);
	}

	/// <summary>Identifies the <see cref="PositionSeconds"/> dependency property.</summary>
	public static readonly DependencyProperty PositionSecondsProperty = DependencyProperty.Register(
		nameof(PositionSeconds), typeof(double), typeof(MidiPlayer),
		new PropertyMetadata(0.0, (o, e) => ((MidiPlayer)o).OnPositionSecondsChanged((double)e.NewValue)));

	/// <summary>
	/// <see cref="Position"/> expressed in seconds, so a Slider's Value can two-way bind with no
	/// converter: the slider follows playback, and dragging it seeks (on release).
	/// </summary>
	public double PositionSeconds
	{
		get => (double)GetValue(PositionSecondsProperty);
		set => SetValue(PositionSecondsProperty, value);
	}

	/// <summary>Identifies the <see cref="Duration"/> dependency property.</summary>
	public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
		nameof(Duration), typeof(TimeSpan), typeof(MidiPlayer), new PropertyMetadata(TimeSpan.Zero));

	/// <summary>
	/// The total length of the loaded sequence (read-only; <see cref="TimeSpan.Zero"/> until
	/// <see cref="MediaOpened"/> has been raised).
	/// </summary>
	public TimeSpan Duration
	{
		get => (TimeSpan)GetValue(DurationProperty);
		private set => SetValue(DurationProperty, value);
	}

	/// <summary>Identifies the <see cref="DurationSeconds"/> dependency property.</summary>
	public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
		nameof(DurationSeconds), typeof(double), typeof(MidiPlayer), new PropertyMetadata(0.0));

	/// <summary>
	/// <see cref="Duration"/> expressed in seconds (read-only) - bind a Slider's Maximum to it with
	/// no converter.
	/// </summary>
	public double DurationSeconds
	{
		get => (double)GetValue(DurationSecondsProperty);
		private set => SetValue(DurationSecondsProperty, value);
	}

	/// <summary>Identifies the <see cref="IsPlaying"/> dependency property.</summary>
	public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
		nameof(IsPlaying), typeof(bool), typeof(MidiPlayer), new PropertyMetadata(false));

	/// <summary>True while the sequence is playing (read-only).</summary>
	public bool IsPlaying
	{
		get => (bool)GetValue(IsPlayingProperty);
		private set => SetValue(IsPlayingProperty, value);
	}

	/// <summary>Identifies the <see cref="Volume"/> dependency property.</summary>
	public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
		nameof(Volume), typeof(double), typeof(MidiPlayer),
		new PropertyMetadata(1.0, (o, e) => ((MidiPlayer)o)._player.Volume = (float)Math.Clamp((double)e.NewValue, 0.0, 1.0)));

	/// <summary>Playback volume from 0.0 (silent) to 1.0 (unity gain, the default).</summary>
	public double Volume
	{
		get => (double)GetValue(VolumeProperty);
		set => SetValue(VolumeProperty, value);
	}

	/// <summary>Identifies the <see cref="IsLooping"/> dependency property.</summary>
	public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
		nameof(IsLooping), typeof(bool), typeof(MidiPlayer),
		new PropertyMetadata(false, (o, e) => ((MidiPlayer)o)._player.IsLooping = (bool)e.NewValue));

	/// <summary>
	/// When true, the sequence repeats. It repeats from its own loop point when it carries one, and
	/// from the beginning when it does not.
	/// </summary>
	public bool IsLooping
	{
		get => (bool)GetValue(IsLoopingProperty);
		set => SetValue(IsLoopingProperty, value);
	}

	/// <summary>Identifies the <see cref="Speed"/> dependency property.</summary>
	public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
		nameof(Speed), typeof(double), typeof(MidiPlayer),
		new PropertyMetadata(1.0, (o, e) => ((MidiPlayer)o).OnSpeedChanged((double)e.NewValue)));

	/// <summary>
	/// The playback speed multiplier: 1.0 is the sequence's written tempo, 0.5 half speed, 2.0
	/// double speed. Negative values are clamped to 0, which freezes the transport while sounding
	/// notes ring out.
	/// </summary>
	/// <remarks>
	/// This is a property of a sequence rather than of a recording: the tempo changes and the pitch
	/// does not, because every note is still synthesized at its written frequency.
	/// </remarks>
	public double Speed
	{
		get => (double)GetValue(SpeedProperty);
		set => SetValue(SpeedProperty, value);
	}

	/// <summary>Identifies the <see cref="ActiveVoiceCount"/> dependency property.</summary>
	public static readonly DependencyProperty ActiveVoiceCountProperty = DependencyProperty.Register(
		nameof(ActiveVoiceCount), typeof(int), typeof(MidiPlayer), new PropertyMetadata(0));

	/// <summary>
	/// The number of synthesizer voices currently sounding (read-only), refreshed with
	/// <see cref="Position"/> while playing. Useful on screen for diagnostics, and for seeing what
	/// an arrangement actually costs.
	/// </summary>
	public int ActiveVoiceCount
	{
		get => (int)GetValue(ActiveVoiceCountProperty);
		private set => SetValue(ActiveVoiceCountProperty, value);
	}

	/// <summary>Identifies the <see cref="BeatsPerMinute"/> dependency property.</summary>
	public static readonly DependencyProperty BeatsPerMinuteProperty = DependencyProperty.Register(
		nameof(BeatsPerMinute), typeof(double), typeof(MidiPlayer),
		new PropertyMetadata(TempoSource.DefaultBeatsPerMinute));

	/// <summary>
	/// The tempo of the sequence at the current position, in beats per minute (read-only), refreshed
	/// with <see cref="Position"/> while playing. A sequence with tempo changes reports the tempo in
	/// force now rather than the one it started at; 120 while nothing is loaded.
	/// </summary>
	/// <remarks>
	/// This is the music's own tempo and takes no notice of <see cref="Speed"/>, which changes how
	/// fast the transport travels through it.
	/// </remarks>
	public double BeatsPerMinute
	{
		get => (double)GetValue(BeatsPerMinuteProperty);
		private set => SetValue(BeatsPerMinuteProperty, value);
	}

	/// <summary>Identifies the <see cref="BeatPosition"/> dependency property.</summary>
	public static readonly DependencyProperty BeatPositionProperty = DependencyProperty.Register(
		nameof(BeatPosition), typeof(double), typeof(MidiPlayer), new PropertyMetadata(0.0));

	/// <summary>
	/// How far into the sequence the transport has travelled, in beats (read-only), refreshed with
	/// <see cref="Position"/> while playing - the musical counterpart of
	/// <see cref="PositionSeconds"/>, for a beat indicator or for lining something up with the bar.
	/// </summary>
	public double BeatPosition
	{
		get => (double)GetValue(BeatPositionProperty);
		private set => SetValue(BeatPositionProperty, value);
	}

	/// <summary>Identifies the <see cref="PositionUpdateInterval"/> dependency property.</summary>
	public static readonly DependencyProperty PositionUpdateIntervalProperty = DependencyProperty.Register(
		nameof(PositionUpdateInterval), typeof(TimeSpan), typeof(MidiPlayer),
		new PropertyMetadata(TimeSpan.FromMilliseconds(150), (o, e) => ((MidiPlayer)o).OnPositionUpdateIntervalChanged((TimeSpan)e.NewValue)));

	/// <summary>
	/// How often <see cref="Position"/>, <see cref="PositionSeconds"/> and
	/// <see cref="ActiveVoiceCount"/> refresh while playing. Defaults to 150 milliseconds.
	/// </summary>
	public TimeSpan PositionUpdateInterval
	{
		get => (TimeSpan)GetValue(PositionUpdateIntervalProperty);
		set => SetValue(PositionUpdateIntervalProperty, value);
	}

	#endregion

	#region | MIDI Polyphonic Expression |

	/// <summary>Identifies the <see cref="MpeMode"/> dependency property.</summary>
	public static readonly DependencyProperty MpeModeProperty = DependencyProperty.Register(
		nameof(MpeMode), typeof(CodeBrix.Audio.Synth.Mpe.MpeMode), typeof(MidiPlayer),
		new PropertyMetadata(CodeBrix.Audio.Synth.Mpe.MpeMode.Off,
			(o, e) => ((MidiPlayer)o)._player.MpeMode = (CodeBrix.Audio.Synth.Mpe.MpeMode)e.NewValue));

	/// <summary>
	/// How the player reads the MIDI Polyphonic Expression zones of the music it plays. Off by
	/// default. Settable before a load and changeable while one plays; it survives a load.
	/// </summary>
	/// <remarks>
	/// A performance recorded from an expressive controller spreads each note onto its own MIDI
	/// channel so that it can bend, brighten and swell alone. Exporters usually leave out the
	/// message that says so, which is what <c>Auto</c> is for. All three instrument formats read
	/// such a performance the same way, so the same recording plays alike through any of them.
	/// </remarks>
	public CodeBrix.Audio.Synth.Mpe.MpeMode MpeMode
	{
		get => (CodeBrix.Audio.Synth.Mpe.MpeMode)GetValue(MpeModeProperty);
		set => SetValue(MpeModeProperty, value);
	}

	/// <summary>Identifies the <see cref="MpeMemberBendRange"/> dependency property.</summary>
	public static readonly DependencyProperty MpeMemberBendRangeProperty = DependencyProperty.Register(
		nameof(MpeMemberBendRange), typeof(double), typeof(MidiPlayer),
		new PropertyMetadata(48.0, (o, e) => ((MidiPlayer)o)._player.MpeMemberBendRange = (double)e.NewValue));

	/// <summary>
	/// How far a member channel's pitch bend reaches when the music never says, in semitones.
	/// Forty-eight by default, the value expressive controllers ship with.
	/// </summary>
	/// <remarks>A bend-range message in the music overrides this, per channel and per zone.</remarks>
	public double MpeMemberBendRange
	{
		get => (double)GetValue(MpeMemberBendRangeProperty);
		set => SetValue(MpeMemberBendRangeProperty, value);
	}

	/// <summary>Identifies the <see cref="MpeLowerZoneMemberCount"/> dependency property.</summary>
	public static readonly DependencyProperty MpeLowerZoneMemberCountProperty = DependencyProperty.Register(
		nameof(MpeLowerZoneMemberCount), typeof(int), typeof(MidiPlayer),
		new PropertyMetadata(0, (o, e) => ((MidiPlayer)o)._player.MpeLowerZoneMemberCount = (int)e.NewValue));

	/// <summary>
	/// How many member channels the lower zone holds when <see cref="MpeMode"/> names it. Zero, the
	/// default, means fifteen when only the lower zone is on and seven when both zones are.
	/// </summary>
	/// <remarks>Use this to pin a file the automatic detection reads differently from how it was played.</remarks>
	public int MpeLowerZoneMemberCount
	{
		get => (int)GetValue(MpeLowerZoneMemberCountProperty);
		set => SetValue(MpeLowerZoneMemberCountProperty, value);
	}

	/// <summary>Identifies the <see cref="MpeUpperZoneMemberCount"/> dependency property.</summary>
	public static readonly DependencyProperty MpeUpperZoneMemberCountProperty = DependencyProperty.Register(
		nameof(MpeUpperZoneMemberCount), typeof(int), typeof(MidiPlayer),
		new PropertyMetadata(0, (o, e) => ((MidiPlayer)o)._player.MpeUpperZoneMemberCount = (int)e.NewValue));

	/// <summary>The upper zone's equivalent of <see cref="MpeLowerZoneMemberCount"/>.</summary>
	public int MpeUpperZoneMemberCount
	{
		get => (int)GetValue(MpeUpperZoneMemberCountProperty);
		set => SetValue(MpeUpperZoneMemberCountProperty, value);
	}

	/// <summary>
	/// The lower MPE zone as the loaded instrument currently reads it (read-only) - master channel
	/// 1, its members, and the bend ranges in force. An inactive zone is reported while nothing is
	/// loaded.
	/// </summary>
	public MpeZoneInfo MpeLowerZone => _player.MpeLowerZone;

	/// <summary>
	/// The upper MPE zone as the loaded instrument currently reads it (read-only), with master
	/// channel 16.
	/// </summary>
	public MpeZoneInfo MpeUpperZone => _player.MpeUpperZone;

	/// <summary>
	/// The note-off ("lift") velocity of the last note-off for a key on a channel, 0 to 127, or -1
	/// when nothing is loaded or the loaded instrument format does not capture it.
	/// </summary>
	/// <param name="channel">The MIDI channel, 0-15.</param>
	/// <param name="key">The MIDI note number, 0-127.</param>
	/// <returns>The release velocity, 0 when that key has not been released, or -1 when unavailable.</returns>
	/// <remarks>
	/// An expressive controller sends how quickly a finger left the key, and a MIDI file records it.
	/// No instrument format here defines what it should DO, so it changes nothing about how a
	/// preset sounds; it is here so an application can react to it. To watch lifts as they happen
	/// rather than ask afterwards, use <see cref="MidiMessageProcessed"/>: a note-off carries the
	/// lift as its second data byte.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0-15.</exception>
	public int GetReleaseVelocity(int channel, int key) => _player.GetReleaseVelocity(channel, key);

	#endregion

	#region | How an instrument is loaded |

	/// <summary>
	/// How to load a Decent Sampler instrument - its memory budget, the size above which a sample is
	/// streamed from disk rather than decoded, whether samples are decoded at all, and where the
	/// decode cache lives. Null, the default, takes the engine's defaults.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Read at the next load, so set it before <see cref="Instrument"/> or change it and set the
	/// instrument again. This is a plain property rather than a bindable one: it is configuration
	/// for the load, not something a page binds to.
	/// </para>
	/// <para>
	/// A preset name inside it is overridden by <see cref="InstrumentPreset"/> whenever that is set.
	/// The element loads from a copy, so changing the object afterwards affects only later loads.
	/// Ignored by SoundFont and SFZ instruments.
	/// </para>
	/// </remarks>
	public DecentSamplerLoadOptions? InstrumentLoadOptions { get; set; }

	#endregion

	#region | What the loaded instrument reported |

	/// <summary>
	/// Anything the loaded instrument could not make sense of - a sample file it references that is
	/// missing, for example. Empty when the instrument loaded cleanly, which is the normal case.
	/// </summary>
	/// <remarks>
	/// An instrument loads even when this is non-empty: the regions it could not build are silent
	/// and the rest plays. Surface it when an instrument sounds wrong, rather than guessing.
	/// </remarks>
	public IReadOnlyList<string> InstrumentProblems { get; private set; } = Array.Empty<string>();

	/// <summary>
	/// Opcodes the loaded SFZ instrument uses that the synthesizer does not implement. Empty for a
	/// SoundFont, empty for a Decent Sampler instrument (which reports
	/// <see cref="UnsupportedInstrumentFeatures"/> instead), and empty for an SFZ instrument that is
	/// fully supported.
	/// </summary>
	public IReadOnlyCollection<string> UnsupportedInstrumentOpcodes { get; private set; } = Array.Empty<string>();

	/// <summary>
	/// Features the loaded Decent Sampler instrument uses that the synthesizer cannot honour. Empty
	/// for the other two formats, and empty for a preset that is fully supported.
	/// </summary>
	/// <remarks>
	/// The preset still loads and plays what it can. Some entries name the
	/// CodeBrix.Audio.ModestSynth add-on package: an application that references it and calls its
	/// <c>Register()</c> once at start-up, before loading, gets those sounds too. This package does
	/// not reference that add-on, exactly as it does not reference the Opus codec.
	/// </remarks>
	public IReadOnlyCollection<string> UnsupportedInstrumentFeatures { get; private set; } = Array.Empty<string>();

	/// <summary>
	/// How the loaded Decent Sampler instrument decided to hold its samples - what was decoded into
	/// memory, what is streamed from disk, and against which budget. Empty for the other two
	/// formats and until <see cref="MediaOpened"/> has been raised.
	/// </summary>
	/// <remarks>
	/// A line for a diagnostics panel or a log, in the engine's own words. What it says is steered
	/// by <see cref="InstrumentLoadOptions"/>.
	/// </remarks>
	public string InstrumentMemorySummary { get; private set; } = "";

	/// <summary>
	/// Anything the loaded MIDI file itself needed forgiving - a Standard MIDI File that breaks a
	/// rule is read leniently rather than refused, and says so here. Empty for a file that is
	/// exactly to specification, which is the normal case.
	/// </summary>
	/// <remarks>
	/// The sequence plays either way; this is where to look when a file from an unusual exporter
	/// sounds wrong. Valid from <see cref="MediaOpened"/>.
	/// </remarks>
	public IReadOnlyList<string> SourceProblems { get; private set; } = Array.Empty<string>();

	#endregion

	#region | The Decent Sampler control model |

	/// <summary>
	/// The knobs, sliders, menus and labels of the loaded Decent Sampler instrument as live
	/// parameters. Empty for the other two formats and until <see cref="MediaOpened"/> has been
	/// raised.
	/// </summary>
	/// <remarks>
	/// <para>
	/// These are the instrument's own control objects: setting one's value fires the bindings behind
	/// it immediately, exactly as turning that knob in a player would, and
	/// <see cref="InstrumentControlChanged"/> reports every movement - including the ones the
	/// preset's own modulators and MIDI bindings make.
	/// </para>
	/// <para>
	/// The values belong to the instrument rather than to this element, and instruments are shared:
	/// two players naming the same preset move one another's knobs.
	/// </para>
	/// </remarks>
	public IReadOnlyList<DecentSamplerControl> InstrumentControls { get; private set; } = Array.Empty<DecentSamplerControl>();

	/// <summary>
	/// The live state of every tag the loaded Decent Sampler preset names - whether it is enabled,
	/// and the volume, pan and polyphony that go with it. Empty for the other two formats.
	/// </summary>
	public IReadOnlyList<DecentSamplerTagState> InstrumentTagStates { get; private set; } = Array.Empty<DecentSamplerTagState>();

	/// <summary>
	/// Finds one of <see cref="InstrumentControls"/> by name, case-insensitively.
	/// </summary>
	/// <param name="name">The control's parameter name or label.</param>
	/// <returns>The first control of that name, or null when there is none (or nothing is loaded).</returns>
	public DecentSamplerControl? GetInstrumentControl(string name)
	{
		if (name is null)
		{
			return null;
		}

		foreach (var control in InstrumentControls)
		{
			if (string.Equals(control.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return control;
			}
		}

		return null;
	}

	#endregion

	#region | Transport |

	/// <summary>Starts or resumes playback. Does nothing until a load has completed.</summary>
	public void Play()
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		try
		{
			_player.Play();
		}
		catch (Exception e)
		{
			ReportFailure("Playback could not be started.", e);
			return;
		}
		IsPlaying = true;
		StartPositionTimer();
	}

	/// <summary>Pauses playback, keeping the current position.</summary>
	public void Pause()
	{
		_player.Pause();
		IsPlaying = false;
		StopPositionTimer();
		RefreshPositionFromPlayback();
	}

	/// <summary>
	/// Stops playback and rewinds to the beginning, silencing every sounding voice and clearing
	/// the controller state the sequence had set.
	/// </summary>
	public void Stop()
	{
		_player.Stop();
		IsPlaying = false;
		StopPositionTimer();
		RefreshPositionFromPlayback();
	}

	/// <summary>
	/// Jumps playback to <paramref name="position"/> immediately (no debounce).
	/// </summary>
	/// <remarks>
	/// Controller state up to that point is replayed so the instruments sound right, but notes that
	/// were already sounding there do not resume - a seek into the middle of a held chord starts
	/// from silence.
	/// </remarks>
	public void Seek(TimeSpan position)
	{
		if (!_isSourceLoaded)
		{
			return;
		}

		_seekDebounceTimer?.Stop();
		_player.Seek(ClampToDuration(position));
		RefreshPositionFromPlayback();
	}

	#endregion

	#region | Playing alongside the sequence |

	/// <summary>
	/// An observe-only callback raised after each MIDI message reaches the synthesizer - the hook
	/// for making something outside the audio react to the music.
	/// </summary>
	/// <remarks>
	/// It runs on the real-time AUDIO THREAD, so it must be fast and allocation-free, must not
	/// touch the UI directly, and must not call back into this player. Hand what it sees to the UI
	/// thread through the element's DispatcherQueue.
	/// </remarks>
	public MidiMessageObserver? MidiMessageProcessed
	{
		get => _player.MidiMessageProcessed;
		set => _player.MidiMessageProcessed = value;
	}

	/// <summary>
	/// Sends a MIDI message to the synthesizer alongside the sequence that is playing - the general
	/// form of the three calls below. Safe from any thread; does nothing until a load completes.
	/// </summary>
	/// <param name="channel">The channel to send to, 0-15.</param>
	/// <param name="command">The command nibble: 0x80 note-off, 0x90 note-on, 0xB0 control change, 0xC0 program change, 0xE0 pitch bend.</param>
	/// <param name="data1">The first data byte, 0-127.</param>
	/// <param name="data2">The second data byte, 0-127. Ignored by commands that take one byte.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0-15.</exception>
	public void SendMidiMessage(int channel, int command, int data1, int data2)
		=> _player.SendMidiMessage(channel, command, data1, data2);

	/// <summary>
	/// Sets one channel's volume, as MIDI control change 7 - how a layered arrangement is mixed
	/// live while the rest of the sequence plays on unchanged.
	/// </summary>
	/// <param name="channel">The channel to set, 0-15.</param>
	/// <param name="volume">The volume, 0.0 (silent) to 1.0 (full). Clamped.</param>
	/// <remarks>
	/// The sequence's own volume automation still applies: a track that writes control change 7
	/// will overwrite this the next time it does so.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0-15.</exception>
	public void SetChannelVolume(int channel, double volume)
		=> _player.SetChannelVolume(channel, (float)volume);

	/// <summary>Sets one channel's stereo position, as MIDI control change 10.</summary>
	/// <param name="channel">The channel to set, 0-15.</param>
	/// <param name="pan">The position, -1.0 (full left) through 0.0 (centre) to 1.0 (full right). Clamped.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0-15.</exception>
	public void SetChannelPan(int channel, double pan)
		=> _player.SetChannelPan(channel, (float)pan);

	/// <summary>Changes the instrument one channel plays, as a MIDI program change.</summary>
	/// <param name="channel">The channel to set, 0-15.</param>
	/// <param name="program">The program (patch) number, 0-127.</param>
	/// <remarks>
	/// Which sound a program number selects is the loaded instrument's business, not this player's.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0-15, or <paramref name="program"/> is outside 0-127.</exception>
	public void SetChannelProgram(int channel, int program)
		=> _player.SetChannelProgram(channel, program);

	#endregion

	#region | Source loading |

	// Source and Instrument are almost always set together (both from markup, or both in one
	// handler), so a change queues the load rather than starting it: by the time the queued call
	// runs, both values are in place and one load covers them.
	private void QueueLoad()
	{
		// Raise IsLoading here rather than in the queued call, so that a caller which sets both
		// properties and then looks at IsLoading sees the load it just started. Only once both are
		// set: a half-configured player has nothing to load yet, and would otherwise flicker.
		if (!string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Instrument))
		{
			IsLoading = true;
		}

		if (_loadQueued)
		{
			return;
		}

		_loadQueued = true;

		var queue = DispatcherQueue;
		if (queue is null || !queue.TryEnqueue(BeginLoad))
		{
			// Nothing to queue on (an element built outside the UI thread's dispatcher): load
			// directly, so a source set that way still plays.
			BeginLoad();
		}
	}

	private async void BeginLoad()
	{
		_loadQueued = false;

		var source = Source;
		var instrument = Instrument;

		// Read on the UI thread, where the dependency properties live, and carried into the
		// background load rather than read from it.
		var presetName = InstrumentPreset;
		var loadOptions = InstrumentLoadOptions;

		StopPositionTimer();
		IsPlaying = false;
		_isSourceLoaded = false;

		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(instrument))
		{
			// A MIDI player needs both halves; until it has them there is nothing to load.
			UnloadSource();
			return;
		}

		var generation = Interlocked.Increment(ref _loadGeneration);
		IsLoading = true;

		LoadResult result;
		await _loadGate.WaitAsync();
		try
		{
			if (generation != Volatile.Read(ref _loadGeneration))
			{
				// Superseded while waiting for an earlier load to finish; that newer load owns the
				// outcome, including clearing IsLoading.
				return;
			}

			result = await Task.Run(() => LoadOffThread(source, instrument, presetName, loadOptions));
		}
		finally
		{
			_loadGate.Release();
		}

		if (generation != Volatile.Read(ref _loadGeneration))
		{
			return;
		}

		IsLoading = false;

		if (result.Error is not null)
		{
			Duration = TimeSpan.Zero;
			DurationSeconds = 0.0;
			ClearInstrumentReporting();

			// An instrument format that must name a file on disk explains itself when it was handed
			// something else; that explanation is the useful half of the message, so it is carried
			// in the message rather than left in the exception for the reader to go and find.
			var message = $"The MIDI source '{source}' could not be loaded with the instrument '{instrument}'.";
			if (result.Error is NotSupportedException explained)
			{
				message = $"{message} {explained.Message}";
			}

			ReportFailure(message, result.Error);
			return;
		}

		_isSourceLoaded = true;
		_player.Volume = (float)Math.Clamp(Volume, 0.0, 1.0);
		_player.IsLooping = IsLooping;
		_player.PlaybackEnded -= OnPlayerPlaybackEnded;
		_player.PlaybackEnded += OnPlayerPlaybackEnded;

		// The engine keeps these across loads; re-applying them costs nothing and means a player
		// configured before it had anything loaded behaves the same as one configured after.
		ApplyMpeSettings();
		_player.DropAuxiliaryOutputs = DropAuxiliaryOutputs;

		Duration = result.Duration;
		DurationSeconds = result.Duration.TotalSeconds;
		InstrumentKind = result.Kind;
		InstrumentProblems = result.Problems;
		UnsupportedInstrumentOpcodes = result.UnsupportedOpcodes;
		UnsupportedInstrumentFeatures = result.UnsupportedFeatures;
		InstrumentMemorySummary = result.MemorySummary;
		SourceProblems = result.SourceProblems;
		SetInstrument(result.Instrument);
		RefreshPositionFromPlayback();

		WarnWhenAnAddOnIsMissing(instrument, result);

		MediaOpened?.Invoke(this, EventArgs.Empty);

		if (AutoPlay)
		{
			Play();
		}
	}

	// Runs on a thread-pool thread: reading an instrument is seconds of work and hundreds of
	// megabytes of decoding, which must never happen on the UI thread. Every call into an
	// instrument - including the shared Decent Sampler cache, whose Get is thread-safe - stays here.
	private LoadResult LoadOffThread(
		string source, string instrument, string presetName, DecentSamplerLoadOptions? loadOptions)
	{
		try
		{
			// ANDROID PORT (1): ms-appx:/// audio is an APK asset; copy it out before it is opened by path.
			PackagedAudioAssets.EnsureCopiedOutForSource(source);
			PackagedAudioAssets.EnsureCopiedOutForSource(instrument, instrument: true);

			var sequence = OpenSequence(source);
			var kind = ClassifyInstrument(instrument, out var instrumentPath);

			switch (kind)
			{
				case MidiInstrumentKind.Sfz:
				{
					var path = instrumentPath ?? throw new NotSupportedException(NeedsFileMessage(kind, instrument));
					var sfz = _sfzInstruments.Get(path);
					_player.Load(sfz, sequence);
					return new LoadResult(
						_player.Duration, kind, sfz.Problems, sfz.UnsupportedOpcodes,
						Array.Empty<string>(), "", sequence.Problems, null, null);
				}

				case MidiInstrumentKind.DecentSampler:
				{
					var path = instrumentPath ?? throw new NotSupportedException(NeedsFileMessage(kind, instrument));

					// The engine's own process-wide cache, rather than a third private one here: it
					// is what the audio package's path-based loading uses, so an application that
					// pre-loaded a library and a player naming the same path pay for it once, and
					// two presets of one library share that library's decoded samples.
					var decentSampler = MidiMusicPlayer.SharedDecentSamplerCache.Get(
						path, BuildLoadOptions(loadOptions, presetName));

					_player.Load(decentSampler, sequence);
					return new LoadResult(
						_player.Duration, kind, decentSampler.Problems, Array.Empty<string>(),
						decentSampler.UnsupportedFeatures, decentSampler.MemoryPolicySummary,
						sequence.Problems, decentSampler, null);
				}

				default:
				{
					var soundFont = OpenSoundFont(instrument);
					_player.Load(soundFont, sequence);
					return new LoadResult(
						_player.Duration, MidiInstrumentKind.SoundFont, Array.Empty<string>(),
						Array.Empty<string>(), Array.Empty<string>(), "", sequence.Problems, null, null);
				}
			}
		}
		catch (Exception e)
		{
			return LoadResult.Failed(e);
		}
	}

	private static MidiSequence OpenSequence(string source)
	{
		var (filePath, stream) = AudioSourceResolver.Resolve(source);
		if (filePath is not null)
		{
			return new MidiSequence(filePath);
		}

		// The sequence is parsed in full by the constructor, so the stream is finished with here.
		using (stream)
		{
			return new MidiSequence(stream!);
		}
	}

	private static SoundFont OpenSoundFont(string instrument)
	{
		var (filePath, stream) = AudioSourceResolver.Resolve(instrument);
		if (filePath is not null)
		{
			// Cached by path: a SoundFont shared by two players is read once.
			return _soundFonts.Get(filePath);
		}

		using (stream)
		{
			return new SoundFont(stream!);
		}
	}

	// What the instrument names decides the synthesizer, matching CodeBrix.Audio's own rule: the
	// extension first, then a folder that holds a preset. The extension is read from the source as
	// written, so an instrument that only exists as a stream is still recognised as the format it
	// names - which is what lets the failure below say which format needs a file, and why.
	private static MidiInstrumentKind ClassifyInstrument(string instrument, out string? resolvedPath)
	{
		// Null for the forms that name no file - an embedded resource. Resolved without opening
		// anything, because a format that must be a file on disk has already failed by then.
		resolvedPath = AudioSourceResolver.ResolveLocalPathOrNull(instrument);

		var extension = Path.GetExtension(instrument);

		if (extension.Equals(".sfz", StringComparison.OrdinalIgnoreCase))
		{
			return MidiInstrumentKind.Sfz;
		}

		if (extension.Equals(".dspreset", StringComparison.OrdinalIgnoreCase)
			|| extension.Equals(".dslibrary", StringComparison.OrdinalIgnoreCase)
			|| extension.Equals(".dsbundle", StringComparison.OrdinalIgnoreCase))
		{
			return MidiInstrumentKind.DecentSampler;
		}

		// A folder is a Decent Sampler instrument when it holds a preset; that is the shape a
		// .dsbundle has on macOS, and the shape an unpacked library has everywhere.
		if (resolvedPath is not null && HoldsDecentSamplerPreset(resolvedPath))
		{
			return MidiInstrumentKind.DecentSampler;
		}

		return MidiInstrumentKind.SoundFont;
	}

	private static bool HoldsDecentSamplerPreset(string path)
	{
		if (!Directory.Exists(path))
		{
			return false;
		}

		using var presets = Directory
			.EnumerateFiles(path, "*.dspreset", SearchOption.AllDirectories)
			.GetEnumerator();

		return presets.MoveNext();
	}

	// Says which format was asked for and why it cannot come from anything but a file on disk.
	private static string NeedsFileMessage(MidiInstrumentKind kind, string instrument)
	{
		var extension = Path.GetExtension(instrument);
		var named = extension.Length == 0 ? "" : $" ({extension})";

		var reason = kind == MidiInstrumentKind.Sfz
			? "an .sfz file references its sample files as separate files beside it"
			: "a .dspreset references its samples as separate files beside it, and a .dslibrary or " +
			  ".dsbundle, though it is one file, is read in place from the archive on disk";

		var format = kind == MidiInstrumentKind.Sfz ? "An SFZ" : "A Decent Sampler";

		return $"{format} instrument{named} must be given as a file path or an ms-appx:/// URI, " +
			$"because {reason} - so it cannot be loaded from an embedded resource or a stream. " +
			"A .sf2 SoundFont can.";
	}

	// The element's own copy of the load options, so that a caller holding the object cannot change
	// a load already under way, and so that InstrumentPreset can name the preset without editing
	// what the caller handed over. Null means the engine's defaults.
	private static DecentSamplerLoadOptions? BuildLoadOptions(DecentSamplerLoadOptions? options, string presetName)
	{
		if (options is null && string.IsNullOrEmpty(presetName))
		{
			return null;
		}

		var forThisLoad = options?.Clone() ?? new DecentSamplerLoadOptions();
		if (!string.IsNullOrEmpty(presetName))
		{
			forThisLoad.PresetName = presetName;
		}

		return forThisLoad;
	}

	private void UnloadSource()
	{
		StopPositionTimer();
		IsPlaying = false;
		IsLoading = false;
		_isSourceLoaded = false;
		_player.Stop();
		Duration = TimeSpan.Zero;
		DurationSeconds = 0.0;
		ActiveVoiceCount = 0;
		ClearInstrumentReporting();
		RefreshPositionFromPlayback();
	}

	// Everything an earlier load reported, back to the nothing-is-loaded state.
	private void ClearInstrumentReporting()
	{
		InstrumentKind = MidiInstrumentKind.None;
		InstrumentProblems = Array.Empty<string>();
		UnsupportedInstrumentOpcodes = Array.Empty<string>();
		UnsupportedInstrumentFeatures = Array.Empty<string>();
		InstrumentMemorySummary = "";
		SourceProblems = Array.Empty<string>();
		SetInstrument(null);
	}

	private void ApplyMpeSettings()
	{
		_player.MpeMode = MpeMode;
		_player.MpeMemberBendRange = MpeMemberBendRange;
		_player.MpeLowerZoneMemberCount = MpeLowerZoneMemberCount;
		_player.MpeUpperZoneMemberCount = MpeUpperZoneMemberCount;
	}

	// One warning, carrying the engine's own advice, when the preset that just loaded asks for a
	// sound only an application-side add-on package can make. The load succeeded, because the
	// engine plays everything else in the preset.
	private void WarnWhenAnAddOnIsMissing(string instrument, LoadResult result)
	{
		var advice = FirstLineNamingTheAddOn(result.Problems) ?? FirstLineNamingTheAddOn(result.UnsupportedFeatures);
		if (advice is null || !this.Log().IsEnabled(LogLevel.Warning))
		{
			return;
		}

		this.Log().Warn(
			$"The instrument '{instrument}' loaded, but part of it cannot sound as written: {advice}");
	}

	private static string? FirstLineNamingTheAddOn(IEnumerable<string> lines)
	{
		foreach (var line in lines)
		{
			if (line is not null && line.Contains(AddOnMarker, StringComparison.Ordinal))
			{
				return line;
			}
		}

		return null;
	}

	private void OnPlayerPlaybackEnded(object? sender, EventArgs e)
	{
		// Raised off the UI thread (the load that captured the context ran on the thread pool), so
		// everything here has to be marshalled.
		DispatcherQueue.TryEnqueue(() =>
		{
			IsPlaying = false;
			StopPositionTimer();
			RefreshPositionFromPlayback();
			PlaybackEnded?.Invoke(this, EventArgs.Empty);
		});
	}

	private void ReportFailure(string message, Exception error)
	{
		if (this.Log().IsEnabled(LogLevel.Error))
		{
			this.Log().Error(message, error);
		}
		MediaFailed?.Invoke(this, new AudioPlayerFailedEventArgs(message, error));
	}

	private readonly record struct LoadResult(
		TimeSpan Duration,
		MidiInstrumentKind Kind,
		IReadOnlyList<string> Problems,
		IReadOnlyCollection<string> UnsupportedOpcodes,
		IReadOnlyCollection<string> UnsupportedFeatures,
		string MemorySummary,
		IReadOnlyList<string> SourceProblems,
		DecentSamplerInstrument? Instrument,
		Exception? Error)
	{
		public static LoadResult Failed(Exception error) => new(
			TimeSpan.Zero, MidiInstrumentKind.None, Array.Empty<string>(), Array.Empty<string>(),
			Array.Empty<string>(), "", Array.Empty<string>(), null, error);
	}

	#endregion

	#region | Watching the loaded instrument's controls |

	// The instrument itself is never disposed here: it belongs to the engine's process-wide cache
	// and may be sounding in another player. All this element owns is its subscriptions.
	private void SetInstrument(DecentSamplerInstrument? instrument)
	{
		DetachInstrumentControls();

		_decentSamplerInstrument = instrument;
		InstrumentControls = instrument?.Controls ?? Array.Empty<DecentSamplerControl>();
		InstrumentTagStates = instrument?.TagStates ?? Array.Empty<DecentSamplerTagState>();

		AttachInstrumentControls();
	}

	private void AttachInstrumentControls()
	{
		if (_controlsSubscribed || _decentSamplerInstrument is null)
		{
			return;
		}

		// Captured here, on the UI thread, because the audio thread cannot go looking for it.
		_uiQueue = DispatcherQueue;

		foreach (var control in InstrumentControls)
		{
			control.Changed += OnInstrumentControlChanged;
		}

		_controlsSubscribed = true;
	}

	private void DetachInstrumentControls()
	{
		if (!_controlsSubscribed)
		{
			return;
		}

		foreach (var control in InstrumentControls)
		{
			control.Changed -= OnInstrumentControlChanged;
		}

		_controlsSubscribed = false;

		lock (_controlChangeGate)
		{
			_pendingControlChanges.Clear();
		}
	}

	// Arrives on whichever thread moved the control - the audio thread for a modulator or a MIDI
	// binding, the caller's thread for a SetValue.
	private void OnInstrumentControlChanged(object? sender, DecentSamplerControlChangedEventArgs e)
	{
		bool flushNeeded;
		lock (_controlChangeGate)
		{
			// One entry per control, carrying the last change seen for it: a modulator can move the
			// same knob on every rendered block, and the UI only ever needs where it ended up.
			_pendingControlChanges[e.Control] = e;
			flushNeeded = !_controlFlushQueued;
			_controlFlushQueued = true;
		}

		if (!flushNeeded)
		{
			return;
		}

		var queue = _uiQueue;
		if (queue is null || !queue.TryEnqueue(FlushControlChanges))
		{
			// Nothing to marshal onto (an element built outside the UI thread's dispatcher): raise
			// it here rather than losing it, the same choice the load path makes.
			FlushControlChanges();
		}
	}

	private void FlushControlChanges()
	{
		DecentSamplerControlChangedEventArgs[] changes;
		lock (_controlChangeGate)
		{
			_controlFlushQueued = false;
			if (_pendingControlChanges.Count == 0)
			{
				return;
			}

			changes = new DecentSamplerControlChangedEventArgs[_pendingControlChanges.Count];
			_pendingControlChanges.Values.CopyTo(changes, 0);
			_pendingControlChanges.Clear();
		}

		var handler = InstrumentControlChanged;
		if (handler is null)
		{
			return;
		}

		foreach (var change in changes)
		{
			handler(this, change);
		}
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
					_player.Seek(ClampToDuration(_pendingSeek));
				}
			};
		}

		// Restarting on every write coalesces a whole slider drag into one seek on release.
		_seekDebounceTimer.Stop();
		_seekDebounceTimer.Start();
	}

	private void OnSpeedChanged(double newSpeed)
		=> _player.Speed = (float)Math.Max(0.0, newSpeed);

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
		Position = _isSourceLoaded ? _player.Position : TimeSpan.Zero;
		_updatingFromPlayback = false;
		ActiveVoiceCount = _isSourceLoaded ? _player.ActiveVoiceCount : 0;

		// The musical clock costs two lock-free reads, so it follows the same timer as the timecode.
		var tempo = _player.TempoSource;
		BeatsPerMinute = _isSourceLoaded ? tempo.BeatsPerMinute : TempoSource.DefaultBeatsPerMinute;
		BeatPosition = _isSourceLoaded ? tempo.BeatPosition : 0.0;
	}

	private TimeSpan ClampToDuration(TimeSpan position)
	{
		var duration = _player.Duration;
		if (position < TimeSpan.Zero)
		{
			return TimeSpan.Zero;
		}
		return duration > TimeSpan.Zero && position > duration ? duration : position;
	}

	#endregion
}

/// <summary>
/// Which instrument format a <see cref="MidiPlayer"/> is rendering its sequence through.
/// </summary>
public enum MidiInstrumentKind
{
	/// <summary>Nothing is loaded.</summary>
	None,

	/// <summary>A SoundFont (<c>.sf2</c>).</summary>
	SoundFont,

	/// <summary>An SFZ instrument (<c>.sfz</c>).</summary>
	Sfz,

	/// <summary>
	/// A Decent Sampler instrument: a <c>.dspreset</c>, a <c>.dslibrary</c>, a <c>.dsbundle</c>, or a
	/// folder holding a preset.
	/// </summary>
	DecentSampler,
}
