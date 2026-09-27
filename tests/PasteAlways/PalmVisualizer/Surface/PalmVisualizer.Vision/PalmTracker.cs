// STUB (paste always): PalmVisualizer.Vision.PalmTracker (CodeBrix.Samples PalmVisualizer/src/libs/PalmVisualizer.Vision/
// PalmTracker.cs); the original runs two embedded MediaPipe TFLite models through CodeBrix.VideoProcessing.OpenCV5
// (native OpenCV per platform; its camera input is D-O14 NotSupported on Android anyway). Same namespace, type name and
// public members; it never reports a palm.
using System;

namespace PalmVisualizer.Vision;

/// <summary>Stand-in for the open-palm tracker (compile-only; never reports a palm).</summary>
public sealed class PalmTracker : IPalmTracker
{
    /// <summary>The most palms tracked at once.</summary>
    public const int MaxPalms = 4;

    /// <summary>The hand-presence score a detection must reach.</summary>
    public const float PresenceThreshold = 0.5f;

    /// <summary>The exponential smoothing factor applied to palm centers.</summary>
    public const float SmoothingAlpha = 0.5f;

    /// <summary>The largest normalized distance a palm may move and keep its track.</summary>
    public const float TrackMatchMaxDistance = 0.25f;

    private bool _running;

    /// <inheritdoc />
    public bool IsRunning => _running;

    /// <inheritdoc />
    public event EventHandler<PalmTrackingEventArgs> TrackingUpdated { add { } remove { } }

    /// <inheritdoc />
    public void Start() => _running = true;

    /// <inheritdoc />
    public void Stop() => _running = false;

    /// <inheritdoc />
    public void SubmitFrame(byte[] bgraPixels, int width, int height) { }

    /// <inheritdoc />
    public void Dispose() => _running = false;
}
