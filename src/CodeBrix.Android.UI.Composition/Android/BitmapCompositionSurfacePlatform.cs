using System;
using System.IO;
using CodeBrix.Platform.UI.Composition.Contracts;
using ABitmap = global::Android.Graphics.Bitmap;
using ABitmapFactory = global::Android.Graphics.BitmapFactory;
using AByteBuffer = Java.Nio.ByteBuffer;

namespace CodeBrix.Android.UI.Composition.Android;

/// <summary>
/// The per-surface platform object on Android: a composition surface (the pixels of a
/// decoded image, a WriteableBitmap or a RenderTargetBitmap) is an
/// <see cref="ABitmap"/> in ARGB_8888 (premultiplied). Nothing is painted from it by
/// composition (D-P2); image handlers read <see cref="Bitmap"/>.
/// </summary>
internal sealed class BitmapCompositionSurfacePlatform : ICompositionSurfacePlatform
{
    /// <summary>Gets the surface's pixels, or null before a successful load/copy or after disposal.</summary>
    internal ABitmap Bitmap { get; private set; }

    /// <inheritdoc />
    public (bool success, object nativeResult) LoadFromStream(int? targetWidth, int? targetHeight, Stream imageStream)
    {
        if (imageStream == null)
        {
            return (false, null);
        }

        try
        {
            var bytes = ReadAll(imageStream);
            var bitmap = Decode(bytes, targetWidth, targetHeight);
            if (bitmap == null)
            {
                return (false, null);
            }

            Replace(bitmap);
            return (true, bitmap);
        }
        catch (Exception exception) when (exception is Java.Lang.Throwable or IOException or ArgumentException)
        {
            return (false, exception);
        }
    }

    /// <summary>
    /// Stores BGRA8 premultiplied pixels (the Core pixel format) as an ARGB_8888 bitmap,
    /// whose memory order is RGBA premultiplied.
    /// </summary>
    public void CopyPixels(int pixelWidth, int pixelHeight, ReadOnlyMemory<byte> data)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            Replace(null);
            return;
        }

        var rgba = new byte[pixelWidth * pixelHeight * 4];
        var source = data.Span;
        var count = Math.Min(rgba.Length, source.Length);
        for (var i = 0; i + 3 < count; i += 4)
        {
            rgba[i] = source[i + 2];
            rgba[i + 1] = source[i + 1];
            rgba[i + 2] = source[i];
            rgba[i + 3] = source[i + 3];
        }

        var bitmap = ABitmap.CreateBitmap(pixelWidth, pixelHeight, ABitmap.Config.Argb8888);
        bitmap.SetPremultiplied(true);
        using (var buffer = AByteBuffer.Wrap(rgba))
        {
            bitmap.CopyPixelsFromBuffer(buffer);
        }

        Replace(bitmap);
    }

    /// <inheritdoc />
    public void Dispose() => Replace(null);

    /// <summary>
    /// Decodes an encoded image; with a target size the decode is subsampled (power of
    /// two) and then scaled to exactly that size, keeping the aspect ratio when only one
    /// dimension is given.
    /// </summary>
    internal static ABitmap Decode(byte[] bytes, int? targetWidth, int? targetHeight)
    {
        using var bounds = new ABitmapFactory.Options { InJustDecodeBounds = true };
        ABitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        {
            return null;
        }

        var (width, height) = TargetSize(bounds.OutWidth, bounds.OutHeight, targetWidth, targetHeight);
        var sample = 1;
        while (bounds.OutWidth / (sample * 2) >= width && bounds.OutHeight / (sample * 2) >= height)
        {
            sample *= 2;
        }

        using var options = new ABitmapFactory.Options { InSampleSize = sample, InPreferredConfig = ABitmap.Config.Argb8888 };
        var decoded = ABitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        if (decoded == null || (decoded.Width == width && decoded.Height == height))
        {
            return decoded;
        }

        var scaled = ABitmap.CreateScaledBitmap(decoded, width, height, filter: true);
        if (!ReferenceEquals(scaled, decoded))
        {
            decoded.Recycle();
            decoded.Dispose();
        }

        return scaled;
    }

    /// <summary>The decode size for a requested target (either dimension may be missing).</summary>
    internal static (int Width, int Height) TargetSize(int width, int height, int? targetWidth, int? targetHeight)
    {
        if (targetWidth is > 0 && targetHeight is > 0)
        {
            return (targetWidth.Value, targetHeight.Value);
        }

        if (targetWidth is > 0)
        {
            return (targetWidth.Value, Math.Max(1, (int)Math.Round((double)height * targetWidth.Value / width)));
        }

        if (targetHeight is > 0)
        {
            return (Math.Max(1, (int)Math.Round((double)width * targetHeight.Value / height)), targetHeight.Value);
        }

        return (width, height);
    }

    private static byte[] ReadAll(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private void Replace(ABitmap bitmap)
    {
        var old = Bitmap;
        Bitmap = bitmap;
        if (old != null && !ReferenceEquals(old, bitmap))
        {
            Release(old);
        }
    }

    /// <summary>
    /// Lets go of a bitmap this surface no longer shows: the managed peer is disposed (its global reference
    /// released) but the bitmap is NOT recycled - a native Image view may still be showing it (AP3a: the
    /// Image handler hands the same bitmap to an ImageView, and a recycled bitmap in a view aborts the next
    /// draw). The pixels are freed by the Android runtime once nothing references the bitmap (API 26+:
    /// native allocation registry). The peer may already be disposed: a decoded bitmap is also handed to
    /// Core as the load's native result, and the surface's Dispose runs from Core's finalizer
    /// (PlatformCompositionSurface.Finalize) - nothing is left to release then (AP2 FIX, fenced by the UIReqs
    /// Image scenarios).
    /// </summary>
    internal static void Release(ABitmap bitmap)
    {
        if (bitmap == null || bitmap.Handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            bitmap.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Disposed concurrently (another finalizer): nothing to release.
        }
    }
}
