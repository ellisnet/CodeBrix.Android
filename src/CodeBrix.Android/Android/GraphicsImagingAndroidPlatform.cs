using System;
using System.IO;
using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using ABitmap = global::Android.Graphics.Bitmap;
using AByteBuffer = Java.Nio.ByteBuffer;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of <see cref="IGraphicsImagingPlatform"/>: a SoftwareBitmap
/// is an ARGB_8888 <see cref="ABitmap"/> (<see cref="AndroidSoftwareBitmap"/>); BGRA8 pixels
/// are swizzled to the bitmap's RGBA byte order on the way in; PNG and JPEG encoding use
/// Bitmap.Compress. Other pixel formats and encoders are not supported (CreateBitmap*
/// returns null, IsEncoderSupported false), which Core reports as the WinRT errors.
/// </summary>
internal sealed class GraphicsImagingAndroidPlatform : IGraphicsImagingPlatform
{
    /// <inheritdoc />
    public object CreateBitmap(BitmapPixelFormat format, int width, int height) =>
        CreateBitmap(format, width, height, BitmapAlphaMode.Premultiplied);

    /// <inheritdoc />
    public object CreateBitmap(BitmapPixelFormat format, int width, int height, BitmapAlphaMode alphaMode)
    {
        if (!IsSupported(format) || width <= 0 || height <= 0)
        {
            return null;
        }

        var bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        ApplyAlphaMode(bitmap, alphaMode);
        return new AndroidSoftwareBitmap(bitmap, format, alphaMode);
    }

    /// <inheritdoc />
    public object CreateBitmapFromPixels(byte[] pixels, BitmapPixelFormat format, BitmapAlphaMode alphaMode, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (!IsSupported(format) || width <= 0 || height <= 0)
        {
            return null;
        }

        var byteCount = checked(width * height * 4);
        if (pixels.Length < byteCount)
        {
            throw new ArgumentException("The pixel buffer is smaller than width * height * 4.", nameof(pixels));
        }

        var rgba = new byte[byteCount];
        if (format == BitmapPixelFormat.Bgra8)
        {
            PixelSwizzle.CopySwapRedBlue(pixels.AsSpan(0, byteCount), rgba);
        }
        else
        {
            Array.Copy(pixels, rgba, byteCount);
        }

        var bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        ApplyAlphaMode(bitmap, alphaMode);
        using (var buffer = AByteBuffer.Wrap(rgba))
        {
            bitmap.CopyPixelsFromBuffer(buffer);
        }

        return new AndroidSoftwareBitmap(bitmap, format, alphaMode);
    }

    /// <inheritdoc />
    public BitmapPixelFormat GetPixelFormat(object bitmap) => Get(bitmap).Format;

    /// <inheritdoc />
    public BitmapAlphaMode GetAlphaMode(object bitmap) => Get(bitmap).AlphaMode;

    /// <inheritdoc />
    public int GetPixelWidth(object bitmap) => Get(bitmap).Bitmap.Width;

    /// <inheritdoc />
    public int GetPixelHeight(object bitmap) => Get(bitmap).Bitmap.Height;

    /// <inheritdoc />
    public void CopyPixels(object source, object destination)
    {
        var from = Get(source).Bitmap;
        var to = Get(destination).Bitmap;
        if (from.Width != to.Width || from.Height != to.Height)
        {
            throw new ArgumentException("The destination bitmap has a different size.", nameof(destination));
        }

        using var buffer = AByteBuffer.Allocate(from.ByteCount);
        from.CopyPixelsToBuffer(buffer);
        buffer.Rewind();
        to.CopyPixelsFromBuffer(buffer);
    }

    /// <inheritdoc />
    public object CopyBitmap(object source)
    {
        var from = Get(source);
        var copy = from.Bitmap.Copy(ABitmap.Config.Argb8888, isMutable: true);
        return new AndroidSoftwareBitmap(copy, from.Format, from.AlphaMode);
    }

    /// <inheritdoc />
    public void DisposeBitmap(object bitmap)
    {
        if (bitmap is AndroidSoftwareBitmap androidBitmap)
        {
            androidBitmap.Dispose();
        }
    }

    /// <inheritdoc />
    public bool IsEncoderSupported(Guid encoderId) => ImageEncoders.FromEncoderId(encoderId) != ImageEncoderKind.None;

    /// <inheritdoc />
    public void Encode(object bitmap, Guid encoderId, IRandomAccessStream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var kind = ImageEncoders.FromEncoderId(encoderId);
        if (kind == ImageEncoderKind.None)
        {
            throw new NotSupportedException($"The bitmap encoder {encoderId} is not supported on Android.");
        }

        using var encoded = new MemoryStream();
        var format = kind == ImageEncoderKind.Png ? ABitmap.CompressFormat.Png : ABitmap.CompressFormat.Jpeg;
        if (!Get(bitmap).Bitmap.Compress(format, kind == ImageEncoderKind.Png ? 100 : ImageEncoders.JpegQuality, encoded))
        {
            throw new InvalidOperationException("Android failed to encode the bitmap.");
        }

        var target = destination.AsStreamForWrite();
        encoded.Position = 0;
        encoded.CopyTo(target);
        target.Flush();
    }

    private static bool IsSupported(BitmapPixelFormat format) =>
        format == BitmapPixelFormat.Bgra8 || format == BitmapPixelFormat.Rgba8;

    private static void ApplyAlphaMode(ABitmap bitmap, BitmapAlphaMode alphaMode)
    {
        switch (alphaMode)
        {
            case BitmapAlphaMode.Ignore:
                bitmap.HasAlpha = false;
                break;
            case BitmapAlphaMode.Straight:
                bitmap.SetPremultiplied(false);
                break;
            default:
                bitmap.SetPremultiplied(true);
                break;
        }
    }

    private static AndroidSoftwareBitmap Get(object bitmap) =>
        bitmap as AndroidSoftwareBitmap ?? throw new ArgumentException("Not an Android platform bitmap.", nameof(bitmap));
}
