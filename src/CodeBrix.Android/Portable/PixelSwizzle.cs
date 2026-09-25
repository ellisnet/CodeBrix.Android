using System;

namespace CodeBrix.Android.Portable;

/// <summary>
/// Byte-order conversion between BGRA8 (the WinUI/Core pixel order) and RGBA8 (the byte
/// order of an Android ARGB_8888 bitmap's pixel buffer).
/// </summary>
internal static class PixelSwizzle
{
    /// <summary>
    /// Swaps the first and third byte of every 4-byte pixel (BGRA &lt;-&gt; RGBA), in place.
    /// The conversion is its own inverse.
    /// </summary>
    internal static void SwapRedBlue(Span<byte> pixels)
    {
        if (pixels.Length % 4 != 0)
        {
            throw new ArgumentException("The pixel buffer length must be a multiple of 4.", nameof(pixels));
        }

        for (var i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
    }

    /// <summary>Copies <paramref name="source"/> into <paramref name="destination"/> swapping red and blue.</summary>
    internal static void CopySwapRedBlue(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length % 4 != 0)
        {
            throw new ArgumentException("The pixel buffer length must be a multiple of 4.", nameof(source));
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentException("The destination is smaller than the source.", nameof(destination));
        }

        for (var i = 0; i < source.Length; i += 4)
        {
            destination[i] = source[i + 2];
            destination[i + 1] = source[i + 1];
            destination[i + 2] = source[i];
            destination[i + 3] = source[i + 3];
        }
    }
}
