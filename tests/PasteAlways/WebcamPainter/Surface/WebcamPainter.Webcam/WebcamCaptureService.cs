using CodeBrix.Webcam;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebcamPainter.Webcam;

/// <summary>
/// The webcam capture model for WebcamPainter: discovers connected cameras, runs a live
/// capture session on the selected one, keeps the most recent BGRA frame available for
/// rendering (and for the hand-tracking pipeline), and takes in-memory still photos.
/// Audio is never captured - this application only paints. The latest-frame cache lives in
/// the underlying <see cref="WebcamSession"/>; this service just forwards to it.
/// </summary>
public sealed class WebcamCaptureService : IDisposable
{
    private volatile bool _hasFrame;

    private WebcamSession _session;

    /// <summary>
    /// Discovers the cameras connected to this computer.
    /// </summary>
    /// <returns>The connected cameras; empty when none were found.</returns>
    public static async Task<IReadOnlyList<CameraDevice>> GetCamerasAsync()
    {
        IReadOnlyList<IImagingMediaDevice> devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        var cameras = new List<CameraDevice>();
        foreach (IImagingMediaDevice device in devices)
        {
            cameras.Add(new CameraDevice(device));
        }
        return cameras;
    }

    /// <summary>Indicates whether a capture session is currently running.</summary>
    public bool IsRunning => _session != null;

    /// <summary>Indicates whether at least one frame has arrived since the session started.</summary>
    public bool HasFrame => _hasFrame;

    /// <summary>
    /// Raised after each new frame has arrived and is available via
    /// <see cref="TryCopyLatestFrame"/>. Raised on the CAPTURE thread - handlers must get
    /// out fast and must marshal any UI work themselves.
    /// </summary>
    public event EventHandler FrameArrived;

    /// <summary>
    /// Starts (or switches) live capture on the given camera. Any prior session is stopped
    /// first and its last frame discarded.
    /// </summary>
    /// <param name="camera">The camera to capture from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="camera"/> is null.</exception>
    public void Start(CameraDevice camera)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }

        Stop();

        _session = new WebcamSession(camera.Device);
        _session.FrameReceived += OnFrameReceived;
        _session.Start();
    }

    /// <summary>Stops the running capture session, when there is one.</summary>
    public void Stop()
    {
        if (_session != null)
        {
            _session.FrameReceived -= OnFrameReceived;
            _session.Dispose();
            _session = null;
        }
        _hasFrame = false;
    }

    private void OnFrameReceived(object sender, WebcamFrameEventArgs frame)
    {
        //Capture-thread context: the session caches the pixels itself (see TryCopyLatestFrame);
        //  we only note that a frame exists and get out fast.
        _hasFrame = true;
        FrameArrived?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Copies the most recent frame (tightly packed BGRA) into <paramref name="buffer"/>,
    /// which is (re)allocated as needed. Returns <c>false</c> when no frame has arrived yet.
    /// Safe to call from any thread.
    /// </summary>
    /// <param name="buffer">The caller's frame buffer; replaced when the size does not match.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns><c>true</c> when a frame was copied.</returns>
    public bool TryCopyLatestFrame(ref byte[] buffer, out int width, out int height)
    {
        WebcamSession session = _session;
        if (session == null)
        {
            width = 0;
            height = 0;
            return false;
        }
        return session.TryCopyLatestFrame(ref buffer, out width, out height);
    }

    /// <summary>
    /// Captures a still photo from the live session, in memory (nothing is written to disk).
    /// </summary>
    /// <returns>The captured photo.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no capture session is running.</exception>
    public CapturedPhoto CapturePhoto()
    {
        WebcamSession session = _session;
        if (session == null)
        {
            throw new InvalidOperationException("Start a camera before capturing a photo.");
        }

        WebcamPhoto photo = session.CapturePhoto();
        return new CapturedPhoto(photo.PixelsBgra32, photo.Width, photo.Height);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        FrameArrived = null;
        Stop();
    }
}
