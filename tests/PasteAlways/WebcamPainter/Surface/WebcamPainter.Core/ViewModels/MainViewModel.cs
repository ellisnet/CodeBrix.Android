using CodeBrix.Platform.Simple;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WebcamPainter.Bridges;
using WebcamPainter.Painting;
using WebcamPainter.Vision;
using WebcamPainter.Webcam;

namespace WebcamPainter.ViewModels;

#if HAS_CODEBRIX
[Microsoft.UI.Xaml.Data.Bindable]
#endif
public class MainViewModel : SimpleViewModel, IFileSaveBridge, ICanvasBridge
{
    private WebcamCaptureService _captureService;
    private HandTracker _tracker;
    private PaintingSession _paintSession;

    private byte[] _visionFrame;

    //The main canvas's live-video renderer. It belongs here rather than in the page because the
    //  view model is what decides whether that canvas shows video at all; its cached buffers are
    //  still only ever touched from the paint handler, which runs on the UI thread.
    private readonly WebcamFrameRenderer _mainRenderer = new WebcamFrameRenderer();

    public MainViewModel()
    {
        if (!IsDesignMode(true))
        {
            Debug.WriteLine("WebcamPainter view model startup.");

            _captureService = new WebcamCaptureService();
            _captureService.FrameArrived += OnFrameArrived;

            StatusText = "Discovering cameras…";
            _ = InitializeAsync();
        }

        HighlighterColors = BuildHighlighterColors();
    }

    //One button item per palette entry, each carrying this view model's own select command
    private IReadOnlyList<HighlighterColorViewModel> BuildHighlighterColors()
    {
        var colors = new List<HighlighterColorViewModel>(HighlighterPalette.Colors.Count);
        foreach (var color in HighlighterPalette.Colors)
        {
            colors.Add(new HighlighterColorViewModel(color, SelectColorCommand));
        }

        return colors;
    }

