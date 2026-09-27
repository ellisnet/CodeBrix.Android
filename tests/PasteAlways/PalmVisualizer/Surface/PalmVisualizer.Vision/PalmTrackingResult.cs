using System;
using System.Collections.Generic;

namespace PalmVisualizer.Vision;

/// <summary>
/// The outcome of analyzing one webcam frame for hand-palms.
/// </summary>
public sealed class PalmTrackingResult
{
    internal PalmTrackingResult(IReadOnlyList<TrackedPalm> palms)
    {
        Palms = palms;
    }

    internal static PalmTrackingResult Empty { get; } =
        new PalmTrackingResult(Array.Empty<TrackedPalm>());

    /// <summary>The palms found in the frame, in stable track order; empty when no hands are visible.</summary>
    public IReadOnlyList<TrackedPalm> Palms { get; }
}

/// <summary>Carries a <see cref="PalmTrackingResult"/> to <see cref="PalmTracker.TrackingUpdated"/> subscribers.</summary>
public sealed class PalmTrackingEventArgs : EventArgs
{
    internal PalmTrackingEventArgs(PalmTrackingResult result)
    {
        Result = result;
    }

    /// <summary>The analysis outcome for the most recently processed frame.</summary>
    public PalmTrackingResult Result { get; }
}
