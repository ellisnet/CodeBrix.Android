using CodeBrix.Platform.Simple;
using PalmVisualizer.Camera;
using PalmVisualizer.Rendering;
using PalmVisualizer.Vision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PalmVisualizer.ViewModels;

/// <summary>
/// Lets the hosting page hand the view model the invalidate (repaint) delegate for the
/// live-preview canvas. Frames arrive on the capture thread; the page's delegate is
/// responsible for marshalling its invalidate onto the UI thread.
/// </summary>
public interface ICanvasBridge
{
    /// <summary>Invalidates the live-preview canvas shown in Camera Mode.</summary>
    Action InvalidatePreviewCanvas { get; set; }
}

/// <summary>
/// Lets the hosting page tell the view model when the visualizer's game canvas has its
/// first real layout size - the engine can only start against a non-zero surface, which
/// happens the first time Visualize Mode is shown.
/// </summary>
public interface IManageGameCanvas
{
    /// <summary>Called once, on the UI thread, at the canvas's FirstStarted event.</summary>
    /// <param name="host">The page that owns the game canvas the visualizer renders into.</param>
    void CanvasFirstStart(IGameCanvasHost host);
}

[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, ICanvasBridge, IManageGameCanvas, IWebcamFrameSource
{
    private readonly IVisualizerSessionFactory _sessionFactory;

    private IWebcamCaptureService _captureService;
    private IPalmTracker _tracker;
    private IVisualizerSession _visualizerSession;

    private byte[] _visionFrame;
    private int _reportedOpenPalmCount;

    private int _cameraSwitchVersion;

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        Debug.WriteLine("PalmVisualizer view model startup.");

        //The three collaborators come from the container App registered them with, so this
        //  view model can also be built against stand-ins that need no camera and no GPU
        _captureService = GetService<IWebcamCaptureService>() ?? new WebcamCaptureService();
        _tracker = GetService<IPalmTracker>() ?? new PalmTracker();
        _sessionFactory = GetService<IVisualizerSessionFactory>() ?? new VisualizerSessionFactory();

        _captureService.FrameArrived += OnFrameArrived;
        _tracker.TrackingUpdated += OnTrackingUpdated;

        StatusText = "Discovering cameras…";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var cameras = await _captureService.DiscoverCamerasAsync();
            InvokeOnMainThread(() =>
            {
                Cameras.Clear();
                foreach (var camera in cameras)
                {
                    Cameras.Add(camera);
                }
                if (Cameras.Count == 0)
                {
                    StatusText = "No cameras were found on this machine.";
                }
                else
                {
                    StatusText = $"Found {Cameras.Count} camera(s).";
                    SelectedCamera = Cameras[0]; //auto-start on the first camera
                }
            });
        }
        catch (Exception e)
        {
            InvokeOnMainThread(() => StatusText = $"Camera discovery failed: {e.Message}");
        }
    }

    #region | Live frames and palm tracking |

    private void OnFrameArrived(object sender, EventArgs e)
    {
        //Capture-thread context: get out fast
        if (!HasFrame)
        {
            InvokeOnMainThread(() => HasFrame = _captureService.HasFrame);
        }

        if (IsCameraMode)
        {
            InvalidatePreviewCanvas?.Invoke();
        }
        else
        {
            //Visualize Mode: the live feed drives the palm tracker
            var tracker = _tracker;
            if (tracker is { IsRunning: true }
                && _captureService.TryCopyLatestFrame(ref _visionFrame, out var width, out var height))
            {
                tracker.SubmitFrame(_visionFrame, width, height);
            }
        }
    }

    private void OnTrackingUpdated(object sender, PalmTrackingEventArgs e)
    {
        //Worker-thread context: the visualizer's attractor field is thread-safe, so the
        //  palms feed straight in - only the status line needs the UI thread
        var session = _visualizerSession;
        if (IsCameraMode || session == null) { return; }

        var attractors = new List<PalmAttractor>(e.Result.Palms.Count);
        foreach (var palm in e.Result.Palms)
        {
            //Only OPEN palms attract the colors - and the user watched a mirrored
            //  preview, so mirror the palm positions to match
            if (palm.IsOpenPalm)
            {
                attractors.Add(new PalmAttractor(palm.TrackId, 1f - palm.PalmCenterX, palm.PalmCenterY));
            }
        }
        session.UpdatePalms(attractors);

        var openCount = attractors.Count;
        if (openCount != _reportedOpenPalmCount)
        {
            _reportedOpenPalmCount = openCount;
            InvokeOnMainThread(() => StatusText = openCount switch
            {
                0 => "Show the camera your open palm - the colors will gather toward it.",
                1 => "The colors are chasing your open palm - close your hand to set them free.",
                _ => $"The colors are chasing {openCount} open palms - close your hands to set them free.",
            });
        }
    }

    #endregion

    #region | IWebcamFrameSource implementation |

    /// <summary>
    /// Hands the page's preview renderer the newest camera frame. Implemented explicitly:
    /// the page pulls frames through the narrow <see cref="IWebcamFrameSource"/> seam and
    /// never sees the capture service behind it.
    /// </summary>
    /// <param name="buffer">The renderer's frame buffer; replaced when the size does not match.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns><c>true</c> when a frame was copied.</returns>
    bool IWebcamFrameSource.TryCopyLatestFrame(ref byte[] buffer, out int width, out int height)
    {
        var service = _captureService;
        if (service == null)
        {
            width = 0;
            height = 0;
            return false;
        }
        return service.TryCopyLatestFrame(ref buffer, out width, out height);
    }

    #endregion

    #region | Bindable properties |

    /// <summary>The connected cameras shown in the dropdown.</summary>
    public ObservableCollection<CameraDevice> Cameras { get; } = new();

    public CameraDevice SelectedCamera
    {
        get;
        set
        {
            if (field != value)
            {
                SetProperty(ref field, value);
                SwitchCamera(value);
            }
        }
    }

    [AffectsCommands(nameof(VisualizeCommand), nameof(BackCommand))]
    public bool IsCameraMode
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsVisualizeMode));
        }
    } = true;

    /// <summary>Visualize Mode is simply not-Camera Mode.</summary>
    public bool IsVisualizeMode => !IsCameraMode;

    [AffectsCommands(nameof(VisualizeCommand))]
    public bool HasFrame
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>Set by the hosting page (see <see cref="ICanvasBridge"/>).</summary>
    public Action InvalidatePreviewCanvas { get; set; }

    #endregion

    private void SwitchCamera(CameraDevice camera)
    {
        //The setter only kicks the switch off: opening a device can take long enough to
        //  stall the UI thread, so the switch itself runs on a worker and the status line
        //  carries the result
        HasFrame = false;
        _ = SwitchCameraAsync(camera, ++_cameraSwitchVersion);
    }

    /// <summary>
    /// Starts (or stops) live capture off the UI thread - the capture service serializes its
    /// own start and stop, so two devices are never opened at once - and lets only the newest
    /// switch report: a camera that finishes opening late must not overwrite the status of
    /// the one the user has since chosen.
    /// </summary>
    private async Task SwitchCameraAsync(CameraDevice camera, int version)
    {
        try
        {
            var service = _captureService;
            if (service == null) { return; }

            await Task.Run(() =>
            {
                if (camera == null)
                {
                    service.Stop();
                }
                else
                {
                    service.Start(camera);
                }
            }).ConfigureAwait(false);

            if (version != _cameraSwitchVersion) { return; } //a newer switch took over

            if (camera == null)
            {
                //The page's delegate marshals the repaint itself
                InvalidatePreviewCanvas?.Invoke();
                return;
            }
            InvokeOnMainThread(() => StatusText = $"Live: {camera.FriendlyName}");
        }
        catch (Exception e)
        {
            if (version == _cameraSwitchVersion)
            {
                InvokeOnMainThread(() =>
                    StatusText = $"Could not start '{camera?.FriendlyName}': {e.Message}");
            }
        }
    }

    #region | IManageGameCanvas implementation |

    public void CanvasFirstStart(IGameCanvasHost host)
    {
        //UI thread, the first time Visualize Mode is shown with a real size: build the
        //  shader scene and start the engine. Later mode switches pause and resume it.
        _visualizerSession = _sessionFactory.CreateSession(host);
        _visualizerSession.Start();
    }

    #endregion

    #region | Commands and their implementations |

    #region VisualizeCommand

    private SimpleCommand _visualizeCommand;
    public SimpleCommand VisualizeCommand =>
        (_visualizeCommand ??= new SimpleCommand(CanVisualize, DoVisualize));

    private bool CanVisualize() => IsCameraMode && HasFrame;

    private Task DoVisualize()
    {
        if (!CanVisualize()) { return Task.CompletedTask; }

        //Starting the tracker is what loads the models, on its own worker thread
        _tracker.Start();
        _reportedOpenPalmCount = 0;

        //Showing the game canvas gives it its first real layout size, which raises its
        //  FirstStarted -> CanvasFirstStart the first time through; on later entries the
        //  engine is merely paused from Camera Mode, so wake it back up
        IsCameraMode = false;
        _visualizerSession?.Resume();

        StatusText = "Show the camera your open palm - the colors will gather toward it.";
        return Task.CompletedTask;
    }

    #endregion

    #region BackCommand

    private SimpleCommand _backCommand;
    public SimpleCommand BackCommand =>
        (_backCommand ??= new SimpleCommand(CanGoBack, DoGoBack));

    private bool CanGoBack() => IsVisualizeMode;

    private Task DoGoBack()
    {
        if (!CanGoBack()) { return Task.CompletedTask; }

        _tracker?.Stop();
        _visualizerSession?.Pause();

        IsCameraMode = true;
        InvalidatePreviewCanvas?.Invoke();
        StatusText = SelectedCamera != null
            ? $"Live: {SelectedCamera.FriendlyName}"
            : "Select a camera.";
        return Task.CompletedTask;
    }

    #endregion

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        _visualizeCommand?.Dispose();
        _visualizeCommand = null;
        _backCommand?.Dispose();
        _backCommand = null;

        InvalidatePreviewCanvas = null;

        if (_tracker != null)
        {
            _tracker.TrackingUpdated -= OnTrackingUpdated;
            _tracker.Dispose();
            _tracker = null;
        }

        var session = _visualizerSession;
        _visualizerSession = null;
        session?.Stop();

        if (_captureService != null)
        {
            _captureService.FrameArrived -= OnFrameArrived;
            _captureService.Dispose();
            _captureService = null;
        }

        base.Dispose();
    }

    #endregion
}
