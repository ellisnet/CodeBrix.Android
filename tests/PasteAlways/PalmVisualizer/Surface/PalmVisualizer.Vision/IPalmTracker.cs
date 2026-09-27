using System;

namespace PalmVisualizer.Vision;

/// <summary>
/// The multi-palm tracking pipeline as its consumer sees it: start it, feed it webcam frames
/// and handle the results it raises. Registered with the dependency-injection container at
/// startup and resolved by the view model, so the view model can be built - and exercised -
/// with a stand-in that runs no inference.
/// </summary>
public interface IPalmTracker : IDisposable
{
    /// <summary>Indicates whether the tracker's worker is running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised after each processed frame - including all-hands-lost frames, so subscribers
    /// can release their palm-driven state. Raised on the tracker's WORKER thread: handlers
    /// must marshal any UI work themselves.
    /// </summary>
    event EventHandler<PalmTrackingEventArgs> TrackingUpdated;

    /// <summary>
    /// Starts the tracker: loads the models and spins up the inference worker. Safe to call
    /// when already started.
    /// </summary>
    void Start();

    /// <summary>Stops the inference worker. Safe to call when already stopped.</summary>
    void Stop();

    /// <summary>
    /// Offers a frame to the tracker. The pixels are copied before returning, so the caller
    /// may reuse its buffer immediately. When the worker is still busy with an earlier
    /// frame, the previous pending frame is silently replaced (latest wins).
    /// </summary>
    /// <param name="bgraPixels">The frame's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    void SubmitFrame(byte[] bgraPixels, int width, int height);
}
