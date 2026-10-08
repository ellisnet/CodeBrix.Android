using CodeBrix.Webcam.Devices;

namespace WebcamPainter.Webcam;

/// <summary>
/// One connected camera, as shown in the camera-selection dropdown. Wraps the discovered
/// device so consumers of this library never handle CodeBrix.Webcam types directly.
/// </summary>
public sealed class CameraDevice
{
    internal CameraDevice(IImagingMediaDevice device)
        : this(device.Id, device.FriendlyName)
    {
        Device = device;
    }

    //For camera sources that are not backed by a discovered device
    internal CameraDevice(string id, string friendlyName)
    {
        Id = id;
        FriendlyName = friendlyName;
    }

    internal IImagingMediaDevice Device { get; }

    /// <summary>The camera's unique hardware identifier.</summary>
    public string Id { get; }

    /// <summary>The camera's human-readable name.</summary>
    public string FriendlyName { get; }

    /// <summary>The dropdown display text.</summary>
    public override string ToString() => FriendlyName;
}
