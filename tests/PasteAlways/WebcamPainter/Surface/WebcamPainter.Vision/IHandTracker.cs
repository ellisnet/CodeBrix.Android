using System;

namespace WebcamPainter.Vision;

/// <summary>
/// Finds the user's hand in webcam frames: feed it frames with <see cref="SubmitFrame"/> and
/// it raises <see cref="TrackingUpdated"/> with the palm's position and open-palm state.
/// <see cref="HandTracker"/> is the real implementation; the application resolves this
/// interface from its services, so another host can supply a different tracker.
/// </summary>
public interface IHandTracker : IDisposable
{
    /// <summary>Indicates whether the tracker is running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised after each processed frame - including hand-lost frames, so subscribers can
    /// end an in-progress paint stroke. May be raised on any thread: handlers must marshal
    /// any UI work themselves.
    /// </summary>
    event EventHandler<HandTrackingEventArgs> TrackingUpdated;

    /// <summary>Starts the tracker. Safe to call when already started.</summary>
    void Start();

    /// <summary>Stops the tracker. Safe to call when already stopped.</summary>
    void Stop();

    /// <summary>
    /// Offers a frame to the tracker. The pixels are copied before returning, so the caller
    /// may reuse its buffer immediately.
    /// </summary>
    /// <param name="bgraPixels">The frame's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    void SubmitFrame(byte[] bgraPixels, int width, int height);
}
