// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuRenderEngine.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) no head GPU device provider and no desktop direct presenter: Android's direct path is DirectSurface (WebGpuSurfacePresenter over the window of a SurfaceView the platform view hands over - the window members, the wait for it and the path decision replace TryCreateDirect); (2) the host has PresentSurfaceFrame instead of PresentDirect; (3) the Adapter type is qualified (a framework namespace of that name hides it); (4) a DeviceOptions change also replaces a DirectSurface device (it is the view's own).

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.UI.Hosting;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using SkiaSharp;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// What the render engine tells its owner (the <see cref="WebGpuRenderView"/>, or a test double). Every callback runs
/// on the thread that caused it - the frame's thread, or the user-interface thread for <c>Detach</c> - with the
/// engine's frame gate held.
/// </summary>
internal interface IWebGpuRenderEngineHost
{
	/// <summary>A device was created: raise the renderer's and the event's DeviceReady.</summary>
	void OnDeviceReady();

	/// <summary>The device is gone (or never came): raise DeviceLost. Called BEFORE the device is released.</summary>
	/// <param name="reason">Why.</param>
	void OnDeviceLost(string reason);

	/// <summary>The render path changed.</summary>
	/// <param name="oldPath">The previous path.</param>
	/// <param name="newPath">The new path.</param>
	void OnRenderPathChanged(WebGpuRenderPath oldPath, WebGpuRenderPath newPath);

	/// <summary>A finished frame's picture: show it (ownership transfers).</summary>
	/// <param name="frame">The picture.</param>
	void Present(SKImage frame);

	// ANDROID PORT (2): no PresentDirect (the desktop direct paths' GPU-resident frames are not ported); the DirectSurface
	// path presents to its window surface itself and only tells the owner a frame went there.

	/// <summary>
	/// A finished frame was presented to the DirectSurface path's window surface (the operating system shows it): count
	/// it and repaint the overlay above it.
	/// </summary>
	void PresentSurfaceFrame();

	/// <summary>Drop the picture on screen - before the bitmaps behind it are released.</summary>
	void ClearPresented();
}

/// <summary>
/// The frame PROTOCOL and the lifetime of a view's WebGPU device and target, with no XAML in it: <c>BeginFrame</c> /
/// <c>EndFrame</c> on any one thread at a time, the lazy device, the render-path choice, the Readback presentation,
/// device-loss handling and the native-module hand-off. The view owns one and forwards to it; the tests drive one
/// directly.
/// </summary>
/// <remarks>
/// <para>
/// <b>The frame gate.</b> A frame holds a monitor from <c>BeginFrame</c> to <c>EndFrame</c>, both on the SAME thread.
/// Another thread's <c>BeginFrame</c> waits for it (the Manual driver and native modules); the built-in drivers only
/// TRY to take it and skip a tick when someone else holds it (latest wins). WebGPU requires one thread at a time on a
/// device, and this is that rule enforced.
/// </para>
/// <para>
/// <b>Lifetime</b> (the VideoPlayer driver's rules): the device is created LAZILY, on the first frame after
/// <see cref="Attach"/> (the view loaded with a live XamlRoot) - never in Loaded itself; it is torn down at
/// <see cref="Detach"/> (Unloaded, NOT disposal), after DeviceLost has told the renderer to release its resources; and
/// it is re-created by the first frame after the next Attach. A new pixel size is a new target at the next frame. A
/// device that cannot be created (no adapter, or a launch that says there is none) is reported once and not retried
/// until the next Attach. Errors the OWNED device reports through its uncaptured-error callback mean its state can no
/// longer be trusted: DeviceLost, one log line, and a fresh device on the next frame.
/// </para>
/// </remarks>
internal sealed unsafe class WebGpuRenderEngine : IWebGpuDeviceSource, IWebGpuTextureTarget
{
	/// <summary>How long Detach waits for a frame another thread holds before deferring the teardown to its EndFrame.</summary>
	private static readonly TimeSpan DetachWait = TimeSpan.FromSeconds(2);

	private readonly object _frameGate = new();
	private readonly Func<WebGpuSkiaDeviceOptions?, WebGpuSkiaDevice> _createDevice;
	private readonly IWebGpuRenderEngineHost _host;
	private readonly WebGpuHandoffBlock _handoff;

