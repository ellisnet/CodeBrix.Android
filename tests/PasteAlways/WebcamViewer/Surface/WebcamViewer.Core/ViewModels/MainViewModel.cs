using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Platform.Simple;
using CodeBrix.Webcam;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WebcamViewer.ViewModels;

/// <summary>
/// Lets the hosting page give the view model a native "choose a folder…" dialog. The page
/// wires this up with the CodeBrix.Platform <c>FolderPicker</c>; a head with no folder dialog
/// leaves it null and the user types the path into the text box instead.
/// </summary>
public interface IFolderPickBridge
{
    /// <summary>Shows a folder picker and returns the chosen path, or null if cancelled.</summary>
    Func<Task<string>> PickFolderPathAsync { get; set; }
}

/// <summary>
/// Lets the hosting page hand the view model a way to invalidate (repaint) the Skia canvas
/// that displays the live video. Frames arrive on a capture thread; the page's delegate is
/// responsible for marshalling the invalidate onto its UI thread.
/// </summary>
public interface ICanvasInvalidator
{
    /// <summary>Invalidates the hosting page's video canvas (null before the page wires it up).</summary>
    Action InvalidateCanvas { get; set; }
}

/// <summary>
/// Lets the canvas renderer pull the most recent webcam frame without knowing which
/// concrete view model produced it. The hosting page resolves its data context through this
/// interface and hands it to the renderer from the paint handler.
/// </summary>
public interface IVideoFrameSource
{
    /// <summary>
    /// Copies the most recent video frame (tightly packed BGRA) into <paramref name="buffer"/>,
    /// which is (re)allocated when its length does not match the frame. Returns false when no
    /// frame has arrived yet, which is a normal state rather than an error.
    /// </summary>
    /// <param name="buffer">The caller's pixel buffer; replaced when it is the wrong size.</param>
    /// <param name="width">The frame width in pixels, or 0 when there is no frame.</param>
    /// <param name="height">The frame height in pixels, or 0 when there is no frame.</param>
    /// <returns>True when a frame was copied out.</returns>
    bool TryGetLatestFrame(ref byte[] buffer, out int width, out int height);
}

/// <summary>One entry in the connected-cameras dropdown.</summary>
public class CameraOption
{
    /// <summary>Wraps a discovered camera for display.</summary>
    public CameraOption(IImagingMediaDevice device)
    {
        Device = device;
    }

    /// <summary>The discovered camera.</summary>
    public IImagingMediaDevice Device { get; }

    /// <summary>The dropdown display text.</summary>
    public override string ToString() => Device.FriendlyName;
}

