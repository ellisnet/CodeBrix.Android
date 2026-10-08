using System;

namespace WebcamViewer.Cameras;

/// <summary>One live video frame from an <see cref="ICameraSession"/>.</summary>
public sealed class CameraFrameEventArgs : EventArgs
{
    private readonly Action<byte[]> _copyTo;

    /// <summary>Describes a frame whose pixels <paramref name="copyTo"/> writes out.</summary>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="copyTo">Copies the frame, tightly packed BGRA, into a buffer of at least width * height * 4 bytes.</param>
    public CameraFrameEventArgs(int width, int height, Action<byte[]> copyTo)
    {
        Width = width;
        Height = height;
        _copyTo = copyTo;
    }

    /// <summary>The frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>The frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>Copies the frame, tightly packed BGRA, into <paramref name="destination"/>.</summary>
    /// <param name="destination">A buffer of at least <see cref="Width"/> * <see cref="Height"/> * 4 bytes.</param>
    public void CopyTo(byte[] destination) => _copyTo(destination);
}
