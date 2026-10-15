// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuRenderView.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) no head GPU device provider is resolved (the desktop direct paths are not ported): the DirectSurface path's window surface host (the platform view's SurfaceView) and its window are wired in instead; (2) the host has PresentSurfaceFrame instead of PresentDirect; (3) the class remarks and the error-handler remarks describe Android's render paths; (4) the Adapter type is qualified (a framework namespace of that name hides it).

#nullable enable

using System;
using System.Threading;
using CodeBrix.Android.UI.WebGpu.Android;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Graphics;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Foundation;
using WinColor = Windows.UI.Color;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// A XAML element that shows what WebGPU renders. Give it a size in a page, then either handle <see cref="Render"/>
/// or set <see cref="Renderer"/> to an <see cref="IWebGpuRenderer"/>, and record WebGPU commands (the raw
/// CodeBrix.WebGpu.Native API) into the <see cref="WebGpuFrame"/> it hands you: a command encoder, and a texture the
/// size of the element (times its rasterization scale) already cleared to <see cref="ClearColor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Drivers.</b> <see cref="FrameDriver"/> decides when frames run: Continuous (the default; at
/// <see cref="TargetFrameRate"/> on the user-interface thread, until <see cref="IsPaused"/>), OnDemand (one frame per
/// <see cref="Invalidate"/>), or Manual (you call <see cref="BeginFrame"/> / <see cref="EndFrame"/> yourself, from any
/// ONE thread at a time - your own render thread included).
/// </para>
/// <para>
/// <b>Device.</b> The view owns its WebGPU device. It is created lazily, on the first frame after the view is loaded
/// (<see cref="DeviceReady"/>), torn down when the view is unloaded (<see cref="DeviceLost"/>, so a renderer releases
/// its resources), and created again on the next frame after the view is loaded again. A machine with no WebGPU
/// adapter raises <see cref="DeviceLost"/> once with the engine's reason; the view then shows what is behind it.
/// </para>
/// <para>
/// <b>Render path.</b> <see cref="RenderPath"/> says how frames reach the screen. Readback - the default on every head -
/// renders off-screen, reads each finished frame back once and draws it through the head's own canvas, so the view
/// clips, overlaps and transforms like any other XAML content. ANDROID PORT (3): on Android the view's own device
/// is a Vulkan device and the canvas is the SkiaSharp.Views add-in's native Skia view. An application that opted in to
/// direct GPU rendering (CodeBrixApplication.UseDirectGpuRenderingMode) gets DirectSurface instead: the view's native
/// view hosts a SurfaceView under its canvas, each frame is drawn onto that window surface on the GPU and presented
/// (Fifo, synchronised with the display), and the operating system composites the surface with the window - nothing is
/// read back. The frame protocol is unchanged (the same BGRA8Unorm texture the size of the element, the same events
/// and hand-off); the Overlay is drawn by the canvas ABOVE the surface. Anything that fails on that path - one log line,
/// DeviceLost, and the view continues on Readback (its SurfaceView removed).
/// </para>
/// <para>
/// <b>Overlay.</b> <see cref="Overlay"/> (and <see cref="IWebGpuRenderer.OnOverlay"/>) draw over each frame with
/// SkiaSharp. They run while the head paints the window, not on the frame's thread: never touch WebGPU objects
/// there.
/// </para>
/// <para>
/// <b>Sharing.</b> The view is an <see cref="IWebGpuDeviceSource"/> (its device) and an
/// <see cref="IWebGpuTextureTarget"/> (the open frame's texture, valid only between <c>BeginFrame</c> and
/// <c>EndFrame</c>), so a library that accepts those contracts draws into it with no knowledge of this package; and
/// <see cref="GetViewHandoff"/> hands the whole view to a native (Rust, C, C++) module.
/// </para>
/// </remarks>
[Bindable]
public sealed partial class WebGpuRenderView : FrameworkElement, IWebGpuDeviceSource, IWebGpuTextureTarget, IWebGpuRenderEngineHost
{
	private readonly WebGpuRenderEngine _engine;
	private readonly WebGpuFrameScheduler _scheduler;
	private readonly WebGpuFrameSurface _surface;
	private readonly DispatcherQueue? _dispatcherQueue;
	private readonly DispatcherQueueHandler _repaintHandler;
	private readonly DispatcherTimer _resizeSettleTimer;
	private readonly WebGpuRenderEventArgs _renderArgs;
	private readonly WebGpuOverlayEventArgs _overlayArgs = new();

