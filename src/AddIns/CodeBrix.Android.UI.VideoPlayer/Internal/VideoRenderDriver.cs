// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoRenderDriver.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Its graphics path is the Android Graphics3DGL add-in's SkiaGpuContext (EGL / OpenGL ES), the same public type. One change,
// marked ANDROID PORT (1): the frame is read back as RGBA (OpenGL ES has no guaranteed BGRA read).

#nullable enable

using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using CodeBrix.VideoPlayback.Rendering;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;

/// <summary>
/// Drives one composed video frame at a time onto the <see cref="VideoSurfaceElement"/>, on the
/// user-interface thread, on either render path.
/// </summary>
/// <remarks>
/// <para>
/// The playback presenter is the off-screen surface that owns the composition; this class is the
/// ADAPTER that decides when a frame is composed, gets the graphics context the composition needs,
/// reads the result back to processor pixels in one copy, and hands the picture to the surface
/// element to blit. It is the video analogue of the CodeBrix.Platform.GameEngine host's GPU
/// render-surface adapter, and every lifetime rule here is that adapter's, for the same reasons:
/// </para>
/// <list type="number">
/// <item>The graphics context is created LAZILY, on the first frame after the element is loaded and
/// has a live XamlRoot - and the attempt is not latched before then, so the next frame retries.
/// One informational log line names the backend that was chosen; one warning (with the Windows
/// compatibility-pack hint) explains a failure, exactly once.</item>
/// <item>Teardown happens when the element leaves the visual tree, NOT at disposal. On WGL the
/// off-screen context is built on the window's own device context, so once the window is destroyed
/// every attempt to make it current fails and the graphics resources can never be released; and
/// continued dispatcher posting after the last window closed can keep a head's message loop from
/// draining. A volatile detached flag turns an already-queued tick into a no-op, re-entering the
/// tree re-arms driving, and the context is rebuilt lazily on the next frame - never in Loaded.</item>
/// <item>All graphics work happens on the user-interface thread inside ONE frame scope per frame.
/// The composition surface is released inside a frame scope BEFORE the context is disposed (both
/// scopes are no-ops on Metal).</item>
/// <item>Frame notifications arrive on the decode thread and are coalesced to a single queued tick,
/// latest wins, so a slow user-interface thread never accumulates a backlog.</item>
/// <item>One readback per frame into two alternating BGRA bitmaps, wrapped zero-copy and presented;
/// the previous wrapper is disposed by the surface element under its own gate.</item>
/// <item>A paused present is available so a seek reaches the screen with playback stopped - and,
/// when there is nothing to decode, it rebuilds the picture from the frame already on screen, so a
/// change to the effect chain or the layers reaches a paused screen too.</item>
/// <item>Whether the graphics path came up is what the element reports as its active render
/// path, and the presented picture is the screenshot hook.</item>
/// </list>
/// <para>
/// The presenter is recreated after a detach rather than kept: releasing its composition surface
/// inside the dying context's frame scope is what rule 3 asks for, and disposal is the one way its
/// public surface offers to do that. Nothing is lost - the mailbox it reads belongs to the playback
/// session, so the picture simply resumes.
/// </para>
/// </remarks>
internal sealed class VideoRenderDriver : IDisposable
{
	private readonly FrameworkElement _owner;
	private readonly VideoSurfaceElement _surface;
	private readonly Action<VideoPresenter> _configurePresenter;
	private readonly Action _renderPathSettled;
	private readonly Action<string, Exception?> _reportFailure;

	private VideoPresenter? _presenter;
	private SkiaGpuContext? _context;
	private bool _gpuInitAttempted;
	private bool _gpuAvailable;

	// Double-buffered processor readback targets: one frame reads back into one bitmap while the
	// surface element may still be blitting the wrapper over the other. Written on the UI thread.
	private SKBitmap? _readbackA;
	private SKBitmap? _readbackB;
	private bool _writeToA = true;

	private int _tickScheduled;

	// Set by PresentPausedFrame, cleared by the frame that honours it: a request to rebuild the
	// picture from the frame already on screen, for a change (an effect, a layer, whether effects
	// are allowed on the processor) that has to reach a PAUSED screen with nothing decoding.
	private int _recomposeRequested;

	private bool _disposed;

	// True while the owning element is off the visual tree (window closing or page navigated away).
	// Set on the UI thread; read from the UI thread and from the decode thread, so volatile.
	private volatile bool _detached;

