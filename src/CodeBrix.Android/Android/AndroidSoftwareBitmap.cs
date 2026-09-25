using Windows.Graphics.Imaging;
using ABitmap = global::Android.Graphics.Bitmap;

namespace CodeBrix.Android.Android;

/// <summary>
/// The platform bitmap object behind a SoftwareBitmap on Android: an ARGB_8888
/// <see cref="ABitmap"/> (whose pixel buffer is RGBA byte order) plus the WinRT pixel
/// format and alpha mode it was created with (the Android bitmap does not know BGRA).
/// </summary>
internal sealed class AndroidSoftwareBitmap
{
    internal AndroidSoftwareBitmap(ABitmap bitmap, BitmapPixelFormat format, BitmapAlphaMode alphaMode)
    {
        Bitmap = bitmap;
        Format = format;
        AlphaMode = alphaMode;
    }

    /// <summary>The Android bitmap (ARGB_8888).</summary>
    internal ABitmap Bitmap { get; private set; }

    /// <summary>The WinRT pixel format (Bgra8 or Rgba8).</summary>
    internal BitmapPixelFormat Format { get; }

    /// <summary>The WinRT alpha mode.</summary>
    internal BitmapAlphaMode AlphaMode { get; }

    /// <summary>Releases the Android bitmap.</summary>
    internal void Dispose()
    {
        var bitmap = Bitmap;
        Bitmap = null;
        if (bitmap != null)
        {
            if (!bitmap.IsRecycled)
            {
                bitmap.Recycle();
            }

            bitmap.Dispose();
        }
    }
}