    private async Task InitializeAsync()
    {
        try
        {
            var cameras = await WebcamCaptureService.GetCamerasAsync();
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

    /// <summary>The capture service - the page's self-view canvas pulls live frames from it.</summary>
    public WebcamCaptureService CaptureService => _captureService;

    #region | What the main canvas draws |

    /// <summary>
    /// Draws the main canvas: the mirrored live preview in Capture Mode, the painting and its
    /// hand crosshair in Paint Mode. The mode decision lives here, so the page's paint handler
    /// is a single forward (see <see cref="ICanvasBridge"/>) and stays out of application state.
    /// </summary>
    /// <param name="surface">The Skia surface to render onto.</param>
    /// <param name="info">The image info describing the surface.</param>
    public void RenderMainCanvas(SKSurface surface, SKImageInfo info)
    {
        var session = _paintSession;
        if (IsPaintMode && session != null)
        {
            PaintCanvasHelper.Render(surface, info, session,
                CrosshairNormX, CrosshairNormY, IsBrushPainting);
        }
        else
        {
            _mainRenderer.Render(surface, info, _captureService, mirror: true);
        }
    }

    /// <summary>The hand's horizontal position over the still, 0..1; null when no hand is tracked.
    /// Read by <see cref="RenderMainCanvas"/> at frame rate, so it is state rather than a binding.</summary>
    private float? CrosshairNormX { get; set; }

    /// <summary>The hand's vertical position over the still, 0..1; null when no hand is tracked.</summary>
    private float? CrosshairNormY { get; set; }

    /// <summary>Indicates whether the open palm is actively painting right now.</summary>
    private bool IsBrushPainting { get; set; }

    #endregion

    #region | Live frames and hand tracking |

    private void OnFrameArrived(object sender, EventArgs e)
    {
        //Capture-thread context: get out fast
        if (!HasFrame)
        {
            InvokeOnMainThread(() => HasFrame = _captureService.HasFrame);
        }

        if (IsCaptureMode)
        {
            InvalidateMainCanvas?.Invoke();
        }
        else
        {
            //Paint Mode: the live feed drives the hand tracker and the little self-view
            var tracker = _tracker;
            if (tracker is { IsRunning: true }
                && _captureService.TryCopyLatestFrame(ref _visionFrame, out var width, out var height))
            {
                tracker.SubmitFrame(_visionFrame, width, height);
            }
            InvalidateSelfView?.Invoke();
        }
    }

    private void OnTrackingUpdated(object sender, HandTrackingEventArgs e)
    {
        //Worker-thread context: marshal all painting decisions onto the UI thread
        var result = e.Result;
        InvokeOnMainThread(() =>
        {
            var session = _paintSession;
            if (IsCaptureMode || session == null) { return; }

            if (result.HandDetected)
            {
                //The preview and the captured still are mirrored, so mirror the hand too
                CrosshairNormX = 1f - result.PalmCenterX;
                CrosshairNormY = result.PalmCenterY;
            }
            else
            {
                CrosshairNormX = null;
                CrosshairNormY = null;
            }

            var paintNow = result.HandDetected && result.IsOpenPalm;
            IsBrushPainting = paintNow;

            //Strokes are driven in normalized still-image coordinates, so no canvas size is
            //  needed - the drawing space is calibrated from the captured photo.
            if (paintNow && CrosshairNormX != null && CrosshairNormY != null)
            {
                if (session.IsStrokeActive)
                {
                    session.ContinueStroke(CrosshairNormX.Value, CrosshairNormY.Value);
                }
                else
                {
                    session.BeginStroke(CrosshairNormX.Value, CrosshairNormY.Value);
                }
            }
            else if (session.IsStrokeActive)
            {
                session.EndStroke();
            }

            InvalidateMainCanvas?.Invoke();
        });
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

    [AffectsCommands(nameof(TakePhotoCommand), nameof(BackCommand), nameof(ClearCommand),
        nameof(SaveCommand), nameof(SelectColorCommand))]
    public bool IsCaptureMode
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsPaintMode));
        }
    } = true;

    /// <summary>Paint Mode is simply not-Capture Mode.</summary>
    public bool IsPaintMode => !IsCaptureMode;

    [AffectsCommands(nameof(TakePhotoCommand))]
    public bool HasFrame
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [AffectsCommands(nameof(SaveCommand), nameof(ClearCommand))]
    public bool HasDrawing
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [AffectsCommands(nameof(TakePhotoCommand), nameof(BackCommand), nameof(ClearCommand),
        nameof(SaveCommand), nameof(SelectColorCommand))]
    public bool IsBusy
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ActiveColorText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The selectable highlighter colors, one button item per palette entry.</summary>
    public IReadOnlyList<HighlighterColorViewModel> HighlighterColors { get; }

    /// <summary>Set by the hosting head (see <see cref="IFileSaveBridge"/>); null on heads with no file dialog.</summary>
    public Func<string, Task<string>> PickSaveJpegPathAsync { get; set; }

    /// <summary>Set by the hosting page (see <see cref="ICanvasBridge"/>).</summary>
    public Action InvalidateMainCanvas { get; set; }

    /// <summary>Set by the hosting page (see <see cref="ICanvasBridge"/>).</summary>
    public Action InvalidateSelfView { get; set; }

    #endregion

    private void SwitchCamera(CameraDevice camera)
    {
        try
        {
            HasFrame = false;
            if (camera == null)
            {
                _captureService.Stop();
                InvalidateMainCanvas?.Invoke();
                return;
            }

            _captureService.Start(camera);
            StatusText = $"Live: {camera.FriendlyName}";
        }
        catch (Exception e)
        {
            StatusText = $"Could not start '{camera?.FriendlyName}': {e.Message}";
        }
    }

    #region | Commands and their implementations |

    #region TakePhotoCommand

    private SimpleCommand _takePhotoCommand;
    public SimpleCommand TakePhotoCommand =>
        (_takePhotoCommand ??= new SimpleCommand(CanTakePhoto, DoTakePhoto));

    private bool CanTakePhoto() => (!IsBusy) && IsCaptureMode && HasFrame;

    private async Task DoTakePhoto()
    {
        if (!CanTakePhoto()) { return; }

        IsBusy = true;
        try
        {
            var photo = _captureService.CapturePhoto();

            //The preview the user was watching is mirrored, so mirror the still to match
            var session = await Task.Run(() =>
                PaintingSession.Create(photo.PixelsBgra32, photo.Width, photo.Height, mirrorHorizontally: true));

            session.Session.RedrawRequested += (_, _) => InvalidateMainCanvas?.Invoke();
            session.Session.DrawingChanged += (_, _) =>
                InvokeOnMainThread(() => HasDrawing = _paintSession?.HasStrokes ?? false);

            _paintSession = session;
            HasDrawing = false;
            ActiveColorText = $"Painting with: {session.ActiveColorName}";

            if (_tracker == null)
            {
                _tracker = new HandTracker();
                _tracker.TrackingUpdated += OnTrackingUpdated;
            }
            _tracker.Start();

            IsCaptureMode = false;
            InvalidateMainCanvas?.Invoke();
            StatusText = "Show the camera your open palm to spread paint on the photo - " +
                         "close your hand (or hide it) to stop painting.";
        }
        catch (Exception e)
        {
            StatusText = $"Photo failed: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region BackCommand

    private SimpleCommand _backCommand;
    public SimpleCommand BackCommand =>
        (_backCommand ??= new SimpleCommand(CanGoBack, DoGoBack));

    private bool CanGoBack() => (!IsBusy) && IsPaintMode;

    private async Task DoGoBack()
    {
        if (!CanGoBack()) { return; }

        if (HasDrawing)
        {
            var discard = await ConfirmDialog(
                "Going back to the camera will discard your painting. Are you sure?",
                "Discard painting?");
            if (!discard) { return; }
        }

        LeavePaintMode();
        StatusText = SelectedCamera != null
            ? $"Live: {SelectedCamera.FriendlyName}"
            : "Select a camera.";
    }

    private void LeavePaintMode()
    {
        _tracker?.Stop();

        var session = _paintSession;
        _paintSession = null;
        session?.Dispose();

        HasDrawing = false;
        CrosshairNormX = null;
        CrosshairNormY = null;
        IsBrushPainting = false;
        ActiveColorText = string.Empty;

        IsCaptureMode = true;
        InvalidateMainCanvas?.Invoke();
    }

    #endregion

    #region ClearCommand

    private SimpleCommand _clearCommand;
    public SimpleCommand ClearCommand =>
        (_clearCommand ??= new SimpleCommand(CanClear, DoClear));

    private bool CanClear() => (!IsBusy) && IsPaintMode && HasDrawing;

    private async Task DoClear()
    {
        if (!CanClear()) { return; }

        var doClear = true;
        if (_paintSession.StrokeCount > 2)
        {
            doClear = await ConfirmDialog(
                "Are you sure you want to clear your painting and start over?",
                "Confirm");
        }

        if (doClear)
        {
            _paintSession.Clear();
            StatusText = "Cleared - paint something new.";
        }
    }

    #endregion

    #region SaveCommand

    private SimpleCommand _saveCommand;
    public SimpleCommand SaveCommand =>
        (_saveCommand ??= new SimpleCommand(CanSave, DoSave));

    private bool CanSave() => (!IsBusy) && IsPaintMode && HasDrawing;

    private static string GetSuggestedFileName() => "webcam_painting.jpg";

    private static string GetDefaultSavePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (String.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        return Path.Combine(folder, $"webcam_painting_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
    }

    private async Task DoSave()
    {
        if (!CanSave()) { return; }

        try
        {
            string outputPath;

            if (PickSaveJpegPathAsync == null)
            {
                //No native file dialog on this head (e.g. the Linux framebuffer head) -
                //  save to a default location instead
                outputPath = GetDefaultSavePath();
            }
            else
            {
                outputPath = await PickSaveJpegPathAsync(GetSuggestedFileName());
                if (String.IsNullOrWhiteSpace(outputPath))
                {
                    return; //the user cancelled the dialog
                }
                outputPath = outputPath.Trim();

                //Confirm before clobbering an existing file (the head's own overwrite
                //  prompt is suppressed so this is the single confirmation)
                if (File.Exists(outputPath))
                {
                    var replace = await ConfirmDialog(
                        $"A file already exists at:\n{outputPath}\n\nDo you want to replace it?",
                        "Replace existing file?");
                    if (!replace)
                    {
                        StatusText = "Save cancelled - the existing file was kept.";
                        return;
                    }
                }
            }

            IsBusy = true;

            var jpeg = _paintSession.ExportJpeg();
            await File.WriteAllBytesAsync(outputPath, jpeg);

            StatusText = $"Saved: {outputPath}";

            var clearDrawing = await ConfirmDialog(
                $"The painted photo was saved to:\n{outputPath}\n\nDo you want to clear the painting?",
                "Image saved");
            if (clearDrawing)
            {
                _paintSession.Clear();
            }
        }
        catch (NotSupportedException)
        {
            await ShowError("File dialogs are not supported on this head.");
        }
        catch (Exception e)
        {
            StatusText = "Saving failed.";
            await ShowError($"Error while saving the painted photo: {e.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region SelectColorCommand

    private SimpleCommand _selectColorCommand;
    public SimpleCommand SelectColorCommand =>
        (_selectColorCommand ??= new SimpleCommand(CanSelectColor, (Action<object>)DoSelectColor));

    private bool CanSelectColor() => (!IsBusy) && IsPaintMode;

    private void DoSelectColor(object parameter)
    {
        var session = _paintSession;
        if (session != null && parameter is string colorName && session.SelectColor(colorName))
        {
            ActiveColorText = $"Painting with: {session.ActiveColorName}";
        }
    }

    #endregion

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        _takePhotoCommand?.Dispose();
        _takePhotoCommand = null;
        _backCommand?.Dispose();
        _backCommand = null;
        _clearCommand?.Dispose();
        _clearCommand = null;
        _saveCommand?.Dispose();
        _saveCommand = null;
        _selectColorCommand?.Dispose();
        _selectColorCommand = null;

        PickSaveJpegPathAsync = null;
        InvalidateMainCanvas = null;
        InvalidateSelfView = null;

        if (_tracker != null)
        {
            _tracker.TrackingUpdated -= OnTrackingUpdated;
            _tracker.Dispose();
            _tracker = null;
        }

        var session = _paintSession;
        _paintSession = null;
        session?.Dispose();

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