	/// <summary>Creates the driver for one <see cref="VideoPlayer"/> element.</summary>
	/// <param name="owner">The element whose dispatcher, XamlRoot and loaded state drive the frames.</param>
	/// <param name="surface">The element the composed picture is presented to.</param>
	/// <param name="configurePresenter">
	/// Applies the owning element's settings (render path, effects, layers, event hooks, the
	/// session mailbox) to a freshly created presenter.
	/// </param>
	/// <param name="renderPathSettled">Called after every resolve so the element can publish what is running.</param>
	/// <param name="reportFailure">Called with a message a person can act on when a frame cannot be produced.</param>
	public VideoRenderDriver(
		FrameworkElement owner,
		VideoSurfaceElement surface,
		Action<VideoPresenter> configurePresenter,
		Action renderPathSettled,
		Action<string, Exception?> reportFailure)
	{
		_owner = owner;
		_surface = surface;
		_configurePresenter = configurePresenter;
		_renderPathSettled = renderPathSettled;
		_reportFailure = reportFailure;

		_owner.Loaded += OnOwnerLoaded;
		_owner.Unloaded += OnOwnerUnloaded;
	}

	/// <summary>
	/// The presenter currently composing frames, creating one if there is none. Null only after
	/// disposal.
	/// </summary>
	public VideoPresenter? Presenter
	{
		get
		{
			if (_disposed)
			{
				return null;
			}

			if (_presenter is null)
			{
				var presenter = new VideoPresenter();
				presenter.Invalidated += OnPresenterInvalidated;
				_presenter = presenter;
				_configurePresenter(presenter);

				// A presenter that was handed a context before this one died must be given the
				// live one; a presenter created before the context exists gets it in EnsureGpu.
				if (_gpuAvailable && _context is not null)
				{
					presenter.UseGpu(_context.GrContext);
				}
			}

			return _presenter;
		}
	}

	/// <summary>
	/// Whether the off-screen graphics context and its GRContext were created successfully:
	/// null until the first frame attempts it, false when the processor path is what is running.
	/// </summary>
	public bool? IsGpuInitialized => _gpuInitAttempted ? _gpuAvailable : null;

	/// <summary>
	/// Asks for one frame to be produced and presented, whether or not playback is running -
	/// the paused present, for a seek or a settings change that has to reach the screen.
	/// </summary>
	/// <remarks>
	/// A seek produces a new frame, which the next frame composes as usual. A change to the effect
	/// chain, the layers or whether effects are allowed on the processor produces NOTHING to decode,
	/// so this also asks the presenter to build the picture again from the frame it is still holding
	/// (<see cref="VideoPresenter.Recompose"/>) - on the user-interface thread, inside the frame
	/// scope on the graphics path, exactly like an ordinary composition. Without that a grade dialled
	/// in while paused would not reach the screen until the next seek or Play.
	/// </remarks>
	public void PresentPausedFrame()
	{
		Interlocked.Exchange(ref _recomposeRequested, 1);
		RequestTick();
	}

	/// <summary>
	/// Settles the render path now and publishes it, rather than waiting for the next frame.
	/// </summary>
	public void ResolveRenderPath()
	{
		if (_disposed || Presenter is not { } presenter)
		{
			return;
		}

		if (presenter.RenderPath != VideoRenderPath.Cpu)
		{
			EnsureGpu();

			if (!_gpuInitAttempted)
			{
				// The element is not loaded yet, so the graphics context has not even been tried.
				// Settling now would report a fallback - or, under GpuNoFallback, refuse outright -
				// for a context that is still to come. The first frame settles it instead.
				return;
			}
		}

		try
		{
			presenter.ResolveRenderPath();
		}
		catch (Exception e)
		{
			// GpuNoFallback with no usable graphics device. That setting exists to say so rather
			// than degrade quietly, so the message goes to the application.
			_reportFailure(e.Message, e);
		}

		_renderPathSettled();
	}

	/// <summary>Drops the picture on screen and the readback buffers behind it.</summary>
	public void ClearPresentedFrame()
	{
		_surface.ClearFrame();
	}

	// Decode thread, once per decoded frame. Coalesce to a single queued tick so a slow UI thread
	// never accumulates a backlog of frames.
	private void OnPresenterInvalidated(object? sender, EventArgs e) => RequestTick();

