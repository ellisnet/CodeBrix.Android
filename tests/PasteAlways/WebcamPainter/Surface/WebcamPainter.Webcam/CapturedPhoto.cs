using System;

namespace WebcamPainter.Webcam;

/// <summary>
/// A still photo captured from the live webcam feed, held in memory as raw pixels - the
/// image the user paints on in Paint Mode.
/// </summary>
public sealed class CapturedPhoto
{
    internal CapturedPhoto(byte[] pixelsBgra32, int width, int height)
    {
        PixelsBgra32 = pixelsBgra32;
        Width = width;
        Height = height;
        CapturedAtUtc = DateTime.UtcNow;
    }

    /// <summary>The photo's tightly packed 32-bit BGRA pixels.</summary>
    public byte[] PixelsBgra32 { get; }

    /// <summary>The photo's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The photo's height in pixels.</summary>
    public int Height { get; }

    /// <summary>When the photo was captured (UTC).</summary>
    public DateTime CapturedAtUtc { get; }
}