	private WebGpuDeviceGateDecision _gate = WebGpuDeviceGateDecision.Proceed;
	private volatile bool _isLive;
	private long _pixelSize;        // width << 32 | height, written by the UI thread, read by the frame's thread
	private long _clearColor;       // premultiplied BGRA as SKColor bits, the same
	private WebGpuPresenter? _presenter;
	private volatile WebGpuRenderPath _path;
	private bool _unavailableLatched;
	private bool _teardownPending;
	private bool _nestedPassOpen;
	private int _frameThread;
	private long _frameIndex;
	private long _firstFrameTimestamp;
	private long _previousFrameTimestamp;
	private string? _lastTargetFailure;
	private bool _directNoticeLogged;
	private bool _directFailed;
	private int _devicesLost;
	private bool _creationFailed;
	private bool _ownedNoticeLogged;
	private volatile WebGpuSkiaDeviceOptions? _deviceOptions;
	private volatile bool _requireOwnedDevice;
	private int _settingsChanged;   // DeviceSettingChange bits, set by the UI thread, taken by the frame's thread
	private volatile WebGpuDeviceInfo? _deviceInfo;

	// ANDROID PORT (1): the DirectSurface path's window, handed over by the platform view (UI thread) and read by the
	// presenter on the frame's thread.
	private readonly IWebGpuWindowSurfaceFactory _surfaceFactory;
	private readonly Func<WebGpuNativeWindow?> _readWindow;
	private readonly System.Collections.Generic.List<WebGpuNativeWindow> _windowsToRelease = new(1);
	private volatile WebGpuNativeWindow? _window;
	private volatile bool _windowHostConnected;
	private long _attachTimestamp;
	private long _windowHostTimestamp;
	private int _windowVersion;

	[Flags]
	private enum DeviceSettingChange
	{
		None = 0,
		Options = 1,
		RequireOwnedDevice = 2,
	}

	/// <summary>Creates the engine. Nothing is created on the GPU until the first frame after <see cref="Attach"/>.</summary>
	/// <param name="host">Its owner.</param>
	/// <param name="createDevice">Creates the owned device from <see cref="DeviceOptions"/> (null:
	/// <see cref="WebGpuSkiaDevice.Create(WebGpuSkiaDeviceOptions)"/>, or <see cref="WebGpuSkiaDevice.Create()"/> for
	/// null options).</param>
	/// <param name="surfaceFactory">ANDROID PORT (1): creates the DirectSurface path's WebGPU surface over a window (null:
	/// the native-window surface; the tests substitute their own).</param>
	internal WebGpuRenderEngine(IWebGpuRenderEngineHost host, Func<WebGpuSkiaDeviceOptions?, WebGpuSkiaDevice>? createDevice = null,
		IWebGpuWindowSurfaceFactory? surfaceFactory = null)
	{
		_host = host ?? throw new ArgumentNullException(nameof(host));
		_createDevice = createDevice ?? CreateOwnedDevice;
		_surfaceFactory = surfaceFactory ?? WebGpuNativeWindowSurface.Factory;
		_readWindow = () => _window;
		Frame = new WebGpuFrame(this);
		_handoff = new WebGpuHandoffBlock(this);
		ClearColor = SKColors.Black;
	}

	/// <summary>The one frame object, reused for every frame.</summary>
	internal WebGpuFrame Frame { get; }

	/// <summary>The render path running now.</summary>
	internal WebGpuRenderPath RenderPath => _path;

	/// <summary>Whether a device exists (between DeviceReady and DeviceLost).</summary>
	internal bool IsDeviceReady => Volatile.Read(ref _presenter) is not null;

	/// <summary>The running render path's presenter (null without a device). Tests.</summary>
	internal WebGpuPresenter? Presenter => Volatile.Read(ref _presenter);

	/// <summary>Whether the engine is attached (the view is loaded).</summary>
	internal bool IsLive => _isLive;

	/// <summary>The view hand-off (native memory, same address for the engine's lifetime).</summary>
	internal WebGpuRenderViewHandoff* ViewHandoff => _handoff.View;

	/// <summary>The device hand-off inside <see cref="ViewHandoff"/>.</summary>
	internal CodebrixDeviceHandoff* DeviceHandoff => _handoff.Device;

	/// <summary>The frame hand-off the current frame filled.</summary>
	internal CodebrixFrameHandoff* FrameHandoff => _handoff.Frame;

	/// <summary>The current device (null without one). Read on the frame's thread.</summary>
	internal WebGpuSkiaDevice? Device => _presenter?.Device;

	/// <summary>What the current device is (null without one). Any thread.</summary>
	internal WebGpuDeviceInfo? DeviceInfo => _deviceInfo;

	/// <summary>
	/// The options the view's OWN device is created with (null = the engine's defaults). Any thread. A change while the
	/// view owns a device replaces it at the next frame (DeviceLost "Device options changed.", then DeviceReady).
	/// </summary>
	internal WebGpuSkiaDeviceOptions? DeviceOptions
	{
		get => _deviceOptions;
		set
		{
			_deviceOptions = value;
			MarkChanged(DeviceSettingChange.Options);
		}
	}