	private void RequestTick()
	{
		if (_disposed || _detached || Interlocked.CompareExchange(ref _tickScheduled, 1, 0) != 0)
		{
			return;
		}

		var dispatcherQueue = _owner.DispatcherQueue;
		if (dispatcherQueue is null || !dispatcherQueue.TryEnqueue(Tick))
		{
			Interlocked.Exchange(ref _tickScheduled, 0);
		}
	}

	private void Tick()
	{
		Interlocked.Exchange(ref _tickScheduled, 0);
		RunFrame();
	}

	// UI thread. Composes one frame and presents the readback. The detached guard covers every
	// frame driver - a tick that was already queued when the element unloaded included - so no
	// graphics work (and no context re-creation) can happen against a window that is going away.
	private void RunFrame()
	{
		if (_disposed || _detached || Presenter is not { } presenter)
		{
			return;
		}

		try
		{
			// A player asked for the processor path never builds a graphics context at all: that is
			// what Cpu means, and creating one anyway would cost a driver context and log a backend
			// line for something nothing uses.
			if (presenter.RenderPath != VideoRenderPath.Cpu)
			{
				if (EnsureGpu())
				{
					using (_context!.BeginFrame())
					{
						ComposeAndPresent(presenter);
					}
					return;
				}

				if (!_gpuInitAttempted)
				{
					// The element is not loaded yet; try again on the next frame notification.
					return;
				}
			}

			ComposeAndPresent(presenter);
		}
		catch (Exception e)
		{
			_reportFailure(e.Message, e);
		}
	}

	// UI thread, context current on the graphics path. One composition, one readback, one present.
	private void ComposeAndPresent(VideoPresenter presenter)
	{
		var before = presenter.ActiveRenderPath;
		var effectsBefore = presenter.EffectsActive;

		// Consumed whether or not it is needed: a request that arrived while a new frame was already
		// waiting is satisfied by composing that frame, which uses the new settings anyway.
		var recomposeRequested = Interlocked.Exchange(ref _recomposeRequested, 0) != 0;

		if (!presenter.Update() && recomposeRequested && presenter.HasComposedFrame)
		{
			// Nothing was decoded, so the picture on screen is the only one there is: build it again
			// through the settings as they now stand. Recompose raises Invalidated, which costs one
			// extra tick that finds an empty mailbox and re-presents the same picture - a frame's
			// work, once, in exchange for keeping the coalescing rule simple and thread-safe.
			presenter.Recompose();
		}

		if (presenter.ActiveRenderPath != before || presenter.EffectsActive != effectsBefore)
		{
			_renderPathSettled();
		}

		if (!presenter.HasComposedFrame)
		{
			return;
		}

		var image = presenter.CurrentImage;
		if (image is not null)
		{
			// The presenter OWNS this image and replaces it at the next composition, so it is read
			// and let go, never held and never disposed here.
			ReadbackAndPresent(image);
		}
	}

	// UI thread (context current when the image is graphics-backed). Reads the frame back into the
	// write-side bitmap, wraps it with no copy, and presents it.
	private void ReadbackAndPresent(SKImage image)
	{
		var width = image.Width;
		var height = image.Height;
		if (width <= 0 || height <= 0)
		{
			return;
		}

		// ANDROID PORT (1): OpenGL ES guarantees an RGBA read-back only; a BGRA read of a GPU-composed frame fails there
		// (GL_INVALID_ENUM from glReadPixels, nothing presented). RGBA reads back on both paths; the surface draws either.
		var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
		var target = _writeToA ? _readbackA : _readbackB;

		if (target is null || target.Width != width || target.Height != height)
		{
			target?.Dispose();
			target = new SKBitmap(info);
			if (_writeToA)
			{
				_readbackA = target;
			}
			else
			{
				_readbackB = target;
			}
		}

		if (!image.ReadPixels(info, target.GetPixels(), target.RowBytes, 0, 0))
		{
			if (this.Log().IsEnabled(LogLevel.Warning))
			{
				this.Log().Warn("The composed video frame could not be read back for presentation.");
			}
			return;
		}

		_writeToA = !_writeToA;

		_surface.PresentFrame(SKImage.FromPixels(info, target.GetPixels(), target.RowBytes));
	}

