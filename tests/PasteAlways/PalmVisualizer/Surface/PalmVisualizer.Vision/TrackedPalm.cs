namespace PalmVisualizer.Vision;

/// <summary>
/// One hand-palm the tracker is currently following across frames.
/// </summary>
public sealed class TrackedPalm
{
    internal TrackedPalm(int trackId, bool isOpenPalm,
        float palmCenterX, float palmCenterY, float detectionScore, float presenceScore)
    {
        TrackId = trackId;
        IsOpenPalm = isOpenPalm;
        PalmCenterX = palmCenterX;
        PalmCenterY = palmCenterY;
        DetectionScore = detectionScore;
        PresenceScore = presenceScore;
    }

    /// <summary>
    /// A stable identifier for this palm while it stays in view. The same physical hand
    /// keeps the same id from frame to frame (nearest-neighbor matching); a hand that
    /// leaves the frame and comes back gets a new id.
    /// </summary>
    public int TrackId { get; }

    /// <summary>
    /// Indicates whether the hand is showing the open-palm gesture - the gesture that
    /// attracts the visualization.
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
