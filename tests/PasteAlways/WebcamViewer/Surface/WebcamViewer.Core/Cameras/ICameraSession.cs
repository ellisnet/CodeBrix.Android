using System;

namespace WebcamViewer.Cameras;

/// <summary>One live session on one camera, as the view model uses it.</summary>
public interface ICameraSession : IDisposable
{
    /// <summary>Raised on a capture thread for every live frame; the frame is valid only until the handler returns.</summary>
    event EventHandler<CameraFrameEventArgs> FrameReceived;

    /// <summary>True when the session found a microphone to capture.</summary>
    bool IsAudioCaptureActive { get; }

    /// <summary>Plays the captured microphone through the default output while true.</summary>
    bool MonitorAudio { get; set; }

    /// <summary>Opens the camera and starts the stream; throws when the camera cannot be opened.</summary>
    void Start();

    /// <summary>Takes the next live frame as a photo.</summary>
    /// <param name="width">The photo width in pixels.</param>
    /// <param name="height">The photo height in pixels.</param>
    /// <returns>The photo's pixels, tightly packed BGRA.</returns>
    byte[] CapturePhoto(out int width, out int height);
}
