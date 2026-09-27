using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PalmVisualizer.Camera;

/// <summary>
/// The narrowest possible "where the pixels come from" seam: one call that copies the most
/// recent frame. A page hands its <see cref="WebcamFrameRenderer"/> one of these, so the
/// painting code - and the page that owns it - never has to know what a capture service is
/// or who owns one.
/// </summary>
public interface IWebcamFrameSource
{
    /// <summary>
    /// Copies the most recent frame (tightly packed BGRA) into <paramref name="buffer"/>,
    /// which is (re)allocated as needed. Returns <c>false</c> when no frame is available.
    /// Safe to call from any thread.
    /// </summary>
    /// <param name="buffer">The caller's frame buffer; replaced when the size does not match.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns><c>true</c> when a frame was copied.</returns>
    bool TryCopyLatestFrame(ref byte[] buffer, out int width, out int height);
}

/// <summary>
/// The webcam capture model as the application consumes it: camera discovery, a live session
/// on the chosen camera, and the latest frame. Registered with the dependency-injection
/// container at startup and resolved by the view model, so the view model can be built - and
/// exercised - with a stand-in that needs no camera.
/// </summary>
public interface IWebcamCaptureService : IWebcamFrameSource, IDisposable
{
    /// <summary>Indicates whether a capture session is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Indicates whether at least one frame has arrived since the session started.</summary>
    bool HasFrame { get; }

    /// <summary>
    /// Raised after each new frame has arrived and is available via
    /// <see cref="IWebcamFrameSource.TryCopyLatestFrame"/>. Raised on the CAPTURE thread -
    /// handlers must get out fast and must marshal any UI work themselves.
    /// </summary>
    event EventHandler FrameArrived;

    /// <summary>
    /// Discovers the cameras connected to this computer. Works with no session running and
    /// no camera present, so it is safe to call at startup.
    /// </summary>
    /// <returns>The connected cameras; empty when none were found.</returns>
    Task<IReadOnlyList<CameraDevice>> DiscoverCamerasAsync();

    /// <summary>
    /// Starts (or switches) live capture on the given camera. Any prior session is stopped
    /// first and its last frame discarded.
    /// </summary>
    /// <param name="camera">The camera to capture from.</param>
    void Start(CameraDevice camera);

    /// <summary>Stops the running capture session, when there is one.</summary>
    void Stop();
}
