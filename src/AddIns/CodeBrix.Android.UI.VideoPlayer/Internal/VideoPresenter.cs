// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoPresenter.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// One change, marked ANDROID PORT (1): the composed-frame capture reads back as RGBA (OpenGL ES has no guaranteed BGRA read).

#nullable enable

// Ported from CodeBrix.VideoPlayback.Skia (commit a3f3051, MIT, same author) on 2026-08-30;
// compiled against the Platform family's SkiaSharp.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using Microsoft.Extensions.Logging;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.VideoPlayback;
using CodeBrix.VideoPlayback.Color;
using CodeBrix.VideoPlayback.Color.Luts;
using CodeBrix.VideoPlayback.Effects;
using CodeBrix.VideoPlayback.Frames;
using CodeBrix.VideoPlayback.Presentation;
using CodeBrix.VideoPlayback.Rendering;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal; //was previously: CodeBrix.VideoPlayback.Skia;

/// <summary>
/// Draws decoded video with SkiaSharp: it takes the newest frame, composes it on an off-screen
/// surface - on the graphics device or on the processor - lets the application draw over it, and
/// hands the result to whoever is presenting it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is called this.</b> Upstream this class is <c>SkiaVideoPresenter</c>, because the
/// playback engine has more than one presenter and that one is the SkiaSharp one. Inside the
/// CodeBrix.Platform family Skia is not a variant: it is the only way anything is drawn, so a
/// "Skia" in the name would mark a distinction that does not exist here. It is
/// <see cref="VideoPresenter"/>, and it is INTERNAL - an application uses the
/// <see cref="VideoPlayer"/> element, never a presenter. (Not to be confused with the engine's
/// <see cref="VideoFramePresenter"/>, which is the frame mailbox this class reads.)
/// </para>
/// <para>
/// <b>Why the add-in owns a copy at all.</b> The engine ships its own SkiaSharp presenter in
/// CodeBrix.VideoPlayback.Skia, for hosts outside this family. That package pins its own SkiaSharp
/// while this family pins one SkiaSharp for everything it ships, and an assembly compiled against
/// one SkiaSharp and run against another fails the moment SkiaSharp changes a signature it uses. So
/// the Skia-bound part of the engine lives here instead, compiled against the family's pin, and the
/// add-in depends on the drawing-free engine only. Everything that does NOT need a canvas - the
/// render paths, the letterbox arithmetic, the composed effect chain, the colour shader source, the
/// composition context - is taken from the engine and never re-declared.
/// </para>
/// <para>
/// <b>The shape of it.</b> The presenter never draws to a window. It owns an off-screen
/// <see cref="SKSurface"/>, draws the video into it as a base layer, runs <see cref="Layers"/> and
/// the <see cref="Composing"/> event over the top, and then either <see cref="Draw"/> blits that
/// surface into a canvas or <see cref="CurrentImage"/> hands it over for the frame driver to read
/// back. That indirection is the whole point: an off-screen surface is a canvas anybody can draw
/// on, which is what makes subtitles, heads-up overlays, annotation and picture-over-picture
/// possible without this class knowing about any of them.
/// </para>
/// <para>
/// <b>Two render paths, both first class.</b> On the graphics path the three planes are uploaded as
/// single-channel textures and ONE shader does the colour conversion and the whole effect chain in
/// a single pass, at full sample precision. On the processor path the engine's vector converter
/// turns the frame into BGRA pixels straight into the composition surface's own memory, with no
/// copy at all. Neither is a degraded version of the other. <see cref="RenderPath"/> says which one
/// is wanted and what to do if it cannot be had; <see cref="ActiveRenderPath"/> says which one is
/// running.
/// </para>
/// <para>
/// <b>Threading.</b> <see cref="Present"/> and <see cref="Attach"/> may be called from any thread.
/// Everything that touches the surface - <see cref="Update"/>, <see cref="Recompose"/>,
/// <see cref="Draw"/>, <see cref="CurrentImage"/>, <see cref="CaptureComposedFrame"/> - must be
/// called from ONE thread, the thread that owns the graphics context, which in this add-in is
/// always the user-interface thread. <see cref="Invalidated"/> is raised on whatever thread posted
/// the frame, which is the decode thread.
/// </para>
/// </remarks>
internal sealed class VideoPresenter : IDisposable
{
	/// <summary>The default number of nodes along each axis of the composed effect lookup table.</summary>
	/// <remarks>
	/// Thirty-three is the size the ".cube" convention settled on, and is enough for any smooth
	/// colour change: 35,937 nodes, a 1089-by-33 atlas, 143 kilobytes of texture. The number itself
	/// lives on <see cref="EffectComposer.DefaultSize"/> in the playback engine, so a chain composed
	/// with no presenter anywhere near it lands on the same grid this one uses.
	/// </remarks>
	internal const int DefaultEffectLutSize = EffectComposer.DefaultSize;