	// UI thread. Creates the backend-neutral off-screen graphics context once the element is loaded
	// (OpenGL/GLES on the Windows and Linux heads, Metal on macOS).
	private bool EnsureGpu()
	{
		if (_gpuInitAttempted)
		{
			return _gpuAvailable;
		}

		// TryCreate needs a live XamlRoot; before the element is loaded, skip WITHOUT latching the
		// attempt, so the next frame retries.
		if (!_owner.IsLoaded || _owner.XamlRoot is null)
		{
			return false;
		}

		_gpuInitAttempted = true;

		try
		{
			if (!SkiaGpuContext.TryCreate(_owner.XamlRoot, out _context))
			{
				var warning =
					"Video rendering on the graphics device is unavailable on this head (no off-screen " +
					"graphics context); the processor render path will be used instead.";

				// On Windows the usual cause is a missing OpenGL driver; Microsoft's free "OpenCL and
				// OpenGL Compatibility Pack" can supply one, so hint at it (Windows only).
				if (OperatingSystem.IsWindows())
				{
					warning +=
						" On Windows, installing the free Microsoft \"OpenCL and OpenGL Compatibility " +
						"Pack\" (https://apps.microsoft.com/detail/9NQPSL29BFFF) might enable it.";
				}

				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn(warning);
				}

				return false;
			}

			_gpuAvailable = true;
			_presenter?.UseGpu(_context.GrContext);

			// Record the chosen backend once, so which API is actually in use is obvious from the
			// log alone.
			if (this.Log().IsEnabled(LogLevel.Information))
			{
				this.Log().Info($"Video rendering on the graphics device is ready (backend: {_context.Backend}).");
			}

			_renderPathSettled();
		}
		catch (Exception e)
		{
			if (this.Log().IsEnabled(LogLevel.Warning))
			{
				this.Log().Warn("Graphics-device video rendering could not be initialized; the processor render path will be used instead.", e);
			}
			_context?.Dispose();
			_context = null;
			_gpuAvailable = false;
		}

		return _gpuAvailable;
	}

	// UI thread. The element re-entered the visual tree after a detach: resume frame driving. The
	// graphics context is deliberately NOT rebuilt here - EnsureGpu re-creates it on the next frame,
	// once the element has a live XamlRoot again.
	private void OnOwnerLoaded(object sender, RoutedEventArgs e)
	{
		if (_disposed || !_detached)
		{
			return;
		}

		_detached = false;

		// Re-create the presenter (the old one went with the old context) and ask for one frame,
		// so a paused player shows its picture again straight away.
		_ = Presenter;
		RequestTick();
	}

	// UI thread. The element left the visual tree: the window is closing or the page navigated
	// away. See the class remarks for why this cannot wait until disposal.
	private void OnOwnerUnloaded(object sender, RoutedEventArgs e)
	{
		if (_disposed || _detached)
		{
			return;
		}

		_detached = true; // an already-queued tick becomes a no-op
		ReleaseRenderResources();
	}

	// UI thread. Tears down in the order the graphics API requires: first the presenter's
	// composition surface, inside a frame scope (disposing it needs its context current), then the
	// context itself (SkiaGpuContext.Dispose runs its own frame scope; both are no-ops on Metal).
	// Resets the init latch so EnsureGpu can rebuild everything on a later frame.
	private void ReleaseRenderResources()
	{
		var presenter = _presenter;
		_presenter = null;

		if (presenter is not null)
		{
			presenter.Invalidated -= OnPresenterInvalidated;

			try
			{
				if (_gpuAvailable && _context is not null)
				{
					using (_context.BeginFrame())
					{
						presenter.Dispose();
					}
				}
				else
				{
					presenter.Dispose();
				}
			}
			catch (Exception e)
			{
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn("The video composition surface could not be torn down cleanly.", e);
				}
			}
		}

		// The SkiaGpuContext owns the GRContext and disposes it inside a frame scope of its own.
		_context?.Dispose();
		_context = null;

		_gpuInitAttempted = false;
		_gpuAvailable = false;

		_surface.ClearFrame();
	}

	/// <summary>
	/// Releases the driver's resources: unhooks the element's events and disposes the readback
	/// buffers, the presenter and the graphics context (each inside a frame scope, as graphics
	/// teardown requires).
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_owner.Loaded -= OnOwnerLoaded;
		_owner.Unloaded -= OnOwnerUnloaded;

		ReleaseRenderResources();

		_readbackA?.Dispose();
		_readbackA = null;
		_readbackB?.Dispose();
		_readbackB = null;
	}
}
