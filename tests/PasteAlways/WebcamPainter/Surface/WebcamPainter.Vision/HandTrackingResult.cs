using System;

namespace WebcamPainter.Vision;

/// <summary>
/// The outcome of analyzing one webcam frame for the user's hand.
/// </summary>
public sealed class HandTrackingResult
{
    internal HandTrackingResult(bool handDetected, bool isOpenPalm,
        float palmCenterX, float palmCenterY, float detectionScore, float presenceScore)
    {
        HandDetected = handDetected;
        IsOpenPalm = isOpenPalm;
        PalmCenterX = palmCenterX;
        PalmCenterY = palmCenterY;
        DetectionScore = detectionScore;
        PresenceScore = presenceScore;
    }

    internal static HandTrackingResult NoHand { get; } =
        new HandTrackingResult(false, false, 0f, 0f, 0f, 0f);

    /// <summary>Indicates whether a hand was found in the frame.</summary>
    public bool HandDetected { get; }

    /// <summary>
    /// Indicates whether the hand is showing the open-palm ("spatula") gesture - the
    /// gesture that paints.
    /// </summary>
    public bool IsOpenPalm { get; }

    /// <summary>
    /// The palm center's horizontal position, normalized 0..1 across the UNMIRRORED camera
    /// frame (smoothed across recent frames).
    /// </summary>
    public float PalmCenterX { get; }

    /// <summary>
    /// The palm center's vertical position, normalized 0..1 down the camera frame
    /// (smoothed across recent frames).
    /// </summary>
    public float PalmCenterY { get; }

    /// <summary>The palm detector's confidence, 0..1.</summary>
    public float DetectionScore { get; }

    /// <summary>The landmark model's hand-presence confidence, 0..1.</summary>
    public float PresenceScore { get; }
}

/// <summary>Carries a <see cref="HandTrackingResult"/> to <see cref="HandTracker.TrackingUpdated"/> subscribers.</summary>
public sealed class HandTrackingEventArgs : EventArgs
{
    internal HandTrackingEventArgs(HandTrackingResult result)
    {
        Result = result;
    }

    /// <summary>The analysis outcome for the most recently processed frame.</summary>
    public HandTrackingResult Result { get; }
}