	private static readonly SKSamplingOptions BlitSampling =
		new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

	private static int _cpuEffectWarningIssued;

	private readonly VideoFramePresenter _ownMailbox = new VideoFramePresenter();
	private readonly ObservableCollection<IVideoFrameEffect> _effects = new ObservableCollection<IVideoFrameEffect>();
	private readonly ObservableCollection<IVideoLayer> _layers = new ObservableCollection<IVideoLayer>();
	private readonly BgraFrameBufferPool _bgraPool = new BgraFrameBufferPool();
	private readonly YuvSurfaceRenderer _renderer = new YuvSurfaceRenderer();

	private VideoFramePresenter _source;
	private GRContext? _grContext;
	private VideoRenderPath _renderPath = VideoRenderPath.GpuAuto;
	private VideoRenderBackend _activeBackend = VideoRenderBackend.Cpu;
	private bool _backendResolved;
	private bool _fallbackReported;
	private bool _allowEffectsOnCpu;
	private bool _effectsChainDirty = true;
	private bool _effectsActive;
	private int _effectLutSize = DefaultEffectLutSize;

	private LutInterpolation _effectInterpolation = LutInterpolation.Tetrahedral;

	private EffectComposer? _composer;
	private Lut3D? _resultantLut;
	private SKBitmap? _lookupAtlas;
	private SKImage? _lookupAtlasImage;

	private SKSurface? _surface;
	private BgraFrameBuffer? _cpuSurfaceBuffer;
	private VideoRenderBackend _surfaceBackend;
	private int _surfaceWidth;
	private int _surfaceHeight;
	private SKImage? _cachedImage;

	private VideoFrame? _composedFrame;
	private bool _hasComposition;
	private int _displayWidth;
	private int _displayHeight;
	private TimeSpan _lastTimestamp;
	private long _lastFrameNumber = -1;

	private long _framesComposed;
	private long _framesDrawn;
	private long _surfaceAllocations;
	private long _effectCompositions;

	private bool _disposed;

	/// <summary>Creates a presenter with no graphics context, which starts on the processor path.</summary>
	/// <remarks>
	/// Hand it a context later with <see cref="UseGpu"/> and it moves to the graphics path at the
	/// next frame.
	/// </remarks>
	internal VideoPresenter()
	{
		_source = _ownMailbox;
		_source.Invalidated += OnSourceInvalidated;
		_effects.CollectionChanged += OnEffectsChanged;
	}

	/// <summary>Creates a presenter that draws on a graphics context somebody else owns.</summary>
	/// <param name="graphicsContext">
	/// The context to render on. The presenter does NOT take ownership of it and never disposes it;
	/// it must outlive the presenter and must be current on the thread that draws.
	/// </param>
	internal VideoPresenter(GRContext graphicsContext)
		: this()
	{
		_grContext = graphicsContext;
	}

	/// <summary>Raised when a new frame has arrived and the view should repaint.</summary>
	/// <remarks>
	/// It is raised on the thread that posted the frame - the decode thread - so a handler must do
	/// the least possible: mark the view dirty, ask for a repaint, and return.
	/// </remarks>
	internal event EventHandler? Invalidated;

	/// <summary>Raised when the presenter settles on a render path, and again whenever it changes.</summary>
	internal event EventHandler<VideoRenderPathChangedEventArgs>? RenderPathChanged;

	/// <summary>
	/// Raised after the video and every registered layer have been drawn on the composition surface,
	/// and before it is presented - the ad-hoc alternative to writing an <see cref="IVideoLayer"/>.
	/// </summary>
	/// <remarks>
	/// Raised on the drawing thread, inside the composition. The canvas is valid only for the
	/// duration of the call.
	/// </remarks>
	internal event EventHandler<VideoComposingEventArgs>? Composing;

	/// <summary>
	/// The mailbox this presenter takes frames from - either one it was
	/// <see cref="Attach">attached</see> to or its own.
	/// </summary>
	internal VideoFramePresenter Source => _source;

	/// <summary>True when the presenter is reading a mailbox somebody else owns.</summary>
	internal bool IsAttached => !ReferenceEquals(_source, _ownMailbox);

	/// <summary>The graphics context the presenter renders on, or null when it has none.</summary>
	internal GRContext? GraphicsContext => _grContext;

	/// <summary>Which render path to use, and what to do when the graphics one is unavailable.</summary>
	/// <remarks>
	/// Changing this takes effect at the next composition. The change is announced through
	/// <see cref="RenderPathChanged"/> once it has actually happened.
	/// </remarks>
	internal VideoRenderPath RenderPath
	{
		get => _renderPath;
		set
		{
			if (_renderPath == value)
			{
				return;
			}

			_renderPath = value;
			_fallbackReported = false;
			_effectsChainDirty = true;
		}
	}

