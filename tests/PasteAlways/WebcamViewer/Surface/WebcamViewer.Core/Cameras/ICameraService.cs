using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Webcam.Devices;

namespace WebcamViewer.Cameras;

/// <summary>
/// The seam in front of the camera hardware: lists the connected cameras and opens a live
/// session on one of them. The application registers <see cref="WebcamCameraService"/>, which
/// is CodeBrix.Webcam itself; an alternate host may register a different implementation.
/// </summary>
public interface ICameraService
{
    /// <summary>Lists the connected cameras; an empty list when there are none.</summary>
    /// <returns>The discovered cameras.</returns>
    Task<IReadOnlyList<IImagingMediaDevice>> GetCamerasAsync();

    /// <summary>Creates a session on <paramref name="device"/>; it does not open the camera until <see cref="ICameraSession.Start"/>.</summary>
    /// <param name="device">A camera returned by <see cref="GetCamerasAsync"/>.</param>
    /// <returns>The new, not yet started session.</returns>
    ICameraSession OpenSession(IImagingMediaDevice device);
}
