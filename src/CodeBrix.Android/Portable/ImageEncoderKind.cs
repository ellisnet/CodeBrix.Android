using System;

namespace CodeBrix.Android.Portable;

/// <summary>
/// The Android Bitmap.CompressFormat a WinRT BitmapEncoder id maps to.
/// </summary>
internal enum ImageEncoderKind
{
    /// <summary>No Android encoder (BitmapEncoder.IsEncoderSupported is false).</summary>
    None = 0,

    /// <summary>PNG (Bitmap.CompressFormat.Png).</summary>
    Png = 1,

    /// <summary>JPEG (Bitmap.CompressFormat.Jpeg).</summary>
    Jpeg = 2,
}

/// <summary>
/// Maps WinRT BitmapEncoder ids to Android encoders. The ids are the published WinRT
/// constants (BitmapEncoder.PngEncoderId, BitmapEncoder.JpegEncoderId).
/// </summary>
internal static class ImageEncoders
{
    /// <summary>BitmapEncoder.PngEncoderId.</summary>
    internal static readonly Guid PngEncoderId = new("27949969-876a-41d7-9447-568f6a35a4dc");

    /// <summary>BitmapEncoder.JpegEncoderId.</summary>
    internal static readonly Guid JpegEncoderId = new("1a34f5c1-4a5a-46dc-b644-1f4567e7a676");

    /// <summary>JPEG quality used by Encode (WinRT's default image quality is 0.9).</summary>
    internal const int JpegQuality = 90;

    /// <summary>Returns the Android encoder for a WinRT encoder id.</summary>
    internal static ImageEncoderKind FromEncoderId(Guid encoderId)
    {
        if (encoderId == PngEncoderId)
        {
            return ImageEncoderKind.Png;
        }

        if (encoderId == JpegEncoderId)
        {
            return ImageEncoderKind.Jpeg;
        }

        return ImageEncoderKind.None;
    }
}