	/// <summary>Which render path is actually running.</summary>
	/// <remarks>
	/// Until the first composition this reports what the presenter WOULD choose, without committing
	/// to it and without raising <see cref="RenderPathChanged"/>.
	/// </remarks>
	internal VideoRenderBackend ActiveRenderPath => _backendResolved ? _activeBackend : PeekBackend();

	/// <summary>True when the configured <see cref="Effects"/> are actually being applied.</summary>
	/// <remarks>
	/// False with a non-empty chain means the presenter is on the processor path and
	/// <see cref="AllowEffectsOnCpu"/> is not set, so the effects are being silently ignored - which
	/// is the documented behaviour, not a fault.
	/// </remarks>
	internal bool EffectsActive =>
		!_disposed && _effects.Count > 0 && (ActiveRenderPath == VideoRenderBackend.Gpu || _allowEffectsOnCpu);

	/// <summary>
	/// Whether the effect chain should be applied on the processor when the graphics path is
	/// unavailable. False by default.
	/// </summary>
	/// <remarks>
	/// A per-pixel lookup on every frame is roughly as expensive as the colour conversion itself, so
	/// turning this on can halve the frame rate the processor path sustains. It is off by default so
	/// that a graphics fallback degrades in speed by nothing at all; turn it on when the effect
	/// chain is the point of the picture rather than an enhancement of it. The first frame that
	/// takes this road is logged once per process as a warning.
	/// </remarks>
	internal bool AllowEffectsOnCpu
	{
		get => _allowEffectsOnCpu;
		set
		{
			if (_allowEffectsOnCpu == value)
			{
				return;
			}

			_allowEffectsOnCpu = value;
			_effectsChainDirty = true;
		}
	}

	/// <summary>
	/// The colour effect chain, applied in list order and composed into ONE resultant lookup table.
	/// </summary>
	/// <remarks>
	/// Editing the list marks the chain for recomposition, which happens at the next composition and
	/// not per frame. Order matters: effect two sees the colours effect one produced. To make an
	/// edit reach a PAUSED screen, call <see cref="Recompose"/> when the edit is finished.
	/// </remarks>
	internal ObservableCollection<IVideoFrameEffect> Effects => _effects;

	/// <summary>
	/// The overlay layers, drawn in list order on top of the video and beneath the
	/// <see cref="Composing"/> event.
	/// </summary>
	internal ObservableCollection<IVideoLayer> Layers => _layers;

	/// <summary>The number of nodes along each axis of the composed effect lookup table.</summary>
	/// <remarks>
	/// Larger grids follow a chain containing a hard step more faithfully and cost more texture
	/// memory - the atlas is <c>size * size</c> by <c>size</c> pixels. Changing this recomposes the
	/// chain.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The value is below <see cref="Lut3D.MinimumSize"/> or above <see cref="Lut3D.MaximumSize"/>.
	/// </exception>
	internal int EffectLutSize
	{
		get => _effectLutSize;
		set
		{
			if (value < Lut3D.MinimumSize || value > Lut3D.MaximumSize)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					$"The effect lookup grid has between {Lut3D.MinimumSize} and {Lut3D.MaximumSize} nodes a side.");
			}

			if (_effectLutSize == value)
			{
				return;
			}

