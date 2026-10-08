using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebcamPainter.Webcam;

/// <summary>
/// The camera the application paints from: discovers cameras, runs live capture on one of
/// them, keeps its most recent BGRA frame available and takes in-memory still photos.
/// <see cref="WebcamCaptureService"/> is the real implementation; the application resolves
/// this interface from its services, so another host can supply a different source.
/// </summary>
public interface ICameraSource : IDisposable
{
    /// <summary>
    /// Discovers the cameras this source can capture from.
    /// </summary>
    /// <returns>The available cameras; empty when none were found.</returns>
    Task<IReadOnlyList<CameraDevice>> GetCamerasAsync();

    /// <summary>Indicates whether a capture session is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Indicates whether at least one frame has arrived since the session started.</summary>
    bool HasFrame { get; }

    /// <summary>
    /// Raised after each new frame has arrived and is available via
    /// <see cref="TryCopyLatestFrame"/>. May be raised on any thread - handlers must get
    /// out fast and must marshal any UI work themselves.
    /// </summary>
    event EventHandler FrameArrived;

    /// <summary>
    /// Starts (or switches) live capture on the given camera. Any prior session is stopped
    /// first and its last frame discarded.
    /// </summary>
    /// <param name="camera">The camera to capture from.</param>
    void Start(CameraDevice camera);

    /// <summary>Stops the running capture session, when there is one.</summary>
    void Stop();

    /// <summary>
    /// Copies the most recent frame (tightly packed BGRA) into <paramref name="buffer"/>,
    /// which is (re)allocated as needed. Returns <c>false</c> when no frame has arrived yet.
    /// Safe to call from any thread.
    /// </summary>
    /// <param name="buffer">The caller's frame buffer; replaced when the size does not match.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns><c>true</c> when a frame was copied.</returns>
    bool TryCopyLatestFrame(ref byte[] buffer, out int width, out int height);

    /// <summary>
    /// Captures a still photo from the live session, in memory (nothing is written to disk).
    /// </summary>
    /// <returns>The captured photo.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no capture session is running.</exception>
    CapturedPhoto CapturePhoto();
}
