using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Webcam;
using CodeBrix.Webcam.Capture;
using CodeBrix.Webcam.Devices;

namespace WebcamViewer.Cameras;

/// <summary>
/// The real cameras: <see cref="WebcamDevices"/> lists them and each session is a
/// <see cref="WebcamSession"/>.
/// </summary>
public sealed class WebcamCameraService : ICameraService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<IImagingMediaDevice>> GetCamerasAsync() => WebcamDevices.GetImagingMediaDeviceListAsync();

    /// <inheritdoc />
    public ICameraSession OpenSession(IImagingMediaDevice device) => new Session(new WebcamSession(device));

    private sealed class Session : ICameraSession
    {
        private readonly WebcamSession _session;

        public Session(WebcamSession session)
        {
            _session = session;
            _session.FrameReceived += OnFrameReceived;
        }

        public event EventHandler<CameraFrameEventArgs> FrameReceived;

        public bool IsAudioCaptureActive => _session.IsAudioCaptureActive;

        public bool MonitorAudio
        {
            get => _session.MonitorAudio;
            set => _session.MonitorAudio = value;
        }

        public void Start() => _session.Start();

        public byte[] CapturePhoto(out int width, out int height)
        {
            WebcamPhoto photo = _session.CapturePhoto();
            width = photo.Width;
            height = photo.Height;
            return photo.PixelsBgra32;
        }

        public void Dispose()
        {
            _session.FrameReceived -= OnFrameReceived;
            _session.Dispose();
        }

        private void OnFrameReceived(object sender, WebcamFrameEventArgs frame)
            => FrameReceived?.Invoke(this, new CameraFrameEventArgs((int)frame.Width, (int)frame.Height, frame.CopyTo));
    }
}