	private SKCanvasVisualBase? _canvasVisual;
	private XamlRoot? _hookedXamlRoot;
	private volatile IWebGpuRenderer? _renderer;
	private int _repaintPosted;
	private IWebGpuWindowSurfaceHost? _windowSurfaceHost; // ANDROID PORT (1)
	private int _builtInFrameFailures;
	private bool _revertingTargetFrameRate;

	/// <summary>Creates the view. Nothing touches the GPU until the first frame after the view is loaded.</summary>
	public WebGpuRenderView()
	{
		// The load-order rule for native modules: codebrix_wgpu is loaded (and checked) by .NET BEFORE any module's
		// native library, so a module linked against it reuses this one copy instead of making the loader find its own.
		try
		{
			CodeBrixWebGpuNative.EnsureLoaded();
		}
		catch (Exception e)
		{
			// The device creation reports the same failure as the view's DeviceLost reason; a view must never stop
			// a page from loading.
			if (this.Log().IsEnabled(LogLevel.Warning))
			{
				this.Log().Warn("The WebGPU native library could not be loaded: " + e.Message);
			}
		}

		_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
		_repaintHandler = RepaintOnUIThread;
		_surface = new WebGpuFrameSurface(RequestRepaint, DrawOverlay);
		_engine = new WebGpuRenderEngine(this);
		_renderArgs = new WebGpuRenderEventArgs(_engine.Frame);
		_scheduler = new WebGpuFrameScheduler(PlatformContract.Create<IWebGpuFrameTimer>(this), RunBuiltInFrame);

		_resizeSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(WebGpuRenderViewRules.ResizeSettleMilliseconds) };
		_resizeSettleTimer.Tick += OnResizeSettled;

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		SizeChanged += OnSizeChanged;
	}

	#region | Events |

	/// <summary>
	/// Raised when a device has been created - on the first frame after the view is loaded, and again after a lost
	/// device is replaced. Create pipelines and buffers here. Raised on the thread that runs the frame, after
	/// <see cref="IWebGpuRenderer.OnDeviceReady"/>.
	/// </summary>
	public event EventHandler<WebGpuDeviceReadyEventArgs>? DeviceReady;

	/// <summary>
	/// Raised for every frame the built-in drivers run (Continuous, OnDemand), on the user-interface thread, after
	/// <see cref="IWebGpuRenderer.OnRender"/>. Record into <see cref="WebGpuRenderEventArgs.Frame"/>. Not raised under
	/// the Manual driver: there, the code that called <see cref="BeginFrame"/> renders.
	/// </summary>
	public event EventHandler<WebGpuRenderEventArgs>? Render;

	/// <summary>
	/// Raised every time the head paints the view (not on the frame's thread), after the frame has been drawn and after
	/// <see cref="IWebGpuRenderer.OnOverlay"/>: draw a HUD with SkiaSharp. Never touch WebGPU objects here.
	/// </summary>
	public event EventHandler<WebGpuOverlayEventArgs>? Overlay;

	/// <summary>
	/// Raised when the view has no usable device any more (or never got one): no adapter on this machine, an error the
	/// device reported, the view unloaded, or a launch that said there is no device. Release device resources here.
	/// Raised after <see cref="IWebGpuRenderer.OnDeviceLost"/>, before the device is released.
	/// </summary>
	public event EventHandler<WebGpuDeviceLostEventArgs>? DeviceLost;

	/// <summary>Raised when <see cref="RenderPath"/> changes, on the thread that changed it.</summary>
	public event EventHandler<WebGpuRenderPathChangedEventArgs>? RenderPathChanged;

	#endregion

	#region | Dependency properties |

	/// <summary>Identifies the <see cref="FrameDriver"/> dependency property.</summary>
	public static readonly DependencyProperty FrameDriverProperty = DependencyProperty.Register(
		nameof(FrameDriver), typeof(WebGpuFrameDriver), typeof(WebGpuRenderView),
		new PropertyMetadata(WebGpuFrameDriver.Continuous, (o, e) => ((WebGpuRenderView)o)._scheduler.Driver = (WebGpuFrameDriver)e.NewValue));

	/// <summary>What decides when frames run: Continuous (the default), OnDemand or Manual.</summary>
	public WebGpuFrameDriver FrameDriver
	{
		get => (WebGpuFrameDriver)GetValue(FrameDriverProperty);
		set => SetValue(FrameDriverProperty, value);
	}

	/// <summary>Identifies the <see cref="TargetFrameRate"/> dependency property.</summary>
	public static readonly DependencyProperty TargetFrameRateProperty = DependencyProperty.Register(
		nameof(TargetFrameRate), typeof(double), typeof(WebGpuRenderView),
		new PropertyMetadata(WebGpuRenderViewRules.DefaultTargetFrameRate, (o, e) => ((WebGpuRenderView)o).OnTargetFrameRateChanged((double)e.OldValue, (double)e.NewValue)));

	/// <summary>
	/// The Continuous driver's rate in frames per second (default 60). Zero means as fast as the user-interface thread
	/// allows: a new frame as soon as the previous one has been presented and the thread is free, with no timer - not
	/// tied to the display's refresh. A late frame is never caught up (latest wins).
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
	public double TargetFrameRate
	{
		get => (double)GetValue(TargetFrameRateProperty);
		set => SetValue(TargetFrameRateProperty, value);
	}

	/// <summary>Identifies the <see cref="IsPaused"/> dependency property.</summary>
	public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
		nameof(IsPaused), typeof(bool), typeof(WebGpuRenderView),
		new PropertyMetadata(false, (o, e) => ((WebGpuRenderView)o)._scheduler.IsPaused = (bool)e.NewValue));

	/// <summary>True pauses the Continuous driver (the last frame stays on screen); false resumes it.</summary>
	public bool IsPaused
	{
		get => (bool)GetValue(IsPausedProperty);
		set => SetValue(IsPausedProperty, value);
	}

	/// <summary>Identifies the <see cref="Renderer"/> dependency property.</summary>
	public static readonly DependencyProperty RendererProperty = DependencyProperty.Register(
		nameof(Renderer), typeof(IWebGpuRenderer), typeof(WebGpuRenderView),
		new PropertyMetadata(null, (o, e) => ((WebGpuRenderView)o)._renderer = e.NewValue as IWebGpuRenderer));

	/// <summary>
	/// A renderer class the view calls for each step (device ready, render, overlay, device lost) BEFORE raising the
	/// matching event. Settable from XAML. Set it before the view is loaded to be told about the first device.
	/// </summary>
	public IWebGpuRenderer? Renderer
	{
		get => (IWebGpuRenderer?)GetValue(RendererProperty);
		set => SetValue(RendererProperty, value);
	}

	/// <summary>Identifies the <see cref="Stretch"/> dependency property.</summary>
	public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
		nameof(Stretch), typeof(Stretch), typeof(WebGpuRenderView),
		new PropertyMetadata(Stretch.Fill, (o, e) => ((WebGpuRenderView)o).OnStretchChanged((Stretch)e.NewValue)));

	/// <summary>
	/// How a frame fits the element. The default, Fill, is exact: the texture IS the element's size. The others matter
	/// only for the moment between a resize and the next frame, or for a frame larger than the texture cap.
	/// </summary>
	public Stretch Stretch
	{
		get => (Stretch)GetValue(StretchProperty);
		set => SetValue(StretchProperty, value);
	}

	/// <summary>Identifies the <see cref="ClearColor"/> dependency property.</summary>
	public static readonly DependencyProperty ClearColorProperty = DependencyProperty.Register(
		nameof(ClearColor), typeof(WinColor), typeof(WebGpuRenderView),
		new PropertyMetadata(Colors.Black, (o, e) => ((WebGpuRenderView)o).OnClearColorChanged((WinColor)e.NewValue)));

	/// <summary>
	/// The colour every frame's texture is cleared to before the renderer draws (default opaque black). A transparent
	/// colour lets what is behind the view show through wherever the renderer draws nothing.
	/// </summary>
	public WinColor ClearColor
	{
		get => (WinColor)GetValue(ClearColorProperty);
		set => SetValue(ClearColorProperty, value);
	}

	/// <summary>Identifies the <see cref="DeviceOptions"/> dependency property.</summary>
	public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
		nameof(DeviceOptions), typeof(WebGpuSkiaDeviceOptions), typeof(WebGpuRenderView),
		new PropertyMetadata(null, (o, e) => ((WebGpuRenderView)o).OnDeviceOptionsChanged(e.NewValue as WebGpuSkiaDeviceOptions)));

	/// <summary>
	/// How the view's OWN device is created (CodeBrix.WebGpu.Skia's options: backend, fallback, power preference,
	/// fallback adapter); null (the default) = the engine's defaults. Consulted every time the view creates its own
	/// device. Setting it while the view owns a device replaces that device at the next frame: DeviceLost("Device
	/// options changed.") then DeviceReady. Assign a NEW options object to apply a change; changing the members of the
	/// object already set is seen only by the next device creation. An adopted device (a direct path) is the head's
	/// and is not affected - see <see cref="RequireOwnedDevice"/>.
	/// </summary>
	public WebGpuSkiaDeviceOptions? DeviceOptions
	{
		get => (WebGpuSkiaDeviceOptions?)GetValue(DeviceOptionsProperty);
		set => SetValue(DeviceOptionsProperty, value);
	}

	/// <summary>Identifies the <see cref="RequireOwnedDevice"/> dependency property.</summary>
	public static readonly DependencyProperty RequireOwnedDeviceProperty = DependencyProperty.Register(
		nameof(RequireOwnedDevice), typeof(bool), typeof(WebGpuRenderView),
		new PropertyMetadata(false, (o, e) => ((WebGpuRenderView)o).OnRequireOwnedDeviceChanged((bool)e.NewValue)));

	/// <summary>
	/// True: the view never adopts a head's GPU device, even when the application enabled direct GPU rendering - it
	/// always creates its own device from <see cref="DeviceOptions"/> and runs Readback. For a renderer whose
	/// requirements (features, limits) the head's device cannot meet. Default false. Setting it while a device exists
	/// with direct GPU rendering enabled replaces the device at the next frame (DeviceLost("Device options changed.")).
	/// </summary>
	public bool RequireOwnedDevice
	{
		get => (bool)GetValue(RequireOwnedDeviceProperty);
		set => SetValue(RequireOwnedDeviceProperty, value);
	}

	#endregion

	#region | Read-only state |

	/// <summary>
	/// What the current device is - adapter, backend, the features and limits it was created with - or null without a
	/// device. Set before <see cref="DeviceReady"/>, cleared at <see cref="DeviceLost"/>; the same on an owned and on an
	/// adopted device. Any thread.
	/// </summary>
	public WebGpuDeviceInfo? DeviceInfo => _engine.DeviceInfo;

	/// <summary>How frames reach the screen now (None until the first device exists). Changes raise <see cref="RenderPathChanged"/>.</summary>
	public WebGpuRenderPath RenderPath => _engine.RenderPath;

	/// <summary>True between <see cref="DeviceReady"/> and <see cref="DeviceLost"/>.</summary>
	public bool IsDeviceReady => _engine.IsDeviceReady;

	/// <summary>The texture format every frame renders in (BGRA8Unorm): the colour-target format pipelines must use.</summary>
	public TextureFormat PreferredFormat => WebGpuPresenter.Format;

	/// <summary>
	/// The scale the texture is rendered at: the window's rasterization scale (1 = 96 DPI). The texture is the element's
	/// size times this, in pixels.
	/// </summary>
	public double PixelScale { get; private set; } = 1.0;

	/// <summary>The width of the texture the next frame renders into, in pixels (0 before the view has a size).</summary>
	public int TextureWidth => _engine.PixelSize.Width;

	/// <summary>The height of the texture the next frame renders into, in pixels (0 before the view has a size).</summary>
	public int TextureHeight => _engine.PixelSize.Height;

	#endregion

	#region | Frame protocol |

	/// <summary>
	/// Opens a frame on the calling thread - any thread, one frame at a time: a second thread waits until the frame
	/// is ended. The view creates its device here the first time. Record into the returned frame, then call
	/// <see cref="EndFrame"/> (or dispose the frame) on the SAME thread. Under the built-in drivers the view calls this
	/// itself; call it directly under the Manual driver.
	/// </summary>
	/// <returns>The open frame, or null when the view cannot render now (not loaded, no device, no size yet) - skip the
	/// frame and do not call <see cref="EndFrame"/>.</returns>
	/// <exception cref="InvalidOperationException">This thread already has an open frame.</exception>
	public WebGpuFrame? BeginFrame() =>
		_engine.OpenFrame(wait: true) == WebGpuFrameOutcome.Rendered ? _engine.Frame : null;

	/// <summary>
	/// Ends the frame this thread opened: validates and submits its commands, reads the picture back and shows it (on
	/// Android's DirectSurface path: draws it onto the window surface and presents it).
	/// </summary>
	/// <exception cref="InvalidOperationException">This thread has no open frame.</exception>
	/// <exception cref="CodeBrix.WebGpu.Skia.WebGpuSkiaValidationException">The frame's commands failed WebGPU
	/// validation: nothing was submitted and the previous picture stays. The view is still usable.</exception>
	public void EndFrame() => _engine.EndFrame();

	/// <summary>
	/// OnDemand: renders one frame soon (any number of calls before it runs make one frame). A no-op under Continuous
	/// and Manual. Callable from any thread.
	/// </summary>
	public void Invalidate() => _scheduler.Invalidate();

	/// <summary>
	/// The view's device as a native-module hand-off (CodeBrix.WebGpu.Native's <c>WGPUCodebrixDeviceHandoff</c>): the
	/// function table and the instance, adapter, device and queue. Filled when a device is created; its device member
	/// is null while the view has none.
	/// </summary>
	/// <returns>A pointer into memory the view owns (the same address for the view's lifetime; the
	/// <c>device</c> member of <see cref="GetViewHandoff"/> points at it).</returns>
	public unsafe CodebrixDeviceHandoff* GetDeviceHandoff() => _engine.DeviceHandoff;

	/// <summary>
	/// The whole view as a native-module hand-off: C-callable <c>begin_frame</c> / <c>end_frame</c> plus a pointer to
	/// the device hand-off, so a module can run its own render loop on its own thread (set <see cref="FrameDriver"/> to
	/// Manual). See <see cref="WebGpuRenderViewHandoff"/>.
	/// </summary>
	/// <returns>A pointer into memory the view owns, valid for the view's lifetime.</returns>
	public unsafe WebGpuRenderViewHandoff* GetViewHandoff() => _engine.ViewHandoff;

	#endregion

	#region | IWebGpuDeviceSource / IWebGpuTextureTarget |

	/// <summary>The device's instance (null without a device). Owned by the view; take a reference to keep it.</summary>
	public Instance Instance => _engine.Instance;

	/// <summary>The device's adapter (null without a device). Owned by the view.</summary>
	public CodeBrix.WebGpu.Native.Adapter Adapter => _engine.Adapter; // ANDROID PORT: qualified - CodeBrix.Platform.UI.Adapter (a namespace of the re-shipped framework) hides the type here.

	/// <summary>The view's device (null without one: before the first frame, and after <see cref="DeviceLost"/>). Owned by the view.</summary>
	public Device Device => ((IWebGpuDeviceSource)_engine).Device;

	/// <summary>The device's queue (null without a device). Owned by the view.</summary>
	public Queue Queue => _engine.Queue;

	/// <summary>The device's backend (Undefined without a device).</summary>
	public BackendType Backend => _engine.Backend;

	/// <summary>
	/// True on Readback: the view's own device reports errors to the view instead of ending the process. False on a
	/// direct path: the adopted device has no such handler (the view scopes its frames and DeviceReady instead).
	/// ANDROID PORT (3): Android's direct path, DirectSurface, runs on the view's OWN device too - true there as well.
	/// </summary>
	public bool HasUncapturedErrorHandler => _engine.HasUncapturedErrorHandler;

	/// <summary>The open frame's texture: valid ONLY between <see cref="BeginFrame"/> and <see cref="EndFrame"/> (null otherwise).</summary>
	public Texture Texture => _engine.Texture;

	/// <summary>The open frame's texture view: valid ONLY between <see cref="BeginFrame"/> and <see cref="EndFrame"/> (null otherwise).</summary>
	public TextureView TextureView => _engine.TextureView;

	/// <summary>The texture format (BGRA8Unorm), the same as <see cref="PreferredFormat"/>.</summary>
	public TextureFormat Format => _engine.Format;

	/// <inheritdoc/>
	int IWebGpuTextureTarget.Width => _engine.Width;

	/// <inheritdoc/>
	int IWebGpuTextureTarget.Height => _engine.Height;

	/// <inheritdoc/>
	int IWebGpuTextureTarget.SampleCount => _engine.SampleCount;

	/// <inheritdoc/>
	TextureUsage IWebGpuTextureTarget.Usage => _engine.Usage;

	/// <inheritdoc/>
	bool IWebGpuTextureTarget.IsInWebGpuPass => _engine.IsInWebGpuPass;

	/// <summary>Hands the OPEN frame's encoder to a library that drives the pass protocol itself; a null encoder outside a frame.</summary>
	/// <returns>The encoder.</returns>
	CommandEncoder IWebGpuTextureTarget.BeginWebGpuPass() => _engine.BeginWebGpuPass();

	/// <summary>Closes that pass; the commands are submitted when the frame ends.</summary>
	void IWebGpuTextureTarget.EndWebGpuPass() => _engine.EndWebGpuPass();

	#endregion

	#region | Engine host (frame thread) |

	void IWebGpuRenderEngineHost.OnDeviceReady()
	{
		_renderer?.OnDeviceReady(this);
		DeviceReady?.Invoke(this, new WebGpuDeviceReadyEventArgs(this, _engine.DeviceInfo!));
	}

	void IWebGpuRenderEngineHost.OnDeviceLost(string reason)
	{
		_renderer?.OnDeviceLost(reason);
		DeviceLost?.Invoke(this, new WebGpuDeviceLostEventArgs(reason));
	}

	void IWebGpuRenderEngineHost.OnRenderPathChanged(WebGpuRenderPath oldPath, WebGpuRenderPath newPath)
	{
		// ANDROID PORT (1): a direct path that failed (or a device that came up on Readback) drops the window surface.
		RequestWindowSurfaceHostUpdate();
		RenderPathChanged?.Invoke(this, new WebGpuRenderPathChangedEventArgs(oldPath, newPath));
	}

	void IWebGpuRenderEngineHost.Present(SKImage frame) => _surface.Present(frame);

	// ANDROID PORT (2): no PresentDirect (no desktop direct path); a DirectSurface frame is counted and the overlay above
	// the surface repainted when there is one.
	void IWebGpuRenderEngineHost.PresentSurfaceFrame() => _surface.PresentSurfaceFrame(_renderer is not null || Overlay is not null);

	void IWebGpuRenderEngineHost.ClearPresented() => _surface.ClearFrame();

	#endregion

	#region | Visual, lifetime and drivers (user-interface thread) |

	private protected override ContainerVisual CreateElementVisual()
	{
		if (ApiExtensibility.CreateInstance<SKCanvasVisualBaseFactory>(this, out var factory))
		{
			return _canvasVisual = factory.CreateInstance((o, size) => _surface.Paint((SKCanvas)o, size), Compositor.GetSharedCompositor());
		}

		throw new InvalidOperationException(
			$"Failed to create an instance of {nameof(SKCanvasVisualBase)} - the WebGPU render view requires a Skia composition target.");
	}

	internal override bool IsViewHit() => true;

	/// <summary>The paint surface (tests).</summary>
	internal WebGpuFrameSurface Surface => _surface;

	/// <summary>The frame protocol engine (tests).</summary>
	internal WebGpuRenderEngine Engine => _engine;

	/// <summary>The frame scheduler (tests).</summary>
	internal WebGpuFrameScheduler Scheduler => _scheduler;

	// UI thread. Entering a live tree: attach the engine (the device comes with the first frame, never here) and re-arm
	// the driver.
	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (XamlRoot is not { } xamlRoot)
		{
			return;
		}

		HookXamlRoot(xamlRoot);
		UpdatePixelSize();

		// The owned-device gate is resolved if a head registered one (the PlayTest head does; no Android head does today).
		var gate = WebGpuDeviceGate.Resolve(xamlRoot);
		_engine.Attach(gate); // ANDROID PORT (1): no head GPU device provider.
		UpdateWindowSurfaceHost(); // ANDROID PORT (1): a direct path that failed is tried again after a new Loaded.
		_scheduler.Attach();
	}

	// UI thread. Leaving the tree: no more frames; the device is released now (not at disposal), after DeviceLost.
	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		HookXamlRoot(null);
		_scheduler.Detach();
		_engine.Detach();
		_resizeSettleTimer.Stop();
		_surface.IsResizing = false;
	}

	private void HookXamlRoot(XamlRoot? xamlRoot)
	{
		if (_hookedXamlRoot is { } previous)
		{
			previous.Changed -= OnXamlRootChanged;
		}

		_hookedXamlRoot = xamlRoot;
		if (xamlRoot is not null)
		{
			xamlRoot.Changed += OnXamlRootChanged;
		}
	}

	private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdatePixelSize();

	private void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		// The first size after loading is not a drag: frames are presented live from the first one.
		if (WebGpuRenderViewRules.IsResize(e.PreviousSize.Width, e.PreviousSize.Height))
		{
			_surface.IsResizing = true;
			_resizeSettleTimer.Stop();
			_resizeSettleTimer.Start();
		}

		UpdatePixelSize();
		_canvasVisual?.Invalidate();
	}

	private void OnResizeSettled(object? sender, object e)
	{
		_resizeSettleTimer.Stop();
		_surface.IsResizing = false;
		_canvasVisual?.Invalidate();
	}

	private void UpdatePixelSize()
	{
		var scale = _hookedXamlRoot?.RasterizationScale ?? XamlRoot?.RasterizationScale ?? 1.0;
		PixelScale = scale;
		var (width, height) = WebGpuRenderViewRules.TargetPixelSize(ActualWidth, ActualHeight, scale);
		var (oldWidth, oldHeight) = _engine.PixelSize;
		if (width == oldWidth && height == oldHeight)
		{
			return;
		}

		_engine.SetPixelSize(width, height);
		_scheduler.NotifyResized();
	}

	// UI thread. One frame of a built-in driver: the renderer, then the event. A failure is logged (once per kind) and
	// the driver carries on; it never escapes into the dispatcher.
	private WebGpuFrameOutcome RunBuiltInFrame()
	{
		WebGpuFrameOutcome outcome;
		try
		{
			outcome = _engine.OpenFrame(wait: false);
		}
		catch (Exception e)
		{
			ReportBuiltInFrameFailure("A frame could not be opened", e);
			return WebGpuFrameOutcome.NotReady;
		}

		if (outcome != WebGpuFrameOutcome.Rendered)
		{
			return outcome;
		}

		try
		{
			_renderer?.OnRender(_engine.Frame);
			Render?.Invoke(this, _renderArgs);
		}
		catch (Exception e)
		{
			ReportBuiltInFrameFailure("A Render handler threw", e);
		}

		try
		{
			_engine.EndFrame();
		}
		catch (Exception e)
		{
			ReportBuiltInFrameFailure("A frame was rejected", e);
			return WebGpuFrameOutcome.NotReady;
		}

		return WebGpuFrameOutcome.Rendered;
	}

	private void ReportBuiltInFrameFailure(string what, Exception e)
	{
		// A renderer bug repeats every frame; the first one is the useful line, the rest go to Debug.
		var first = Interlocked.Increment(ref _builtInFrameFailures) == 1;
		var level = first ? LogLevel.Warning : LogLevel.Debug;
		if (this.Log().IsEnabled(level))
		{
			var message = $"{what}: {e.Message}";
			if (first)
			{
				this.Log().Warn(message, e);
			}
			else
			{
				this.Log().Debug(message);
			}
		}
	}

	// Any thread. A present asks for a repaint; off the user-interface thread it is marshalled there, coalesced to
	// one queued repaint (the canvas visual is the user-interface thread's).
	private void RequestRepaint()
	{
		if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
		{
			_canvasVisual?.Invalidate();
			return;
		}

		if (Interlocked.Exchange(ref _repaintPosted, 1) == 0 && !_dispatcherQueue.TryEnqueue(_repaintHandler))
		{
			Interlocked.Exchange(ref _repaintPosted, 0);
		}
	}

	private void RepaintOnUIThread()
	{
		Interlocked.Exchange(ref _repaintPosted, 0);
		_canvasVisual?.Invalidate();
	}

	// Render thread, inside the paint.
	private void DrawOverlay(SKCanvas canvas, SKSize size)
	{
		var renderer = _renderer;
		var overlay = Overlay;
		if (renderer is null && overlay is null)
		{
			return;
		}

		try
		{
			renderer?.OnOverlay(canvas, size);
			if (overlay is not null)
			{
				_overlayArgs.Canvas = canvas;
				_overlayArgs.Size = size;
				overlay(this, _overlayArgs);
			}
		}
		catch (Exception e)
		{
			ReportBuiltInFrameFailure("An Overlay handler threw", e);
		}
	}

	private void OnTargetFrameRateChanged(double oldValue, double newValue)
	{
		if (_revertingTargetFrameRate)
		{
			return;
		}

		if (!WebGpuRenderViewRules.IsValidTargetFrameRate(newValue))
		{
			_revertingTargetFrameRate = true;
			try
			{
				TargetFrameRate = oldValue;
			}
			finally
			{
				_revertingTargetFrameRate = false;
			}

			throw new ArgumentOutOfRangeException(nameof(TargetFrameRate), newValue,
				"TargetFrameRate must be zero (as fast as possible) or a finite positive number of frames per second.");
		}

		_scheduler.TargetFrameRate = newValue;
	}

	private void OnStretchChanged(Stretch stretch)
	{
		_surface.Stretch = stretch;
		_canvasVisual?.Invalidate();
	}

	private void OnDeviceOptionsChanged(WebGpuSkiaDeviceOptions? options)
	{
		_engine.DeviceOptions = options;
		_scheduler.Invalidate();
	}

	private void OnRequireOwnedDeviceChanged(bool value)
	{
		_engine.RequireOwnedDevice = value;
		UpdateWindowSurfaceHost(); // ANDROID PORT (1)
		_scheduler.Invalidate();
	}

	#endregion

	#region | ANDROID PORT (1): the DirectSurface path's window surface host |

	/// <summary>
	/// The platform view that can host a window surface under the element (set and cleared by the Android handler on
	/// the user-interface thread; null with the plain canvas handler).
	/// </summary>
	internal IWebGpuWindowSurfaceHost? WindowSurfaceHost
	{
		get => _windowSurfaceHost;
		set
		{
			_windowSurfaceHost = value;
			UpdateWindowSurfaceHost();
		}
	}

	/// <summary>Android created the host's window surface: the engine takes the window (and its reference). UI thread.</summary>
	/// <param name="handle">The acquired native window.</param>
	/// <param name="width">Its width in pixels.</param>
	/// <param name="height">Its height in pixels.</param>
	/// <param name="release">Gives the reference back once the WebGPU surface over it is released.</param>
	internal void OnWindowCreated(nint handle, int width, int height, Action<nint> release)
	{
		_engine.AttachWindow(handle, width, height, release);
		_scheduler.Invalidate();
	}

	/// <summary>Android changed the host's window surface. UI thread.</summary>
	/// <param name="width">Its width in pixels.</param>
	/// <param name="height">Its height in pixels.</param>
	internal void OnWindowChanged(int width, int height)
	{
		_engine.ResizeWindow(width, height);
		_scheduler.Invalidate();
	}

	/// <summary>Android is destroying the host's window surface: released before this returns (or by the frame holding it). UI thread.</summary>
	internal void OnWindowDestroyed() => _engine.DetachWindow();

	/// <summary>The host shows a window surface now (it will report its window) or no longer does. UI thread.</summary>
	/// <param name="connected">True when the host's SurfaceView is in place.</param>
	internal void OnWindowHostConnected(bool connected)
	{
		if (connected)
		{
			_engine.ConnectWindowHost();
		}
		else
		{
			_engine.DisconnectWindowHost();
		}
	}

	// UI thread. The host keeps a window surface exactly while the engine may take the direct path.
	private void UpdateWindowSurfaceHost() => _windowSurfaceHost?.HostWindowSurface(_engine.WantsWindowSurface);

	// Any thread (a render-path change happens on the frame's thread).
	private void RequestWindowSurfaceHostUpdate()
	{
		if (_windowSurfaceHost is null)
		{
			return;
		}

		if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
		{
			UpdateWindowSurfaceHost();
		}
		else
		{
			_dispatcherQueue.TryEnqueue(UpdateWindowSurfaceHost);
		}
	}

	private void OnClearColorChanged(WinColor color)
	{
		_engine.ClearColor = new SKColor(color.R, color.G, color.B, color.A);
		_scheduler.Invalidate();
	}

	#endregion
}
