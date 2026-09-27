// STUB (paste always): PalmVisualizer.Camera.WebcamCaptureService (CodeBrix.Samples PalmVisualizer/src/libs/
// PalmVisualizer.Camera/WebcamCaptureService.cs); the original captures through CodeBrix.Webcam (desktop capture with
// native assets; camera capture is D-O14 NotSupported on Android). Same namespace, type name and public members; it
// finds no cameras.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PalmVisualizer.Camera;

/// <summary>Stand-in for the webcam capture service (compile-only; finds no cameras).</summary>
public sealed class WebcamCaptureService : IWebcamCaptureService
{
    /// <summary>Lists the connected cameras (none here).</summary>
    public static Task<IReadOnlyList<CameraDevice>> GetCamerasAsync() =>
        Task.FromResult<IReadOnlyList<CameraDevice>>(Array.Empty<CameraDevice>());

    /// <inheritdoc />
    public Task<IReadOnlyList<CameraDevice>> DiscoverCamerasAsync() => GetCamerasAsync();

    /// <inheritdoc />
    public bool IsRunning => false;

    /// <inheritdoc />
    public bool HasFrame => false;

    /// <inheritdoc />
    public event EventHandler FrameArrived { add { } remove { } }

    /// <inheritdoc />
    public void Start(CameraDevice camera)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }
    }

    /// <inheritdoc />
    public void Stop() { }

    /// <inheritdoc />
    public bool TryCopyLatestFrame(ref byte[] buffer, out int width, out int height)
    {
        width = 0;
        height = 0;
        return false;
    }

    /// <inheritdoc />
    public void Dispose() { }
}