	/// <summary>
	/// True: never adopt a head's device - always create the view's own (Readback), even with direct GPU rendering
	/// enabled. Any thread. A change while a device exists with the latch on replaces it at the next frame.
	/// </summary>
	internal bool RequireOwnedDevice
	{
		get => _requireOwnedDevice;
		set
		{
			if (_requireOwnedDevice == value)
			{
				return;
			}

			_requireOwnedDevice = value;
			MarkChanged(DeviceSettingChange.RequireOwnedDevice);
		}
	}

	private void MarkChanged(DeviceSettingChange change) => Interlocked.Or(ref _settingsChanged, (int)change);

	private static WebGpuSkiaDevice CreateOwnedDevice(WebGpuSkiaDeviceOptions? options) =>
		options is null ? WebGpuSkiaDevice.Create() : WebGpuSkiaDevice.Create(options);

	/// <summary>The colour every frame's texture is cleared to before the renderer draws (unpremultiplied).</summary>
	internal SKColor ClearColor
	{
		get => (SKColor)(uint)Interlocked.Read(ref _clearColor);
		set => Interlocked.Exchange(ref _clearColor, (uint)value);
	}

	/// <summary>The pixel size the next frame renders at; (0, 0) renders no frame. Any thread.</summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	internal void SetPixelSize(int width, int height) =>
		Interlocked.Exchange(ref _pixelSize, ((long)Math.Max(0, width) << 32) | (uint)Math.Max(0, height));

	/// <summary>The pixel size the next frame renders at.</summary>
	internal (int Width, int Height) PixelSize
	{
		get
		{
			var packed = Interlocked.Read(ref _pixelSize);
			return ((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));
		}
	}

	/// <summary>
	/// The view entered a live visual tree (Loaded with a XamlRoot): remember what the head's gate said, and allow
	/// frames again. Creates nothing.
	/// </summary>
	/// <param name="gate">The owned-device gate's decision for this XamlRoot.</param>
	/// <remarks>ANDROID PORT (1): no native GPU device provider probe (no head device is adopted on Android); a direct path
	/// that failed is tried again (the platform view hosts its window surface again, see <see cref="WantsWindowSurface"/>).</remarks>
	internal void Attach(WebGpuDeviceGateDecision gate)
	{
		lock (_frameGate)
		{
			_gate = gate;
			_unavailableLatched = false;
			_creationFailed = false;
			_directFailed = false;
			_attachTimestamp = Stopwatch.GetTimestamp();
			_isLive = true;
		}
	}

	/// <summary>
	/// The view left the visual tree: no more frames, and the device and target are torn down - now, or by the
	/// EndFrame of a frame another thread still holds (after <see cref="DetachWait"/>).
	/// </summary>
	internal void Detach()
	{
		_isLive = false;

		if (Volatile.Read(ref _frameThread) == Environment.CurrentManagedThreadId)
		{
			// Unloaded raised from inside this thread's own frame (a Render handler that removed the view): the
			// frame's EndFrame tears down.
			_teardownPending = true;
			return;
		}

		if (!Monitor.TryEnter(_frameGate, DetachWait))
		{
			_teardownPending = true;
			return;
		}

		try
		{
			Teardown("The view was unloaded.", WebGpuRenderPath.None);
		}
		finally
		{
			Monitor.Exit(_frameGate);
		}
	}

	/// <summary>
	/// Opens a frame: <see cref="WebGpuFrameOutcome.Rendered"/> means it is open on this thread (and the frame gate is
	/// held until <see cref="EndFrame"/>); anything else means no frame is open.
	/// </summary>
	/// <param name="wait">True to wait while another thread holds a frame (Manual, modules); false to give up at once
	/// (the built-in drivers).</param>
	/// <returns>What happened.</returns>
	/// <exception cref="InvalidOperationException">This thread already holds an open frame.</exception>
	internal WebGpuFrameOutcome OpenFrame(bool wait)
	{
		if (Volatile.Read(ref _frameThread) == Environment.CurrentManagedThreadId)
		{
			throw new InvalidOperationException("BeginFrame was called twice without EndFrame.");
		}

		if (wait)
		{
			Monitor.Enter(_frameGate);
		}
		else if (!Monitor.TryEnter(_frameGate))
		{
			return WebGpuFrameOutcome.NotReady;
		}

		var opened = false;
		try
		{
			var outcome = Open();
			opened = outcome == WebGpuFrameOutcome.Rendered;
			return outcome;
		}
		finally
		{
			if (!opened)
			{
				Monitor.Exit(_frameGate);
			}
		}
	}

