using CodeBrix.Webcam.Devices;

namespace WebcamPainter.Webcam;

/// <summary>
/// One connected camera, as shown in the camera-selection dropdown. Wraps the discovered
/// device so consumers of this library never handle CodeBrix.Webcam types directly.
/// </summary>
public sealed class CameraDevice
{
    internal CameraDevice(IImagingMediaDevice device)
    {
        Device = device;
    }

    internal IImagingMediaDevice Device { get; }

    /// <summary>The camera's unique hardware identifier.</summary>
    public string Id => Device.Id;

    /// <summary>The camera's human-readable name.</summary>
    public string FriendlyName => Device.FriendlyName;

    /// <summary>The dropdown display text.</summary>
    public override string ToString() => Device.FriendlyName;
}
