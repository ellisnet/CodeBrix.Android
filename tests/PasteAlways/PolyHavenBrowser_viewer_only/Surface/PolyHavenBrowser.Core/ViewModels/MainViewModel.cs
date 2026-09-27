using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using PolyHavenBrowser.Display;
using PolyHavenBrowser.Rendering;
using PolyHavenBrowser.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace PolyHavenBrowser.ViewModels;

/// <summary>
/// Lets the hosting page hand the view model a way to invalidate (repaint) the Skia canvas.
/// </summary>
public interface ICanvasInvalidator
{
    /// <summary>
    /// Invalidates the hosting page's canvas (null before the page wires it up). This is the
    /// raw "repaint the control" call; <see cref="MainViewModel.RequestRender"/> is the
    /// coalescing gate in front of it.
    /// </summary>
    Action InvalidateCanvas { get; set; }
}

/// <summary>
/// Drives the PolyHavenBrowser main page: three sample buttons (texture, HDRI, model) that
/// download a representative Poly Haven asset on demand and display it on the shared Skia
/// canvas through an <see cref="IScenePainter"/>. It also owns the canvas repaint policy -
/// paint coalescing and backlogged-pointer-frame detection - so the page's canvas handlers
/// stay short forwards.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class MainViewModel : SimpleViewModel, ICanvasInvalidator
{
    //A pointer frame that is running more than this far behind real time is a backlog
    //frame: keep the cursor anchor in sync but skip rendering it, catching up to the latest.
    private const double StaleFrameMicroseconds = 1_000_000; // 1 second

    private readonly SampleAssetService _assets;
    private readonly IModelRenderEngineSelector _engineSelector;

    //Cancels an in-flight download when the view model is disposed, so shutdown does not
    //wait for the network. Nothing else cancels: the sample buttons are disabled while busy.
    private readonly CancellationTokenSource _lifetime = new();

    //Tracks how far behind real time the pointer stream is, to detect a backlog.
    private readonly Stopwatch _gestureClock = new();
    private double _gestureStartTimestamp;

    //Coalescing: never queue more than one paint. While one is pending, pointer moves only
    //update the camera; the next paint draws the latest state.
    private bool _renderPending;

    private ModelScenePainter _modelPainter;

    private IScenePainter _currentPainter;
    private PanoramaScenePainter _panoramaPainter;
    private SampleAssetKind _selectedKind = SampleAssetKind.Texture;
    private RenderEngineKind _currentEngineKind = RenderEngineKind.OpenGL;
    private RenderEngineKind _selectedRenderEngine = RenderEngineKind.OpenGL;

    private SimpleCommand _selectTextureCommand;
    private SimpleCommand _selectHdriCommand;
    private SimpleCommand _selectModelCommand;

    /// <summary>Creates the view model and begins loading the initial texture sample.</summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; }

        _assets = GetService<SampleAssetService>();

        //The engine selector owns the available 3D backends (OpenGL + Vulkan) and creates
        //them on demand; the app always starts on OpenGL (no persistence).
        _engineSelector = GetService<IModelRenderEngineSelector>();

        _modelPainter = new ModelScenePainter(_engineSelector.Create(RenderEngineKind.OpenGL, GetXamlRoot));

        Initialization = SelectAsync(SampleAssetKind.Texture);
    }

    /// <summary>
    /// The initial sample load the constructor starts, so a page or a test can await the first
    /// display instead of racing it. It never faults: the load catches every failure and reports
    /// it through <see cref="StatusText"/>.
    /// </summary>
    public Task Initialization { get; } = Task.CompletedTask;

    /// <inheritdoc />
    public Action InvalidateCanvas { get; set; }

    /// <summary>The painter the hosting canvas should draw with.</summary>
    public IScenePainter CurrentPainter
    {
        get => _currentPainter;
        private set => SetProperty(ref _currentPainter, value);
    }

    /// <summary>Whether an asset is currently being downloaded or loaded.</summary>
    [AffectsCommands(nameof(SelectTextureCommand), nameof(SelectHdriCommand), nameof(SelectModelCommand))]
    [AffectsProperties(nameof(IsNotBusy), nameof(BusyVisibility))]
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The inverse of <see cref="IsBusy"/> (disables the engine dropdown while loading).</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>The busy indicator's visibility (visible while an asset is downloading/loading).</summary>
    public Visibility BusyVisibility => GetVisibility(IsBusy);

    /// <summary>A short status line shown beneath the canvas.</summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = "Starting up…";

    /// <summary>The rendering engines shown in the dropdown (OpenGL first - the default).</summary>
    public IReadOnlyList<RenderEngineKind> RenderEngineKinds =>
        _engineSelector?.AvailableKinds ?? Array.Empty<RenderEngineKind>();

    /// <summary>
    /// The rendering engine picked in the dropdown. Selecting an engine that is not supported
    /// on this platform shows an alert and snaps the selection back; selecting a supported one
    /// swaps the 3D engine and re-displays the current texture/model sample through it.
    /// </summary>
    public RenderEngineKind SelectedRenderEngine
    {
        get => _selectedRenderEngine;
        set
        {
            if (value == _selectedRenderEngine) { return; }

            //Optimistic: show the new selection at once; SwitchEngineAsync reverts it if the
            //engine is unsupported or fails to initialize.
            SetEnumProperty(ref _selectedRenderEngine, value);
            _ = RunSwitchEngineAsync(value);
        }
    }

    /// <summary>
    /// The engine dropdown's visibility: shown in Texture and Model modes, hidden in HDRI mode
    /// (the HDRI panorama is CPU-rendered and unaffected by the engine choice).
    /// </summary>
    public Visibility EngineSelectorVisibility => GetVisibility(_selectedKind != SampleAssetKind.Hdri);

    /// <summary>Whether the texture sample is selected (drives the button highlight).</summary>
    public bool IsTextureSelected => _selectedKind == SampleAssetKind.Texture;

    /// <summary>Whether the HDRI sample is selected.</summary>
    public bool IsHdriSelected => _selectedKind == SampleAssetKind.Hdri;

    /// <summary>Whether the model sample is selected.</summary>
    public bool IsModelSelected => _selectedKind == SampleAssetKind.Model;

    /// <summary>Selects and shows the sample texture (on a lit cube).</summary>
    public SimpleCommand SelectTextureCommand =>
        _selectTextureCommand ??= new SimpleCommand(() => !IsBusy, () => SelectAsync(SampleAssetKind.Texture));

    /// <summary>Selects and shows the sample HDRI panorama.</summary>
    public SimpleCommand SelectHdriCommand =>
        _selectHdriCommand ??= new SimpleCommand(() => !IsBusy, () => SelectAsync(SampleAssetKind.Hdri));

    /// <summary>Selects and shows the sample 3D model.</summary>
    public SimpleCommand SelectModelCommand =>
        _selectModelCommand ??= new SimpleCommand(() => !IsBusy, () => SelectAsync(SampleAssetKind.Model));

    #region | Canvas painting and pointer input |

    /// <summary>
    /// Paints the current scene into the canvas surface; the hosting page calls this from its
    /// canvas <c>PaintSurface</c> handler. The coalescing flag is cleared first, so a repaint
    /// requested while this paint runs still queues the next frame.
    /// </summary>
    /// <param name="surface">The canvas surface to paint into.</param>
    /// <param name="info">The surface's pixel size and format.</param>
    public void PaintCanvas(SKSurface surface, SKImageInfo info)
    {
        _renderPending = false;
        _currentPainter?.Paint(surface, info);
    }

    /// <summary>
    /// Requests a repaint, keeping at most one queued: while a paint is pending, pointer moves
    /// only update the camera and the next paint draws the latest state.
    /// </summary>
    public void RequestRender()
    {
        if (_renderPending) { return; }
        _renderPending = true;
        InvalidateCanvas?.Invoke();
    }

    /// <summary>
    /// Begins a drag at the given canvas position and starts the gesture clock that backlog
    /// detection measures against.
    /// </summary>
    /// <param name="x">The pointer's X position in canvas pixels.</param>
    /// <param name="y">The pointer's Y position in canvas pixels.</param>
    /// <param name="timestamp">The pointer event's own timestamp, in microseconds.</param>
    /// <returns><see langword="true"/> when a painter took the press, otherwise false.</returns>
    public bool PointerPressed(double x, double y, ulong timestamp)
    {
        var painter = _currentPainter;
        if (painter == null) { return false; }

        painter.PointerDown(x, y);
        _gestureStartTimestamp = timestamp;
        _gestureClock.Restart();
        RequestRender();
        return true;
    }

    /// <summary>
    /// Continues a drag to the given canvas position. A frame that has fallen far enough behind
    /// real time is discarded through <see cref="IScenePainter.PointerSkip"/>, which advances
    /// the drag anchor without moving the camera, so dropping a frame keeps the camera in sync
    /// with the cursor instead of making the scene jump.
    /// </summary>
    /// <param name="x">The pointer's X position in canvas pixels.</param>
    /// <param name="y">The pointer's Y position in canvas pixels.</param>
    /// <param name="timestamp">The pointer event's own timestamp, in microseconds.</param>
    /// <returns><see langword="true"/> when a painter took the move, otherwise false.</returns>
    public bool PointerMoved(double x, double y, ulong timestamp)
    {
        var painter = _currentPainter;
        if (painter == null) { return false; }

        if (IsBacklogFrame(timestamp))
        {
            //Discard this stale frame: stay aligned with the cursor but don't render it.
            painter.PointerSkip(x, y);
        }
        else
        {
            painter.PointerDrag(x, y);
            RequestRender();
        }

        return true;
    }

    /// <summary>
    /// Ends the current drag, from either a pointer release or a lost pointer capture, and
    /// redraws once at full (non-drag) resolution.
    /// </summary>
    public void PointerReleased()
    {
        _currentPainter?.PointerUp();
        _gestureClock.Reset();
        RequestRender();
    }

    /// <summary>Zooms by a mouse-wheel delta (positive zooms in / narrows).</summary>
    /// <param name="wheelDelta">The wheel delta the pointer event reported.</param>
    /// <returns><see langword="true"/> when a painter took the zoom, otherwise false.</returns>
    public bool PointerWheelChanged(double wheelDelta)
    {
        var painter = _currentPainter;
        if (painter == null) { return false; }

        painter.Zoom(wheelDelta);
        RequestRender();
        return true;
    }

    //True when this pointer frame is running far enough behind real time to be a backlog
    //frame that should be dropped rather than rendered.
    private bool IsBacklogFrame(ulong timestamp)
    {
        if (!_gestureClock.IsRunning) { return false; }
        var inputElapsed = timestamp - _gestureStartTimestamp;
        var lag = _gestureClock.Elapsed.TotalMicroseconds - inputElapsed;
        return lag > StaleFrameMicroseconds;
    }

    #endregion

    //Starts an engine switch from the bound setter. The task is discarded, so this wrapper is
    //what guarantees a failure becomes status text rather than an unobserved exception.
    private async Task RunSwitchEngineAsync(RenderEngineKind kind)
    {
        try
        {
            await SwitchEngineAsync(kind);
        }
        catch (Exception ex)
        {
            StatusText = $"Could not switch to {kind} rendering: {ex.Message}";
            RevertEngineSelection();
        }
    }

    //Switches the 3D engine behind the model painter: alert + snap back when the engine is
    //not okayed for this platform, otherwise swap painters and re-display the current sample.
    private async Task SwitchEngineAsync(RenderEngineKind kind)
    {
        if (kind == _currentEngineKind) { return; }

        if (IsBusy)
        {
            //The dropdown is disabled while busy; this is just a belt-and-braces revert.
            RevertEngineSelection();
            return;
        }

        if (!_engineSelector.IsSupported(kind))
        {
            //The unsupported engine differs by platform: Vulkan is excluded on macOS, Metal is
            //excluded everywhere except macOS - so name whichever one was picked.
            using (var alert = CreateDialog(
                $"{kind} rendering is not available on this platform.", $"{kind} Rendering"))
            {
                _ = await alert.ShowAsync();
            }
            RevertEngineSelection();
            return;
        }

        IsBusy = true;
        try
        {
            var engine = _engineSelector.Create(kind, GetXamlRoot);
            if (kind is RenderEngineKind.Vulkan or RenderEngineKind.Metal)
            {
                //Fail fast off the UI thread (a supported platform can still lack a working
                //driver) so a failure never surfaces inside the Skia paint callback. Safe for the
                //own-stack engines (Vulkan, Metal): they have no thread-affinity, unlike the
                //OpenGL engine's native GL context, which must be created on the render thread at
                //first paint.
                await Task.Run(() => engine.RenderFrame(1, 1, (0f, 0f, 0f, 1f)));
            }

            var oldPainter = _modelPainter;
            _modelPainter = new ModelScenePainter(engine);
            _currentEngineKind = kind;
            if (ReferenceEquals(CurrentPainter, oldPainter))
            {
                CurrentPainter = null;
            }
            oldPainter?.Dispose();
        }
        catch (Exception ex)
        {
            StatusText = $"Could not switch to {kind} rendering: {ex.Message}";
            RevertEngineSelection();
            return;
        }
        finally
        {
            IsBusy = false;
        }

        //Re-display the current sample through the new engine (from the local cache, so no
        //network). The dropdown is hidden in HDRI mode, so this is always Texture or Model.
        if (_selectedKind != SampleAssetKind.Hdri)
        {
            await SelectAsync(_selectedKind);
        }
        else
        {
            RequestRender();
        }
    }

    private void RevertEngineSelection()
    {
        _selectedRenderEngine = _currentEngineKind;
        NotifyPropertyChanged(nameof(SelectedRenderEngine));
    }

    private async Task SelectAsync(SampleAssetKind kind)
    {
        if (IsBusy) { return; }

        _selectedKind = kind;
        RaiseSelectionChanged();
        IsBusy = true;

        try
        {
            var progress = new Progress<string>(message => StatusText = message);
            var asset = await _assets.EnsureSampleAsync(kind, progress, _lifetime.Token);

            //Decode off the UI thread; the painters upload to GL lazily during Paint.
            var decoded = await Task.Run(() => DecodeSample(kind, asset), _lifetime.Token);

            //Hand the decoded content to a painter back on the UI thread: the painters, their
            //cameras and the bound status line are only ever touched there.
            InvokeOnMainThread(() =>
            {
                CurrentPainter = ApplyDecodedSample(kind, decoded);
                StatusText = $"{Label(kind)}: {asset.Name}    ·    {Hint(kind)}";
            });
        }
        catch (OperationCanceledException)
        {
            //The view model is shutting down; leave the status line as it is.
        }
        catch (Exception ex)
        {
            StatusText = $"Could not load the {kind.ToString().ToLowerInvariant()} sample: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RequestRender();
        }
    }

    //Runs on a worker thread: decodes the downloaded file and builds the mesh. It touches no
    //painter, no camera and no bound state, so nothing here needs the UI thread.
    private static DecodedSample DecodeSample(SampleAssetKind kind, SampleAsset asset)
    {
        switch (kind)
        {
            case SampleAssetKind.Texture:
                //The decoded bitmap feeds the cube's texture and doubles as the darkened
                //  backdrop; the painter takes ownership of it for the background.
                var textureBitmap = TextureImageLoader.LoadForDisplay(asset.PrimaryFilePath);
                return new DecodedSample
                {
                    TextureBitmap = textureBitmap,
                    Model = CubeMeshBuilder.Build(textureBitmap, asset.Name),
                };

            case SampleAssetKind.Model:
                return new DecodedSample { Model = new GltfModelLoader().LoadFile(asset.PrimaryFilePath) };

            case SampleAssetKind.Hdri:
                var bytes = File.ReadAllBytes(asset.PrimaryFilePath);
                return new DecodedSample
                {
                    Panorama = TextureImageLoader.LoadFloatImage(
                        bytes, Path.GetExtension(asset.PrimaryFilePath)),
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    //Runs on the UI thread: hands the decoded content to a painter and frames the camera.
    private IScenePainter ApplyDecodedSample(SampleAssetKind kind, DecodedSample decoded)
    {
        switch (kind)
        {
            case SampleAssetKind.Texture:
                _modelPainter.SetModel(decoded.Model);
                _modelPainter.SetBackgroundTexture(decoded.TextureBitmap);
                //A fixed key light (from upper-front-left) shades the faces distinctly so the
                //  cube reads as solid, and a 3/4 angle with perspective shows three faces.
                //  Extra framing margin keeps the whole cube (and its rotating silhouette)
                //  in view against the backdrop, which reads as clearly 3D.
                _modelPainter.FixedLightDirection = new Vector3(-0.4f, 1f, 0.7f);
                _modelPainter.Camera.FovDegrees = 40f;
                _modelPainter.Camera.YawDegrees = 35f;
                _modelPainter.Camera.PitchDegrees = 28f;
                _modelPainter.Camera.FitMargin = 1.2f;
                _modelPainter.Camera.VerticalFramingBias = 0f;
                return _modelPainter;

            case SampleAssetKind.Model:
                _modelPainter.SetModel(decoded.Model);
                _modelPainter.SetBackgroundTexture(null);
                _modelPainter.FixedLightDirection = null;
                _modelPainter.Camera.FovDegrees = 45f;
                _modelPainter.Camera.YawDegrees = 30f;
                _modelPainter.Camera.PitchDegrees = 15f;
                _modelPainter.Camera.FitMargin = 0.73f;         // ~1.5x closer than before
                _modelPainter.Camera.VerticalFramingBias = 0.22f; // sit the model lower in view
                return _modelPainter;

            case SampleAssetKind.Hdri:
                var newPainter = new PanoramaScenePainter(decoded.Panorama);
                var old = _panoramaPainter;
                _panoramaPainter = newPainter;
                old?.Dispose();
                return newPainter;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private void RaiseSelectionChanged()
    {
        NotifyPropertyChanged(nameof(IsTextureSelected));
        NotifyPropertyChanged(nameof(IsHdriSelected));
        NotifyPropertyChanged(nameof(IsModelSelected));
        NotifyPropertyChanged(nameof(EngineSelectorVisibility));
    }

    private static string Label(SampleAssetKind kind) => kind switch
    {
        SampleAssetKind.Texture => "Texture",
        SampleAssetKind.Hdri => "HDRI",
        SampleAssetKind.Model => "Model",
        _ => kind.ToString(),
    };

    private static string Hint(SampleAssetKind kind) => kind switch
    {
        SampleAssetKind.Hdri => "drag to look around · scroll to zoom",
        _ => "drag to rotate · scroll to zoom",
    };

    //What one sample decodes to, before any of it reaches a painter: whichever of these the
    //sample kind produces.
    private sealed class DecodedSample
    {
        public SKBitmap TextureBitmap { get; init; }

        public LoadedModel Model { get; init; }

        public FloatImage Panorama { get; init; }
    }

    #region | IDisposable implementation |

    /// <summary>
    /// Releases what this view model created: the painters (each of which disposes its
    /// rendering engine), the commands, the canvas bridge delegate the page handed over, and
    /// the cancellation source that stops an in-flight download. The services resolved from
    /// the container are singletons, so they are released rather than disposed.
    /// </summary>
    public override void Dispose()
    {
        //Stop an in-flight download so shutdown does not wait for the network.
        _lifetime.Cancel();

        _selectTextureCommand?.Dispose();
        _selectTextureCommand = null;
        _selectHdriCommand?.Dispose();
        _selectHdriCommand = null;
        _selectModelCommand?.Dispose();
        _selectModelCommand = null;

        //The delegate captures the page, so clearing it is what releases the page.
        InvalidateCanvas = null;

        //Null each painter before disposing it, so a paint arriving mid-teardown sees null
        //rather than a disposed painter.
        _currentPainter = null;

        var modelPainter = _modelPainter;
        _modelPainter = null;
        modelPainter?.Dispose();

        var panoramaPainter = _panoramaPainter;
        _panoramaPainter = null;
        panoramaPainter?.Dispose();

        _lifetime.Dispose();

        base.Dispose();
    }

    #endregion
}
