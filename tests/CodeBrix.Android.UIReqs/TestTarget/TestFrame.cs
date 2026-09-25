using System;
using System.IO;
using CodeBrix.Android.UIReqs.Protocol;

namespace CodeBrix.Android.UIReqs.TestTarget;

/// <summary>The panel orientation of a run (the runner script rotates the emulator to it).</summary>
public enum TestDisplayOrientation
{
    /// <summary>Wider than tall.</summary>
    Landscape,

    /// <summary>Taller than wide.</summary>
    Portrait,
}

/// <summary>
/// A frame the host captured with <c>adb exec-out screencap</c>, cropped to the app window
/// (straight RGBA). The copied FrameArchive / FrameReview save it; the device gets its pixels.
/// </summary>
public sealed class TestFrame
{
    /// <summary>Creates a frame.</summary>
    public TestFrame(byte[] rgba, int width, int height, long sequence)
    {
        Rgba = rgba ?? throw new ArgumentNullException(nameof(rgba));
        Width = width;
        Height = height;
        Sequence = sequence;
    }

    /// <summary>The pixels (RGBA rows).</summary>
    public byte[] Rgba { get; }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>The capture number (the device's).</summary>
    public long Sequence { get; }

    /// <summary>Saves the frame as a PNG (the folder is created).</summary>
    public void SavePng(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, PngCodec.Encode(Rgba, Width, Height));
    }
}