	/// <summary>
	/// Ends the frame this thread opened: validates and submits its commands, reads the picture back and presents it,
	/// then releases the frame gate.
	/// </summary>
	/// <exception cref="InvalidOperationException">This thread holds no open frame.</exception>
	/// <exception cref="WebGpuSkiaValidationException">The frame's commands failed validation; nothing was submitted
	/// or presented (the previous picture stays).</exception>
	internal void EndFrame()
	{
		if (!Frame.IsOpen || Volatile.Read(ref _frameThread) != Environment.CurrentManagedThreadId)
		{
			throw new InvalidOperationException("EndFrame was called without BeginFrame on this thread.");
		}

		ExceptionDispatchInfo? rejected = null;
		try
		{
			_nestedPassOpen = false;
			var presenter = _presenter!;
			try
			{
				presenter.EndPass(Frame.Encoder);
			}
			catch (WebGpuSkiaValidationException e)
			{
				rejected = ExceptionDispatchInfo.Capture(e);
			}

			if (rejected is null)
			{
				presenter.Show(_host);
			}

			if (presenter.TakeErrors() is { } errors)
			{
				LoseDevice("The WebGPU device reported an error and was replaced: " + errors);
			}
		}
		finally
		{
			CloseFrameObject();
			Volatile.Write(ref _frameThread, 0);
			ReleaseDetachedWindows(); // ANDROID PORT (1)
			if (_teardownPending)
			{
				_teardownPending = false;
				try
				{
					Teardown("The view was unloaded.", WebGpuRenderPath.None);
				}
				finally
				{
					Monitor.Exit(_frameGate);
				}
			}
			else
			{
				Monitor.Exit(_frameGate);
			}
		}

		rejected?.Throw();
	}

	// Frame gate held. Everything that has to happen before a frame can be recorded.
	private WebGpuFrameOutcome Open()
	{
		if (_teardownPending)
		{
			_teardownPending = false;
			Teardown("The view was unloaded.", WebGpuRenderPath.None);
		}

		ReleaseDetachedWindows(); // ANDROID PORT (1)

		if (!_isLive)
		{
			return WebGpuFrameOutcome.NotReady;
		}

		ApplyDeviceSettingChanges();

		if (_presenter?.FailureReason is { } failure)
		{
			// A direct path stopped working (its paint side could not wrap a frame, the head's device went away):
			// one line, DeviceLost, and the view continues on Readback from this very frame.
			FallBackToReadback(_presenter.Path, failure, _presenter.HeadDeviceGone);
		}

		if (_presenter is null)
		{
			var created = CreateDevice();
			if (created != WebGpuFrameOutcome.Rendered)
			{
				return created;
			}
		}

		var (width, height) = PixelSize;
		if (width == 0 || height == 0)
		{
			return WebGpuFrameOutcome.NotReady;
		}

		var presenter = _presenter!;
		if (!presenter.EnsureTarget(width, height, out var reason))
		{
			if (presenter.FailureReason is { } directFailure)
			{
				FallBackToReadback(presenter.Path, directFailure);
				return WebGpuFrameOutcome.NotReady;
			}

			// ANDROID PORT (1): the DirectSurface path's window does not exist now (the activity is in the background):
			// no frame, no line - the next frame after the window comes back renders.
			if (presenter.IsWaiting)
			{
				return WebGpuFrameOutcome.NotReady;
			}

			// Logged once per distinct reason: a size the device refuses would otherwise log every frame.
			if (reason != _lastTargetFailure)
			{
				_lastTargetFailure = reason;
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn($"The WebGPU render target ({width}x{height}) could not be created: {reason}");
				}
			}

			return WebGpuFrameOutcome.NotReady;
		}

		_lastTargetFailure = null;
		var target = presenter.Target!;
		var encoder = target.BeginWebGpuPass();
		if (encoder.IsNull)
		{
			return WebGpuFrameOutcome.NotReady;
		}

		presenter.OnFrameOpened();
		RecordClear(encoder, target.TextureView, ClearColor);

		var now = Stopwatch.GetTimestamp();
		if (_frameIndex == 0)
		{
			_firstFrameTimestamp = now;
			_previousFrameTimestamp = now;
		}

