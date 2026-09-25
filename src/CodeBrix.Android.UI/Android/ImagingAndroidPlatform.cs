using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Composition.Android;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Xaml.Media;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using ABitmap = global::Android.Graphics.Bitmap;
using ABitmapFactory = global::Android.Graphics.BitmapFactory;
using ACanvas = global::Android.Graphics.Canvas;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IImagingPlatform"/>: encoded images are
/// decoded with android.graphics.BitmapFactory, pixel images are composition surfaces
/// backed by an ARGB_8888 <see cref="ABitmap"/> (<see cref="BitmapCompositionSurfacePlatform"/>),
/// and RenderTargetBitmap draws the element's native view (the handler's platform view).
/// </summary>
/// <remarks>
/// Core pixel formats are BGRA8; an ARGB_8888 bitmap stores RGBA in memory, so every copy
/// swaps the R and B bytes. Skia pictures (<see cref="CreateSurfaceFromPicture"/>) do not
/// exist on Android.
/// </remarks>
internal sealed class ImagingAndroidPlatform : IImagingPlatform
{
    /// <summary>No browser decoder on Android: always null (the Core then decodes itself).</summary>
    public Task<ImageData?> TryDecodeWithBrowserAsync(byte[] encodedImage) => Task.FromResult<ImageData?>(null);

    /// <inheritdoc />
    public (int, int) GetEncodedImageSize(Stream encodedImage)
    {
        var bytes = ReadAll(encodedImage);
        using var options = new ABitmapFactory.Options { InJustDecodeBounds = true };
        ABitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        return (Math.Max(0, options.OutWidth), Math.Max(0, options.OutHeight));
    }

    /// <summary>Decodes to straight (not premultiplied) BGRA8 rows of <paramref name="rowBytes"/> bytes.</summary>
    public void DecodeToBgra8(Stream encodedImage, Span<byte> destination, int rowBytes)
    {
        var bytes = ReadAll(encodedImage);
        using var options = new ABitmapFactory.Options { InPreferredConfig = ABitmap.Config.Argb8888, InPremultiplied = false };
        using var bitmap = ABitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options)
            ?? throw new InvalidOperationException("The image could not be decoded.");
        var rgba = new byte[bitmap.Width * bitmap.Height * 4];
        using (var buffer = Java.Nio.ByteBuffer.Wrap(rgba))
        {
            bitmap.CopyPixelsToBuffer(buffer);
        }

        CopyRowsSwappingRB(rgba, bitmap.Width, bitmap.Height, destination, rowBytes);
        bitmap.Recycle();
    }

    /// <inheritdoc />
    public ImageData CreateImageFromBgra8Premul(IntPtr pixels, int width, int height)
    {
        var length = Math.Max(0, width) * Math.Max(0, height) * 4;
        var managed = new byte[length];
        if (length > 0 && pixels != IntPtr.Zero)
        {
            Marshal.Copy(pixels, managed, 0, length);
        }

        var surface = new PlatformCompositionSurface();
        surface.CopyPixels(width, height, managed);
        return ImageData.FromCompositionSurface(surface);
    }

    /// <summary>
    /// Draws the element's native view (its handler's platform view) into a BGRA8
    /// premultiplied buffer, optionally scaled; returns (buffer length, width, height).
    /// An element without a native view renders as a transparent image of its size.
    /// </summary>
    public (int, int, int) RenderToBgra8Premul(UIElement element, ref RenderTargetBitmap.UnmanagedArrayOfBytes buffer, Size? scaledSize)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
        var view = element.Handler?.PlatformView as AView;
        var width = view is { Width: > 0 } ? view.Width : (int)Math.Ceiling(element.RenderSize.Width * scale);
        var height = view is { Height: > 0 } ? view.Height : (int)Math.Ceiling(element.RenderSize.Height * scale);
        if (scaledSize is { Width: > 0, Height: > 0 } target)
        {
            width = (int)Math.Round(target.Width);
            height = (int)Math.Round(target.Height);
        }

        if (width <= 0 || height <= 0)
        {
            return (0, 0, 0);
        }

        using var bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        if (view is { Width: > 0, Height: > 0 })
        {
            using var canvas = new ACanvas(bitmap);
            canvas.Scale((float)width / view.Width, (float)height / view.Height);
            view.Draw(canvas);
        }

        var length = width * height * 4;
        var rgba = new byte[length];
        using (var javaBuffer = Java.Nio.ByteBuffer.Wrap(rgba))
        {
            bitmap.CopyPixelsToBuffer(javaBuffer);
        }

        bitmap.Recycle();
        SwapRB(rgba);
        RenderTargetBitmap.EnsureBuffer(ref buffer, length);
        Marshal.Copy(rgba, 0, buffer.Pointer, length);
        return (length, width, height);
    }

    /// <summary>Skia pictures do not exist on Android.</summary>
    public PlatformCompositionSurface CreateSurfaceFromPicture(object picture, Size size) =>
        throw new NotSupportedException("A Skia picture cannot be turned into an image on Android.");

    /// <inheritdoc />
    public bool TryGetImageSize(PlatformCompositionSurface surface, out int width, out int height)
    {
        if (surface?.Platform is BitmapCompositionSurfacePlatform { Bitmap: { } bitmap })
        {
            width = bitmap.Width;
            height = bitmap.Height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    /// <inheritdoc />
    public void DisposeImage(PlatformCompositionSurface surface) => surface?.Platform?.Dispose();

    /// <summary>Copies tightly packed RGBA rows into BGRA rows of <paramref name="rowBytes"/> bytes.</summary>
    internal static void CopyRowsSwappingRB(byte[] rgba, int width, int height, Span<byte> destination, int rowBytes)
    {
        var sourceRow = width * 4;
        for (var y = 0; y < height; y++)
        {
            var d = y * rowBytes;
            var s = y * sourceRow;
            if (d + sourceRow > destination.Length)
            {
                break;
            }

            for (var x = 0; x < sourceRow; x += 4)
            {
                destination[d + x] = rgba[s + x + 2];
                destination[d + x + 1] = rgba[s + x + 1];
                destination[d + x + 2] = rgba[s + x];
                destination[d + x + 3] = rgba[s + x + 3];
            }
        }
    }

    private static void SwapRB(byte[] pixels)
    {
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
    }

    private static byte[] ReadAll(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return memory.ToArray();
    }
}
