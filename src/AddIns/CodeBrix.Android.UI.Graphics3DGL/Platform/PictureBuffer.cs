using System;
using CodeBrix.Android.UI.Graphics3DGL.Portable;
using ABitmap = global::Android.Graphics.Bitmap;
using AByteBuffer = Java.Nio.ByteBuffer;

namespace CodeBrix.Android.UI.Graphics3DGL.Platform;

/// <summary>
/// An android.graphics.Bitmap that follows a read-back picture: re-created when the size changes, refilled from the
/// Core's bottom-up BGRA bytes (<see cref="GlReadback.BgraBottomUpToRgbaTopDown"/>).
/// </summary>
internal sealed class PictureBuffer : IDisposable
{
    private byte[] _rgba = [];

    /// <summary>The bitmap (null until the first picture).</summary>
    internal ABitmap Bitmap { get; private set; }

    /// <summary>Fills the bitmap from a bottom-up BGRA picture.</summary>
    /// <param name="bgra">The BGRA bytes (rows bottom-up).</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void FillFromBgraBottomUp(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        EnsureSize(width, height);
        var total = width * height * GlReadback.BytesPerPixel;
        if (_rgba.Length != total)
        {
            _rgba = new byte[total];
        }

        GlReadback.BgraBottomUpToRgbaTopDown(bgra, _rgba, width, height);
        using var buffer = AByteBuffer.Wrap(_rgba);
        Bitmap.CopyPixelsFromBuffer(buffer);
    }

    /// <summary>Makes sure the bitmap is <paramref name="width"/> x <paramref name="height"/> (8-bit RGBA).</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The bitmap.</returns>
    internal ABitmap EnsureSize(int width, int height)
    {
        if (Bitmap is { IsRecycled: false } bitmap && bitmap.Width == width && bitmap.Height == height)
        {
            return bitmap;
        }

        Free();
        Bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        return Bitmap;
    }

    /// <summary>Releases the bitmap.</summary>
    internal void Free()
    {
        if (Bitmap is { } bitmap)
        {
            Bitmap = null;
            if (!bitmap.IsRecycled)
            {
                bitmap.Recycle();
            }

            bitmap.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Free();
}