		var frame = Frame;
		frame.Encoder = encoder;
		frame.Texture = target.Texture;
		frame.TextureView = target.TextureView;
		frame.Format = target.Format;
		frame.Width = target.Width;
		frame.Height = target.Height;
		frame.FrameIndex = _frameIndex++;
		frame.ElapsedSeconds = (now - _firstFrameTimestamp) / (double)Stopwatch.Frequency;
		frame.DeltaSeconds = (now - _previousFrameTimestamp) / (double)Stopwatch.Frequency;
		_previousFrameTimestamp = now;
		CodebrixFrameHandoff.Fill(target, encoder, _handoff.Frame);
		frame.IsOpen = true;
		Volatile.Write(ref _frameThread, Environment.CurrentManagedThreadId);
		return WebGpuFrameOutcome.Rendered;
	}

	// Frame gate held. DeviceOptions / RequireOwnedDevice changed since the last frame: replace the device when the
	// change can make a difference (the rules), and let a device creation that failed (possibly because of the old
	// options) be tried again. A launch that said "no device" stays latched.
	private void ApplyDeviceSettingChanges()
	{
		var change = (DeviceSettingChange)Interlocked.Exchange(ref _settingsChanged, 0);
		if (change == DeviceSettingChange.None)
		{
			return;
		}

		if (_unavailableLatched && _creationFailed)
		{
			_unavailableLatched = false;
			_creationFailed = false;
		}

		if (_presenter is null)
		{
			return;
		}

		var recreate =
			((change & DeviceSettingChange.Options) != 0 && WebGpuSurfaceRules.DeviceOptionsChangeRecreatesDevice(_path)) // ANDROID PORT (4)
			|| ((change & DeviceSettingChange.RequireOwnedDevice) != 0
				&& WebGpuRenderViewRules.RequireOwnedDeviceChangeRecreatesDevice(DirectGpuRenderingMode.IsEnabled, _path));
		if (!recreate)
		{
			return;
		}

		if (this.Log().IsEnabled(LogLevel.Information))
		{
			this.Log().Info("The WebGPU device is replaced: " + WebGpuRenderViewRules.DeviceOptionsChangedReason);
		}

		// RequireOwnedDevice turned off asks for the direct path again; a direct path that FAILED stays failed until the
		// next Loaded otherwise.
		if ((change & DeviceSettingChange.RequireOwnedDevice) != 0)
		{
			_directFailed = false;
		}

		Teardown(WebGpuRenderViewRules.DeviceOptionsChangedReason, WebGpuRenderPath.None);
	}

	// Frame gate held. The lazy device: the gate first, then the device, then the path.
	private WebGpuFrameOutcome CreateDevice()
	{
		if (_unavailableLatched)
		{
			return WebGpuFrameOutcome.Unavailable;
		}

		if (!_gate.MayCreate)
		{
			_unavailableLatched = true;
			var reason = _gate.Reason ?? "The head reported that no WebGPU device is available.";
			if (this.Log().IsEnabled(_gate.Failed ? LogLevel.Warning : LogLevel.Information))
			{
				var line = "No WebGPU device for this view: " + reason;
				if (_gate.Failed)
				{
					this.Log().Warn(line);
				}
				else
				{
					this.Log().Info(line);
				}
			}

			_host.OnDeviceLost(reason);
			return WebGpuFrameOutcome.Unavailable;
		}

		// ANDROID PORT (1): no head device to adopt; the latch decides between Readback and the DirectSurface path.
		var decision = DecideDirectSurface();
		if (decision == WebGpuDirectSurfaceDecision.Wait)
		{
			return WebGpuFrameOutcome.NotReady;
		}

		WebGpuSkiaDevice device;
		try
		{
			device = _createDevice(_deviceOptions);
		}
		catch (Exception e)
		{
			_unavailableLatched = true;
			_creationFailed = true;
			if (this.Log().IsEnabled(LogLevel.Warning))
			{
				this.Log().Warn("The WebGPU device could not be created: " + e.Message, e);
			}

			_host.OnDeviceLost("The WebGPU device could not be created: " + e.Message);
			return WebGpuFrameOutcome.Unavailable;
		}

		if (!device.IsAvailable)
		{
			var reason = device.Reason ?? "No WebGPU device could be created.";
			device.Dispose();
			_unavailableLatched = true;
			_creationFailed = true;
			if (this.Log().IsEnabled(LogLevel.Warning))
			{
				this.Log().Warn("No WebGPU device for this view: " + reason);
			}

			_host.OnDeviceLost(reason);
			return WebGpuFrameOutcome.Unavailable;
		}

		return Start(decision == WebGpuDirectSurfaceDecision.DirectSurface
			? new WebGpuSurfacePresenter(device, _readWindow, _surfaceFactory)
			: new WebGpuReadbackPresenter(device));
	}

	// Frame gate held. A device exists: install its presenter, then DeviceReady (inside the presenter's error capture:
	// an adopted device has no handler of ours, so what a renderer creates there must not end the process).
	private WebGpuFrameOutcome Start(WebGpuPresenter presenter)
	{
		var device = presenter.Device;
		var path = presenter.Path;
		Volatile.Write(ref _presenter, presenter);
		_frameIndex = 0;
		CodebrixDeviceHandoff.Fill(this, _handoff.Device);
		_deviceInfo = WebGpuDeviceInfo.Query(device.Adapter, device.Device, ((IWebGpuDeviceSource)device).Backend);

		if (this.Log().IsEnabled(LogLevel.Information))
		{
			this.Log().Info($"WebGPU device ready: {device.AdapterName} ({device.Backend}), render path {path}.");
		}

		SetPath(path);
		string? deviceWorkError;
		presenter.BeginDeviceWork();
		try
		{
			_host.OnDeviceReady();
		}
		finally
		{
			deviceWorkError = presenter.EndDeviceWork();
		}

		if (deviceWorkError is not null)
		{
			LoseDevice("The WebGPU device reported an error and was replaced: " + deviceWorkError);
			return WebGpuFrameOutcome.NotReady;
		}

		return WebGpuFrameOutcome.Rendered;
	}

	// ANDROID PORT (1): replaces the Platform's TryCreateDirect. The latch is read here (plan 2.5/2.6), as there; the
	// direct path is the DirectSurface one (the view's own device presenting to its SurfaceView's window). Each Readback
	// reason is said ONCE per engine (RequireOwnedDevice gets the Platform's own line).
	private WebGpuDirectSurfaceDecision DecideDirectSurface()
	{
		var latch = DirectGpuRenderingMode.IsEnabled;
		var since = Math.Max(Volatile.Read(ref _attachTimestamp), Volatile.Read(ref _windowHostTimestamp));
		var decision = WebGpuSurfaceRules.Decide(latch, _requireOwnedDevice, _directFailed, _windowHostConnected,
			_window is not null, Stopwatch.GetElapsedTime(since).TotalMilliseconds);
		switch (decision)
		{
			case WebGpuDirectSurfaceDecision.Readback when latch && _requireOwnedDevice:
				if (!_ownedNoticeLogged && this.Log().IsEnabled(LogLevel.Information))
				{
					_ownedNoticeLogged = true;
					this.Log().Info(WebGpuRenderViewRules.RequireOwnedDeviceMessage);
				}

				break;

			case WebGpuDirectSurfaceDecision.ReadbackNoWindowHost:
				if (!_directNoticeLogged && this.Log().IsEnabled(LogLevel.Information))
				{
					_directNoticeLogged = true;
					this.Log().Info(WebGpuRenderViewRules.DirectPathFallbackMessage(WebGpuRenderPath.DirectSurface, WebGpuSurfaceRules.NoWindowHostReason));
				}

				break;

			case WebGpuDirectSurfaceDecision.ReadbackWindowNeverCame:
				// Latched until the next Loaded, as every direct failure: the platform view drops its window surface host.
				_directFailed = true;
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn(WebGpuRenderViewRules.DirectPathFallbackMessage(WebGpuRenderPath.DirectSurface, WebGpuSurfaceRules.WindowNeverCameReason));
				}

				break;
		}

		return decision;
	}

	/// <summary>
	/// ANDROID PORT (1): whether the view's platform view should host a window surface now (the direct path may be taken).
	/// Read by the view on the user-interface thread after every render-path change and every Loaded.
	/// </summary>
	internal bool WantsWindowSurface =>
		WebGpuSurfaceRules.WantsWindowSurface(DirectGpuRenderingMode.IsEnabled, _requireOwnedDevice, _directFailed);

	/// <summary>ANDROID PORT (1): whether a platform view that hosts a window surface is connected.</summary>
	internal bool IsWindowHostConnected => _windowHostConnected;

	/// <summary>ANDROID PORT (1): the window the DirectSurface path presents to now (null while there is none).</summary>
	internal WebGpuNativeWindow? Window => _window;

	/// <summary>
	/// ANDROID PORT (1): a platform view that hosts a window surface (a SurfaceView under the element) is connected; the
	/// first device may now wait up to <see cref="WebGpuSurfaceRules.WindowWaitMilliseconds"/> for its window. UI thread.
	/// </summary>
	internal void ConnectWindowHost()
	{
		Volatile.Write(ref _windowHostTimestamp, Stopwatch.GetTimestamp());
		_windowHostConnected = true;
	}

	/// <summary>ANDROID PORT (1): the window surface host is gone (its window, if any, is detached first). UI thread.</summary>
	internal void DisconnectWindowHost()
	{
		DetachWindow();
		_windowHostConnected = false;
	}

	/// <summary>
	/// ANDROID PORT (1): Android created the window surface (SurfaceHolder.Callback.surfaceCreated): the next frame
	/// presents to it. The engine owns the platform's reference from now on and gives it back through
	/// <paramref name="release"/> once the WebGPU surface over it is released (<see cref="DetachWindow"/>). UI thread.
	/// </summary>
	/// <param name="handle">The native window pointer (an acquired reference).</param>
	/// <param name="width">Its width in pixels (0 while unknown).</param>
	/// <param name="height">Its height in pixels (0 while unknown).</param>
	/// <param name="release">Gives the reference back (null: nothing to give back).</param>
	internal void AttachWindow(nint handle, int width, int height, Action<nint>? release)
	{
		DetachWindow();
		_window = new WebGpuNativeWindow(handle, width, height, Interlocked.Increment(ref _windowVersion), release);
	}

	/// <summary>
	/// ANDROID PORT (1): Android changed the window surface (surfaceChanged: a new size or format): the next frame
	/// reconfigures it. UI thread.
	/// </summary>
	/// <param name="width">The new width in pixels.</param>
	/// <param name="height">The new height in pixels.</param>
	internal void ResizeWindow(int width, int height)
	{
		if (_window is { } window)
		{
			_window = new WebGpuNativeWindow(window.Handle, width, height, Interlocked.Increment(ref _windowVersion), window.Release);
		}
	}

	/// <summary>
	/// ANDROID PORT (1): Android is destroying the window surface (surfaceDestroyed): no frame presents to it any more,
	/// the WebGPU surface over it is unconfigured and released, then the platform's reference is given back - here, once
	/// a frame another thread holds has ended (waiting up to <see cref="DetachWait"/>, else that frame's EndFrame does it).
	/// UI thread.
	/// </summary>
	/// <returns>True when the window was released before returning (false: no window, or deferred to a frame).</returns>
	internal bool DetachWindow()
	{
		var window = Interlocked.Exchange(ref _window, null);
		if (window is null)
		{
			return false;
		}

		lock (_windowsToRelease)
		{
			_windowsToRelease.Add(window);
		}

		if (Volatile.Read(ref _frameThread) == Environment.CurrentManagedThreadId)
		{
			// Destroyed from inside this thread's own frame (a Render handler that removed the view): its EndFrame releases.
			return false;
		}

		if (!Monitor.TryEnter(_frameGate, DetachWait))
		{
			return false;
		}

		try
		{
			ReleaseDetachedWindows();
		}
		finally
		{
			Monitor.Exit(_frameGate);
		}

		return true;
	}

	// Frame gate held. The WebGPU surface goes first (it is the presenter's), then the platform's references.
	private void ReleaseDetachedWindows()
	{
		WebGpuNativeWindow[]? windows = null;
		lock (_windowsToRelease)
		{
			if (_windowsToRelease.Count == 0)
			{
				return;
			}

			windows = _windowsToRelease.ToArray();
			_windowsToRelease.Clear();
		}

		(_presenter as WebGpuSurfacePresenter)?.ReleaseWindowSurface();
		foreach (var window in windows)
		{
			try
			{
				window.Release?.Invoke(window.Handle);
			}
			catch (Exception e)
			{
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn("The native window could not be released: " + e.Message, e);
				}
			}
		}
	}

	// Frame gate held. The direct path that was running cannot go on: the one line, DeviceLost (the renderer lets go of
	// what it made on the adopted device), and the next device is the view's own.
	private void FallBackToReadback(WebGpuRenderPath path, string reason, bool headDeviceGone = false)
	{
		_directFailed = true;

		// The head's device going away (its window closing) is not a fault: Information, not Warning.
		var level = headDeviceGone ? LogLevel.Information : LogLevel.Warning;
		if (this.Log().IsEnabled(level))
		{
			if (headDeviceGone)
			{
				this.Log().Info(WebGpuRenderViewRules.DirectPathFallbackMessage(path, reason));
			}
			else
			{
				this.Log().Warn(WebGpuRenderViewRules.DirectPathFallbackMessage(path, reason));
			}
		}

		Teardown(WebGpuRenderViewRules.DirectPathFallbackMessage(path, reason), WebGpuRenderPath.Readback);
	}

	// Frame gate held.
	private void LoseDevice(string reason)
	{
		_devicesLost++;
		if (this.Log().IsEnabled(_devicesLost == 1 ? LogLevel.Warning : LogLevel.Debug))
		{
			if (_devicesLost == 1)
			{
				this.Log().Warn(reason);
			}
			else
			{
				this.Log().Debug(reason);
			}
		}

		Teardown(reason, WebGpuRenderPath.None, raiseLost: true);
	}

	// Frame gate held. DeviceLost first (the renderer releases its resources while the device still exists), then the
	// picture (no wrapper may outlive the bitmaps), then the target and the device.
	private void Teardown(string reason, WebGpuRenderPath newPath, bool raiseLost = true)
	{
		var presenter = _presenter;
		if (presenter is null)
		{
			SetPath(newPath);
			return;
		}

		if (raiseLost)
		{
			try
			{
				_host.OnDeviceLost(reason);
			}
			catch (Exception e)
			{
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn("A DeviceLost handler threw: " + e.Message, e);
				}
			}
		}

		_host.ClearPresented();
		Volatile.Write(ref _presenter, null);
		_deviceInfo = null;
		presenter.Dispose();
		CodebrixDeviceHandoff.Fill(this, _handoff.Device);
		SetPath(newPath);
	}

	private void SetPath(WebGpuRenderPath path)
	{
		var old = _path;
		if (old == path)
		{
			return;
		}

		_path = path;
		_host.OnRenderPathChanged(old, path);
	}

	private void CloseFrameObject()
	{
		var frame = Frame;
		frame.IsOpen = false;
		frame.Encoder = CommandEncoder.Null;
		frame.Texture = Texture.Null;
		frame.TextureView = TextureView.Null;
	}

	// Every frame starts cleared: one render pass with LoadOp.Clear, ended at once. Premultiplied, like the texture.
	private static void RecordClear(CommandEncoder encoder, TextureView view, SKColor color)
	{
		var alpha = color.Alpha / 255.0;
		var attachment = new RenderPassColorAttachment
		{
			View = view,
			DepthSlice = Constants.DepthSliceUndefined,
			LoadOp = LoadOp.Clear,
			StoreOp = StoreOp.Store,
			ClearValue = new Color
			{
				R = color.Red / 255.0 * alpha,
				G = color.Green / 255.0 * alpha,
				B = color.Blue / 255.0 * alpha,
				A = alpha,
			},
		};
		var descriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
		var pass = Wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor);
		Wgpu.RenderPassEncoderEnd(pass);
		Wgpu.RenderPassEncoderRelease(pass);
	}

	// ------------------------------------------------------------------ IWebGpuDeviceSource

	/// <inheritdoc/>
	public Instance Instance => _presenter?.Device.Instance ?? Instance.Null;

	/// <inheritdoc/>
	public CodeBrix.WebGpu.Native.Adapter Adapter => _presenter?.Device.Adapter ?? CodeBrix.WebGpu.Native.Adapter.Null; // ANDROID PORT: qualified - CodeBrix.Platform.UI.Adapter (a namespace of the re-shipped framework) hides the type here.

	/// <inheritdoc/>
	Device IWebGpuDeviceSource.Device => _presenter?.Device.Device ?? CodeBrix.WebGpu.Native.Device.Null;

	/// <inheritdoc/>
	public Queue Queue => _presenter?.Device.Queue ?? Queue.Null;

	/// <inheritdoc/>
	public BackendType Backend => _presenter?.Device is { } device ? ((IWebGpuDeviceSource)device).Backend : BackendType.Undefined;

	/// <inheritdoc/>
	public bool HasUncapturedErrorHandler => _presenter?.Device is { } device && ((IWebGpuDeviceSource)device).HasUncapturedErrorHandler;

	// ------------------------------------------------------------------ IWebGpuTextureTarget

	private WebGpuSkiaTarget? OpenTarget => Frame.IsOpen ? _presenter?.Target : null;

	/// <inheritdoc/>
	public Texture Texture => OpenTarget?.Texture ?? Texture.Null;

	/// <inheritdoc/>
	public TextureView TextureView => OpenTarget?.TextureView ?? TextureView.Null;

	/// <inheritdoc/>
	public TextureFormat Format => WebGpuPresenter.Format;

	/// <inheritdoc/>
	public int Width => OpenTarget?.Width ?? 0;

	/// <inheritdoc/>
	public int Height => OpenTarget?.Height ?? 0;

	/// <inheritdoc/>
	public int SampleCount => 1;

	/// <inheritdoc/>
	public TextureUsage Usage => OpenTarget?.Usage ?? TextureUsage.None;

	/// <inheritdoc/>
	public bool IsInWebGpuPass => _nestedPassOpen;

	/// <summary>
	/// Hands out the OPEN frame's encoder to a library that drives the pass protocol itself (a graphics layer's
	/// "frame on a shared target"). A null encoder outside a frame: open one first (or do the work in Render).
	/// </summary>
	/// <returns>The frame's encoder, or a null encoder when this thread has no open frame.</returns>
	/// <exception cref="InvalidOperationException">The pass was already handed out in this frame.</exception>
	public CommandEncoder BeginWebGpuPass()
	{
		if (!Frame.IsOpen || Volatile.Read(ref _frameThread) != Environment.CurrentManagedThreadId)
		{
			return CommandEncoder.Null;
		}

		if (_nestedPassOpen)
		{
			throw new InvalidOperationException("BeginWebGpuPass was called twice without EndWebGpuPass.");
		}

		_nestedPassOpen = true;
		return Frame.Encoder;
	}

	/// <summary>
	/// Closes the pass <see cref="BeginWebGpuPass"/> handed out. The commands are validated and submitted when the
	/// FRAME ends (<c>EndFrame</c>), not here.
	/// </summary>
	/// <exception cref="InvalidOperationException">No pass was handed out.</exception>
	public void EndWebGpuPass()
	{
		if (!_nestedPassOpen)
		{
			throw new InvalidOperationException("EndWebGpuPass was called without BeginWebGpuPass.");
		}

		_nestedPassOpen = false;
	}
}
