using System;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Portable;

/// <summary>
/// The pixel arithmetic of the GL read-back (pure, host-free tested).
/// <para>
/// GLCanvasElement (the add-in Core) fills its back buffer with BGRA bytes, the byte order of a WriteableBitmap.
/// Since pin 1.0.268.12 (WPE1-1 C0f) the Core picks the read-back per context itself - GL_BGRA where the driver
/// has it (desktop GL, OpenGL ES with EXT_read_format_bgra), else GL_RGBA plus a red/blue swap - so the Android
/// context hands the Core the driver's own <c>glReadPixels</c> (the AP7-A read-back shim is gone).
/// </para>
/// <para>
/// The read-back rows run bottom-up (OpenGL's origin is the bottom left; the Core shows them through a vertically
/// flipped brush). The Android picture is an android.graphics.Bitmap (RGBA bytes, rows top-down):
/// <see cref="BgraBottomUpToRgbaTopDown"/> does both conversions in one pass.
/// </para>
/// </summary>
internal static class GlReadback
{
    /// <summary>Four bytes per pixel (8-bit BGRA / RGBA).</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>
    /// Converts a bottom-up BGRA picture (the Core's back buffer) into a top-down RGBA picture (an Android bitmap's
    /// bytes).
    /// </summary>
    /// <param name="source">The BGRA pixels, rows bottom-up, <paramref name="width"/> * 4 bytes per row.</param>
    /// <param name="destination">The RGBA pixels, rows top-down; at least as long as the picture.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="ArgumentException">A buffer is shorter than the picture.</exception>
    internal static void BgraBottomUpToRgbaTopDown(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var rowBytes = width * BytesPerPixel;
        var total = rowBytes * height;
        if (source.Length < total || destination.Length < total)
        {
            throw new ArgumentException($"A {width}x{height} picture needs {total} bytes (source {source.Length}, destination {destination.Length}).");
        }

        for (var row = 0; row < height; row++)
        {
            var from = source.Slice((height - 1 - row) * rowBytes, rowBytes);
            var to = destination.Slice(row * rowBytes, rowBytes);
            for (var i = 0; i < rowBytes; i += BytesPerPixel)
            {
                to[i] = from[i + 2];
                to[i + 1] = from[i + 1];
                to[i + 2] = from[i];
                to[i + 3] = from[i + 3];
            }
        }
    }
}
