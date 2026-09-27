// STUB (paste always): PalmVisualizer.Camera.CameraDevice (CodeBrix.Samples PalmVisualizer/src/libs/PalmVisualizer.Camera/
// CameraDevice.cs); the original wraps a CodeBrix.Webcam device (CodeBrix.Webcam.LgplLicenseForever + CodeBrix.MediaCore,
// desktop capture with native assets; camera capture is D-O14 NotSupported on Android). Same namespace, type name and
// public members.
namespace PalmVisualizer.Camera;

/// <summary>Stand-in for one connected camera (compile-only).</summary>
public sealed class CameraDevice
{
    internal CameraDevice(string id, string friendlyName)
    {
        Id = id;
        FriendlyName = friendlyName;
    }

    /// <summary>The camera's unique hardware identifier.</summary>
    public string Id { get; }

    /// <summary>The camera's human-readable name.</summary>
    public string FriendlyName { get; }

    /// <summary>The dropdown display text.</summary>
    public override string ToString() => FriendlyName;
}