[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IFolderPickBridge, ICanvasInvalidator, IVideoFrameSource
{
    private WebcamSession _session;

    private readonly object _frameLock = new object();
    private byte[] _latestFrame;
    private int _frameWidth;
    private int _frameHeight;

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        Debug.WriteLine("Webcam Viewer view model startup.");
        StatusText = "Discovering cameras…";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
            InvokeOnMainThread(() =>
            {
                Cameras.Clear();
                foreach (var device in devices)
                {
                    Cameras.Add(new CameraOption(device));
                }
                if (Cameras.Count == 0)
                {
                    StatusText = "No cameras were found on this machine.";
                }
                else
                {
                    StatusText = $"Found {Cameras.Count} camera(s).";
                    SelectedCamera = Cameras[0]; // auto-start on the first camera
                }
            });
        }
        catch (Exception e)
        {
            InvokeOnMainThread(() => StatusText = $"Camera discovery failed: {e.Message}");
        }
    }

    private void SwitchCamera(CameraOption camera)
    {
        try
        {
            HasFrame = false;
            if (_session != null)
            {
                _session.FrameReceived -= OnFrameReceived;
                _session.Dispose();
                _session = null;
            }
            lock (_frameLock)
            {
                _latestFrame = null;
            }
            InvalidateCanvas?.Invoke();

            if (camera == null)
            {
                IsMicAvailable = false;
                return;
            }

            _session = new WebcamSession(camera.Device);
            _session.FrameReceived += OnFrameReceived;
            _session.MonitorAudio = IsAudioMonitorOn;
            _session.Start();
            IsMicAvailable = _session.IsAudioCaptureActive;
            StatusText = $"Live: {camera.Device.FriendlyName}";
        }
        catch (Exception e)
        {
            StatusText = $"Could not start '{camera?.Device.FriendlyName}': {e.Message}";
        }
    }

    private void OnFrameReceived(object sender, WebcamFrameEventArgs frame)
    {
        // Capture-thread context: copy the pixels and get out fast.
        lock (_frameLock)
        {
            var needed = (int)(frame.Width * frame.Height * 4);
            if (_latestFrame == null || _latestFrame.Length != needed)
            {
                _latestFrame = new byte[needed];
            }
            frame.CopyTo(_latestFrame);
            _frameWidth = (int)frame.Width;
            _frameHeight = (int)frame.Height;
        }

        if (!HasFrame)
        {
            InvokeOnMainThread(() => HasFrame = true);
        }
        InvalidateCanvas?.Invoke();
    }

    /// <summary>
    /// Copies the most recent video frame (tightly packed BGRA) into <paramref name="buffer"/>
    /// (which is (re)allocated as needed). Returns false when no frame has arrived yet.
    /// Called by the canvas renderer on the UI thread.
    /// </summary>
    public bool TryGetLatestFrame(ref byte[] buffer, out int width, out int height)
    {
        lock (_frameLock)
        {
            if (_latestFrame == null)
            {
                width = 0;
                height = 0;
                return false;
            }
            if (buffer == null || buffer.Length != _latestFrame.Length)
            {
                buffer = new byte[_latestFrame.Length];
            }
            Array.Copy(_latestFrame, buffer, _latestFrame.Length);
            width = _frameWidth;
            height = _frameHeight;
            return true;
        }
    }

    private static bool IsValidFolder(string path)
        => !String.IsNullOrWhiteSpace(path) && Directory.Exists(path.Trim());

    #region | Bindable properties |

    /// <summary>The connected cameras shown in the dropdown.</summary>
    public ObservableCollection<CameraOption> Cameras { get; } = new();

    private CameraOption _selectedCamera;
    public CameraOption SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (_selectedCamera != value)
            {
                SetProperty(ref _selectedCamera, value);
                SwitchCamera(value);
            }
        }
    }

    private bool _hasFrame;
    [AffectsCommands(nameof(PhotoCommand))]
    public bool HasFrame
    {
        get => _hasFrame;
        private set => SetProperty(ref _hasFrame, value);
    }

    private bool _isMicAvailable;
    public bool IsMicAvailable
    {
        get => _isMicAvailable;
        private set => SetProperty(ref _isMicAvailable, value);
    }

    private bool _isAudioMonitorOn;
    public bool IsAudioMonitorOn
    {
        get => _isAudioMonitorOn;
        set
        {
            SetProperty(ref _isAudioMonitorOn, value);
            var session = _session;
            if (session != null)
            {
                session.MonitorAudio = value;
            }
        }
    }

    private string _folderPath = string.Empty;
    [AffectsCommands(nameof(PhotoCommand))]
    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value ?? string.Empty);
    }

    private bool _isBusy;
    [AffectsCommands(nameof(PhotoCommand), nameof(BrowseFolderCommand))]
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    /// <summary>Set by the hosting head (see <see cref="IFolderPickBridge"/>); null on heads with no folder dialog.</summary>
    public Func<Task<string>> PickFolderPathAsync { get; set; }

    /// <summary>Set by the hosting page (see <see cref="ICanvasInvalidator"/>).</summary>
    public Action InvalidateCanvas { get; set; }

    #endregion

    #region | Commands and their implementations |

    #region BrowseFolderCommand

    private SimpleCommand _browseFolderCommand;
    public SimpleCommand BrowseFolderCommand =>
        (_browseFolderCommand ??= new SimpleCommand(() => !IsBusy, DoBrowseFolder));

    private async Task DoBrowseFolder()
    {
        if (PickFolderPathAsync == null)
        {
            StatusText = "No folder dialog on this head - type the folder path into the text box.";
            return;
        }
        try
        {
            var path = await PickFolderPathAsync();
            if (!String.IsNullOrWhiteSpace(path))
            {
                FolderPath = path.Trim();
                StatusText = $"Photos will be saved to: {FolderPath}";
            }
        }
        catch (Exception e)
        {
            StatusText = $"Folder dialog failed: {e.Message} - type the folder path instead.";
        }
    }

    #endregion

    #region PhotoCommand

    private SimpleCommand _photoCommand;
    public SimpleCommand PhotoCommand =>
        (_photoCommand ??= new SimpleCommand(CanTakePhoto, DoTakePhoto));

    private bool CanTakePhoto() => (!IsBusy) && HasFrame && IsValidFolder(FolderPath);

    private async Task DoTakePhoto()
    {
        if (!CanTakePhoto()) { return; }

        IsBusy = true;
        try
        {
            var session = _session;
            if (session == null) { return; }

            WebcamPhoto photo = session.CapturePhoto();
            string fileName = $"frame_capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string outputPath = Path.Combine(FolderPath.Trim(), fileName);

            // The raw BGRA pixels hand straight to CodeBrix.Imaging for PNG encoding.
            await Task.Run(() =>
            {
                using Image<Bgra32> image = Image.LoadPixelData<Bgra32>(
                    photo.PixelsBgra32, photo.Width, photo.Height, PngFormat.Instance);
                image.SaveAsPng(outputPath);
            });

            StatusText = $"Saved: {outputPath}";
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

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        _browseFolderCommand?.Dispose();
        _browseFolderCommand = null;
        _photoCommand?.Dispose();
        _photoCommand = null;

        PickFolderPathAsync = null;
        InvalidateCanvas = null;

        if (_session != null)
        {
            _session.FrameReceived -= OnFrameReceived;
            _session.Dispose();
            _session = null;
        }

        base.Dispose();
    }

    #endregion
}