			_effectLutSize = value;
			_effectsChainDirty = true;
		}
	}

	/// <summary>
	/// How a colour that falls BETWEEN the nodes of a lookup table is worked out, everywhere in this
	/// presenter's effect chain.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It governs three things at once, on purpose, so that one setting means one thing: how each
	/// effect's own table is sampled while the chain is folded, how the shader reads the resultant
	/// table on the graphics path, and how <see cref="AllowEffectsOnCpu"/> reads it on the processor
	/// path. Both render paths therefore always agree with each other.
	/// </para>
	/// <para>
	/// <see cref="LutInterpolation.Tetrahedral"/> is the default. It is what colour-grading tools do,
	/// so a grade shown here and the same grade baked to a ".cube" file for an encoding pipeline
	/// agree to about one level in 255. It holds the neutral axis exactly - a grey the table leaves
	/// grey stays grey - and costs four texture fetches a pixel.
	/// </para>
	/// <para>
	/// <see cref="LutInterpolation.Trilinear"/> is what a graphics card's own texture filter does and
	/// costs two fetches a pixel instead of four. On a smooth grade the two are within a level of
	/// each other; choose it when the per-pixel cost matters more than agreeing with a grading tool.
	/// </para>
	/// <para>Changing this recomposes the chain and repaints.</para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not one of the two.</exception>
	internal LutInterpolation EffectInterpolation
	{
		get => _effectInterpolation;
		set
		{
			if (value != LutInterpolation.Tetrahedral && value != LutInterpolation.Trilinear)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					"A lookup table is read tetrahedrally or trilinearly.");
			}

			if (_effectInterpolation == value)
			{
				return;
			}

			_effectInterpolation = value;
			_effectsChainDirty = true;
		}
	}

	/// <summary>True once a frame has been composed and there is something to draw.</summary>
	internal bool HasComposedFrame => _hasComposition;

	/// <summary>The coded width of the frame most recently composed, or zero before the first one.</summary>
	internal int ComposedWidth => _surfaceWidth;

	/// <summary>The coded height of the frame most recently composed, or zero before the first one.</summary>
	internal int ComposedHeight => _surfaceHeight;

	/// <summary>The width the composed frame should be SHOWN at, once its pixel aspect ratio is applied.</summary>
	internal int DisplayWidth => _displayWidth;

	/// <summary>The height the composed frame should be SHOWN at, once its pixel aspect ratio is applied.</summary>
	internal int DisplayHeight => _displayHeight;

	/// <summary>The timestamp of the frame most recently composed.</summary>
	internal TimeSpan CurrentTimestamp => _lastTimestamp;

	/// <summary>The number of the frame most recently composed, or -1 before the first one.</summary>
	internal long CurrentFrameNumber => _lastFrameNumber;

	/// <summary>
	/// The composed picture as an image, for a host that would rather composite it itself than let
	/// <see cref="Draw"/> blit it. This is what the frame driver reads back.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The presenter OWNS the returned image and replaces it at the next composition, so use it and
	/// let it go - never hold it across a frame and never dispose it. An image you may keep comes
	/// from <see cref="CaptureComposedFrame"/>.
	/// </para>
	/// <para>Null before the first frame has been composed.</para>
	/// </remarks>
	internal SKImage? CurrentImage
	{
		get
		{
			if (_disposed || !_hasComposition || _surface is null)
			{
				return null;
			}

			return _cachedImage ??= _surface.Snapshot();
		}
	}

	/// <summary>Works out where a picture goes inside a rectangle.</summary>
	/// <param name="destination">The rectangle to fit the picture into.</param>
	/// <param name="contentWidth">The picture's display width.</param>
	/// <param name="contentHeight">The picture's display height.</param>
	/// <param name="stretch">How to fit it.</param>
	/// <returns>
	/// The rectangle the picture should be drawn into. For
	/// <see cref="VideoStretch.UniformToFill"/> and <see cref="VideoStretch.None"/> it can be larger
	/// than <paramref name="destination"/>, and the caller is expected to clip.
	/// </returns>
	/// <remarks>
	/// A pure function. The arithmetic itself is
	/// <see cref="VideoStretchMath.ComputeDestination"/> in the playback engine, where every
	/// presenter in the family reads it; this is the SkiaSharp spelling of the same answer.
	/// </remarks>
	internal static SKRect ComputeDestinationRect(
		SKRect destination,
		int contentWidth,
		int contentHeight,
		VideoStretch stretch) =>
		VideoStretchMath
			.ComputeDestination(
				VideoRectangles.FromSKRect(destination),
				contentWidth,
				contentHeight,
				stretch)
			.ToSKRect();

	/// <summary>Reads frames from somebody else's mailbox - a playback session's, normally.</summary>
	/// <param name="presenter">
	/// The mailbox to read. The presenter does NOT take ownership of it; the session that made it
	/// disposes it.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="presenter"/> is null.</exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	internal void Attach(VideoFramePresenter presenter)
	{
		if (presenter is null)
		{
			throw new ArgumentNullException(nameof(presenter));
		}

		ThrowIfDisposed();

		if (ReferenceEquals(presenter, _source))
		{
			return;
		}

		_source.Invalidated -= OnSourceInvalidated;
		if (ReferenceEquals(_source, _ownMailbox))
		{
			_ownMailbox.Clear();
		}

		_source = presenter;
		_source.Invalidated += OnSourceInvalidated;
	}

	/// <summary>Stops reading somebody else's mailbox and goes back to this presenter's own.</summary>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	internal void Detach()
	{
		ThrowIfDisposed();

		if (!IsAttached)
		{
			return;
		}

		_source.Invalidated -= OnSourceInvalidated;
		_source = _ownMailbox;
		_source.Invalidated += OnSourceInvalidated;
	}

	/// <summary>Hands a frame straight to the presenter, without a playback session in between.</summary>
	/// <param name="frame">
	/// The frame to show. The presenter takes its own reference; the caller keeps and disposes its
	/// own.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// The frame goes into whichever mailbox the presenter is reading, so it replaces anything
	/// waiting there - the newest frame always wins, exactly as it does during playback.
	/// </remarks>
	internal void Present(VideoFrame frame)
	{
		if (frame is null)
		{
			throw new ArgumentNullException(nameof(frame));
		}

		ThrowIfDisposed();
		_source.Post(frame);
	}

	/// <summary>Moves the presenter onto a graphics context.</summary>
	/// <param name="graphicsContext">
	/// The context to render on, or null to go back to the processor path. The presenter does NOT
	/// take ownership: it never disposes the context, which must outlive it and must be current on
	/// the drawing thread.
	/// </param>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// The change takes effect at the next composition, which allocates a new composition surface on
	/// the new backend.
	/// </remarks>
	internal void UseGpu(GRContext? graphicsContext)
	{
		ThrowIfDisposed();

		if (ReferenceEquals(_grContext, graphicsContext))
		{
			return;
		}

		_grContext = graphicsContext;
		_fallbackReported = false;
		_effectsChainDirty = true;
	}

	/// <summary>
	/// Settles which render path will be used, raising <see cref="RenderPathChanged"/> if that is
	/// news.
	/// </summary>
	/// <returns>The path that will run.</returns>
	/// <exception cref="VideoPlaybackException">
	/// <see cref="RenderPath"/> is <see cref="VideoRenderPath.GpuNoFallback"/> and there is no
	/// usable graphics context.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// <see cref="Update"/> and <see cref="Draw"/> call this for you.
	/// </remarks>
	internal VideoRenderBackend ResolveRenderPath()
	{
		ThrowIfDisposed();

		VideoRenderBackend wanted;
		string reason;

		if (_renderPath == VideoRenderPath.Cpu)
		{
			wanted = VideoRenderBackend.Cpu;
			reason = "the render path is set to Cpu.";
		}
		else if (HasUsableContext())
		{
			wanted = VideoRenderBackend.Gpu;
			reason = "a graphics context is available.";
		}
		else if (_renderPath == VideoRenderPath.GpuNoFallback)
		{
			throw new VideoPlaybackException(
				_grContext is null
					? "This player's RenderPath is GpuNoFallback, but it has no graphics context: the running " +
					  "head could not supply one. Set RenderPath to GpuAuto to let it fall back to the " +
					  "processor path."
					: "This player's RenderPath is GpuNoFallback, and the graphics context it was given has " +
					  "been abandoned. Set RenderPath to GpuAuto to let it fall back to the processor path.");
		}
		else
		{
			wanted = VideoRenderBackend.Cpu;
			reason = _grContext is null
				? "no graphics context was supplied, so the processor path is running instead."
				: "the graphics context has been abandoned, so the processor path is running instead.";

			if (!_fallbackReported)
			{
				_fallbackReported = true;

				if (this.Log().IsEnabled(LogLevel.Debug))
				{
					this.Log().Debug(
						$"Video rendering fell back to the processor path: {reason} Set RenderPath to " +
						"GpuNoFallback if that should be an error instead. Configured effects are " +
						(_allowEffectsOnCpu ? "being applied on the processor." : "not being applied."));
				}
			}
		}

		if (_backendResolved && wanted == _activeBackend)
		{
			return _activeBackend;
		}

		_activeBackend = wanted;
		_backendResolved = true;
		_effectsChainDirty = true;
		RenderPathChanged?.Invoke(this, new VideoRenderPathChangedEventArgs(wanted, reason));
		return _activeBackend;
	}

	/// <summary>Takes the newest frame, if there is one, and composes it onto the off-screen surface.</summary>
	/// <returns>True when a new frame was composed; false when the mailbox was empty.</returns>
	/// <exception cref="VideoPlaybackException">
	/// The graphics path was demanded and cannot be had, or a graphics resource could not be created.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	internal bool Update()
	{
		ThrowIfDisposed();
		var backend = ResolveRenderPath();

		if (!_source.TryTakeLatest(out var frame))
		{
			return false;
		}

		try
		{
			Compose(frame, backend);
		}
		finally
		{
			frame.Dispose();
		}

		return true;
	}

	/// <summary>
	/// Composes the frame that is already on screen again, through whatever the effect chain, the
	/// layers and the render path now say.
	/// </summary>
	/// <exception cref="VideoPlaybackException">
	/// The graphics path was demanded and cannot be had, or a graphics resource could not be created.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// <para>
	/// <b>What it is for.</b> Editing <see cref="Effects"/> while playback is PAUSED changes nothing
	/// visible, because the picture is only ever built when a frame arrives and none is coming. This
	/// builds it again from the frame the presenter is still holding, so a grade the user just
	/// dialled in reaches the screen at once. <see cref="CurrentImage"/> and
	/// <see cref="CaptureComposedFrame"/> then hand back the new picture, and
	/// <see cref="Invalidated"/> is raised so the host repaints.
	/// </para>
	/// <para>
	/// It is a full composition and it counts as one: <see cref="GetStatistics"/> sees
	/// <c>FramesComposed</c> rise, and <c>EffectCompositions</c> too whenever the chain actually
	/// needed folding again.
	/// </para>
	/// <para>
	/// <b>Nothing calls it automatically.</b> An edit to <see cref="Effects"/> marks the chain dirty
	/// and stops there, deliberately: the collection is edited on whatever thread the application
	/// likes, while a composition must happen on the thread that owns the graphics context, and a
	/// chain built up in several steps would otherwise recompose once per step. So edit the chain,
	/// then call this - on the drawing thread, like <see cref="Update"/>.
	/// </para>
	/// <para>
	/// It does NOT collect a newer frame; that is <see cref="Update"/>'s job. Before the first frame
	/// has been composed it does nothing at all.
	/// </para>
	/// </remarks>
	internal void Recompose()
	{
		ThrowIfDisposed();

		var frame = _composedFrame;
		if (frame is null)
		{
			return;
		}

		var backend = ResolveRenderPath();
		Compose(frame, backend);
		Invalidated?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Draws the composed video into a canvas.</summary>
	/// <param name="canvas">The canvas to draw into.</param>
	/// <param name="destination">The rectangle in that canvas the video should occupy.</param>
	/// <param name="stretch">How to fit the picture into the rectangle. Letterboxed by default.</param>
	/// <exception cref="ArgumentNullException"><paramref name="canvas"/> is null.</exception>
	/// <exception cref="VideoPlaybackException">
	/// The graphics path was demanded and cannot be had, or a graphics resource could not be created.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// <para>
	/// This collects the newest frame first. The canvas is clipped to
	/// <paramref name="destination"/> and its state is restored before the call returns.
	/// </para>
	/// <para>
	/// The <see cref="VideoPlayer"/> element does NOT use this: the scene's paint callback is not on
	/// the user-interface thread on every head, and this surface belongs to one thread, so the
	/// element reads each frame back on the user-interface thread and blits the readback instead. It
	/// is kept because it is the presenter's natural entry point for any host that paints on the
	/// thread that composes.
	/// </para>
	/// </remarks>
	internal void Draw(SKCanvas canvas, SKRect destination, VideoStretch stretch = VideoStretch.Uniform)
	{
		if (canvas is null)
		{
			throw new ArgumentNullException(nameof(canvas));
		}

		ThrowIfDisposed();

		Update();

		if (!_hasComposition || _surface is null || _surfaceWidth <= 0 || _surfaceHeight <= 0)
		{
			return;
		}

		var target = ComputeDestinationRect(destination, _displayWidth, _displayHeight, stretch);

		canvas.Save();
		canvas.ClipRect(destination, SKClipOperation.Intersect, false);
		canvas.Translate(target.Left, target.Top);
		canvas.Scale(target.Width / _surfaceWidth, target.Height / _surfaceHeight);
		_surface.Draw(canvas, 0f, 0f, BlitSampling, null);
		canvas.Restore();

		_framesDrawn++;
	}

	/// <summary>Takes a copy of the composed picture that the caller owns.</summary>
	/// <returns>
	/// A readable image of the composition surface, which the CALLER must dispose, or null when
	/// nothing has been composed yet.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// The image is always readable on the processor, whichever path composed it, so its pixels can
	/// be encoded, hashed or written to a file. It is NOT a cheap read - it snapshots, reads back
	/// and copies - so the per-frame path uses <see cref="CurrentImage"/> instead.
	/// </remarks>
	internal SKImage? CaptureComposedFrame()
	{
		ThrowIfDisposed();

		if (!_hasComposition || _surface is null)
		{
			return null;
		}

		// ANDROID PORT (1): OpenGL ES guarantees an RGBA read-back only (a GPU-composed surface cannot be read as BGRA there).
		var info = new SKImageInfo(_surfaceWidth, _surfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

		using var snapshot = _surface.Snapshot();
		using var scratch = new SKBitmap(info);

		if (!snapshot.ReadPixels(info, scratch.GetPixels(), info.RowBytes, 0, 0))
		{
			return null;
		}

		return SKImage.FromPixelCopy(info, scratch.GetPixels(), info.RowBytes);
	}

	/// <summary>Composes the effect chain, if it needs it, and hands back the resultant table.</summary>
	/// <returns>
	/// The single lookup table the whole chain reduces to, or null when the chain is empty. The
	/// table is a copy; changing it changes nothing.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The presenter has been disposed.</exception>
	/// <remarks>
	/// Useful for showing a user what a chain does, for saving a chain as one file, and for testing.
	/// It counts as a composition in <see cref="GetStatistics"/> when it actually recomposes.
	/// </remarks>
	internal Lut3D? GetResultantLut()
	{
		ThrowIfDisposed();

		if (_effects.Count == 0)
		{
			return null;
		}

		if (_effectsChainDirty || _composer is null || _composer.Size != _effectLutSize)
		{
			ComposeEffectChain();
		}

		return _resultantLut;
	}

	/// <summary>Takes a snapshot of the presenter's counters.</summary>
	/// <returns>The counters as they stood at the moment of the call.</returns>
	internal VideoCompositionStatistics GetStatistics() =>
		new VideoCompositionStatistics(_framesComposed, _framesDrawn, _surfaceAllocations, _effectCompositions);

	/// <summary>Sets every counter back to zero. Nothing else changes.</summary>
	internal void ResetStatistics()
	{
		_framesComposed = 0;
		_framesDrawn = 0;
		_surfaceAllocations = 0;
		_effectCompositions = 0;
	}

	/// <summary>
	/// Releases the composition surface, the pooled pixels, the compiled shaders and the presenter's
	/// own mailbox.
	/// </summary>
	/// <remarks>
	/// The graphics context and any attached mailbox belong to whoever made them and are left alone.
	/// This must be called while the graphics context is still current - the frame driver disposes
	/// the presenter inside a frame scope for exactly that reason.
	/// </remarks>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_source.Invalidated -= OnSourceInvalidated;
		_effects.CollectionChanged -= OnEffectsChanged;

		ReleaseSurface();

		_cachedImage?.Dispose();
		_cachedImage = null;

		_lookupAtlasImage?.Dispose();
		_lookupAtlasImage = null;
		_lookupAtlas?.Dispose();
		_lookupAtlas = null;

		_composedFrame?.Dispose();
		_composedFrame = null;

		_renderer.Dispose();
		_bgraPool.Dispose();
		_ownMailbox.Dispose();

		Invalidated = null;
		RenderPathChanged = null;
		Composing = null;
	}

	private void OnSourceInvalidated(object? sender, EventArgs args) => Invalidated?.Invoke(this, EventArgs.Empty);

	private void OnEffectsChanged(object? sender, NotifyCollectionChangedEventArgs args) => _effectsChainDirty = true;

	private bool HasUsableContext() => _grContext is not null && !_grContext.IsAbandoned;

	private VideoRenderBackend PeekBackend() =>
		_renderPath != VideoRenderPath.Cpu && HasUsableContext() ? VideoRenderBackend.Gpu : VideoRenderBackend.Cpu;

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(VideoPresenter));
		}
	}

	private void Compose(VideoFrame frame, VideoRenderBackend backend)
	{
		_effectsActive = _effects.Count > 0 && (backend == VideoRenderBackend.Gpu || _allowEffectsOnCpu);
		if (_effectsActive && (_effectsChainDirty || _composer is null || _composer.Size != _effectLutSize))
		{
			ComposeEffectChain();
		}

		EnsureSurface(frame, backend);

		_cachedImage?.Dispose();
		_cachedImage = null;

		if (backend == VideoRenderBackend.Gpu)
		{
			ComposeOnGpu(frame);
		}
		else
		{
			ComposeOnCpu(frame);
		}

		// Recompose() needs the frame that is on screen, so the presenter keeps a reference of its
		// own - taken AFTER the composition, so a frame that threw on the way in is never
		// recomposed. Retaining before releasing the previous one is deliberate: recomposing hands
		// this same frame back in, and releasing first could drop the last reference to the very
		// frame being composed.
		if (!ReferenceEquals(_composedFrame, frame))
		{
			var previous = _composedFrame;
			_composedFrame = frame.Retain();
			previous?.Dispose();
		}

		_displayWidth = frame.DisplayWidth > 0 ? frame.DisplayWidth : frame.Width;
		_displayHeight = frame.DisplayHeight > 0 ? frame.DisplayHeight : frame.Height;
		_lastTimestamp = frame.Timestamp;
		_lastFrameNumber = frame.FrameNumber;
		_hasComposition = true;
		_framesComposed++;

		RunOverlays(backend);
	}

	private void RunOverlays(VideoRenderBackend backend)
	{
		var composing = Composing;
		if ((_layers.Count == 0 && composing is null) || _surface is null)
		{
			return;
		}

		var context = new VideoCompositionContext(
			VideoRectangle.Create(0f, 0f, _surfaceWidth, _surfaceHeight),
			_surfaceWidth,
			_surfaceHeight,
			_displayWidth,
			_displayHeight,
			_lastTimestamp,
			_lastFrameNumber,
			backend,
			_effectsActive);

		var canvas = _surface.Canvas;

		foreach (var layer in _layers)
		{
			if (layer is null)
			{
				continue;
			}

			canvas.Save();
			try
			{
				layer.Draw(canvas, context);
			}
			finally
			{
				canvas.Restore();
			}
		}

		if (composing is not null)
		{
			canvas.Save();
			try
			{
				composing(this, new VideoComposingEventArgs(canvas, context));
			}
			finally
			{
				canvas.Restore();
			}
		}
	}

	private void ComposeOnCpu(VideoFrame frame)
	{
		// The converter writes into the surface's own memory rather than through its canvas, which
		// is what makes this path copy-free - and which means Skia never learns the pixels changed.
		// It caches the image a snapshot handed back and reuses it until something draws, so without
		// this the SECOND and every later frame would be captured as the FIRST one. Discard says
		// exactly what is about to happen: every pixel is replaced, nothing needs preserving.
		_surface!.Canvas.Discard();

		VideoFrameConverter.ToBgra32(frame, _cpuSurfaceBuffer!.AsSpan(), _cpuSurfaceBuffer.Stride);

		if (!_effectsActive || _resultantLut is null)
		{
			return;
		}

		WarnOnceAboutEffectsOnCpu();
		CpuLutApplier.Apply(_resultantLut, _cpuSurfaceBuffer, _effectInterpolation);
	}

	private void ComposeOnGpu(VideoFrame frame)
	{
		// The pool must not recycle this frame's memory until the upload has actually been handed to
		// the driver, so a fence goes in the buffer's tag before the upload and is signalled after
		// the submit.
		var fence = new GpuUploadFence();
		frame.Buffer.Tag = fence;

		try
		{
			var useLookup = _effectsActive && _lookupAtlasImage is not null;
			_renderer.Render(
				frame,
				_surface!,
				_grContext,
				useLookup ? _lookupAtlasImage : null,
				useLookup ? _composer!.Size : 0,
				_effectInterpolation);
		}
		finally
		{
			fence.Signal();
		}
	}

	private unsafe void ComposeEffectChain()
	{
		if (_composer is null || _composer.Size != _effectLutSize)
		{
			_composer = new EffectComposer(_effectLutSize);
		}
		else
		{
			_composer.Reset();
		}

		_composer.Interpolation = _effectInterpolation;

		foreach (var effect in _effects)
		{
			effect?.Compose(_composer);
		}

		_resultantLut = _composer.ToLut3D();

		var width = LutAtlas.GetWidth(_effectLutSize);
		var height = LutAtlas.GetHeight(_effectLutSize);

		if (_lookupAtlas is null || _lookupAtlas.Width != width || _lookupAtlas.Height != height)
		{
			_lookupAtlas?.Dispose();
			_lookupAtlas = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
		}

		LutAtlas.Write(
			_composer,
			new Span<byte>((void*)_lookupAtlas.GetPixels(), _lookupAtlas.ByteCount),
			_lookupAtlas.RowBytes);

		_lookupAtlas.NotifyPixelsChanged();

		_lookupAtlasImage?.Dispose();
		_lookupAtlasImage = SKImage.FromBitmap(_lookupAtlas);

		_effectsChainDirty = false;
		_effectCompositions++;
	}

	private void EnsureSurface(VideoFrame frame, VideoRenderBackend backend)
	{
		var width = frame.Width;
		var height = frame.Height;

		if (_surface is not null && _surfaceWidth == width && _surfaceHeight == height && _surfaceBackend == backend)
		{
			return;
		}

		ReleaseSurface();

		var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

		if (backend == VideoRenderBackend.Cpu)
		{
			_cpuSurfaceBuffer = _bgraPool.Rent(width, height);
			_surface = SKSurface.Create(info, _cpuSurfaceBuffer.Data, _cpuSurfaceBuffer.Stride);

			if (_surface is null)
			{
				_bgraPool.Return(_cpuSurfaceBuffer);
				_cpuSurfaceBuffer = null;
				throw new VideoPlaybackException(
					$"SkiaSharp would not make a {width}x{height} BGRA raster surface over the pooled pixel " +
					"buffer, so there is nothing to compose the video on.");
			}
		}
		else
		{
			_surface = SKSurface.Create(_grContext, true, info);

			if (_surface is null)
			{
				throw new VideoPlaybackException(
					$"The graphics context would not make a {width}x{height} BGRA render target, so there is " +
					"nothing to compose the video on. Set RenderPath to GpuAuto to fall back to the processor " +
					"path, or Cpu to use it outright.");
			}
		}

		_surfaceWidth = width;
		_surfaceHeight = height;
		_surfaceBackend = backend;
		_surfaceAllocations++;
		_hasComposition = false;
		_bgraPool.Trim(width, height);
	}

	private void ReleaseSurface()
	{
		_surface?.Dispose();
		_surface = null;

		if (_cpuSurfaceBuffer is not null)
		{
			_bgraPool.Return(_cpuSurfaceBuffer);
			_cpuSurfaceBuffer = null;
		}

		_cachedImage?.Dispose();
		_cachedImage = null;

		_surfaceWidth = 0;
		_surfaceHeight = 0;
		_hasComposition = false;
	}

	private void WarnOnceAboutEffectsOnCpu()
	{
		if (Interlocked.Exchange(ref _cpuEffectWarningIssued, 1) != 0)
		{
			return;
		}

		if (this.Log().IsEnabled(LogLevel.Warning))
		{
			this.Log().Warn(
				"AllowEffectsOnCpu is set, so the composed colour lookup table is being applied to every " +
				"pixel of every frame on the processor. That costs roughly as much again as the colour " +
				"conversion itself and will lower the frame rate this machine can sustain. A head that can " +
				"supply a graphics context moves the effect chain onto the graphics device. This warning is " +
				"issued once per process.");
		}
	}
}
