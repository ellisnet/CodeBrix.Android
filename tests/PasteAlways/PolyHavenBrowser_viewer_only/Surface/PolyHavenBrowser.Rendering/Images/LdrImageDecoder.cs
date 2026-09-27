using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using SkiaSharp;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Decoder for standard low-dynamic-range images (JPEG, PNG, WebP, and the other formats
/// CodeBrix.Imaging supports), producing an <see cref="SKBitmap"/> ready for display.
/// </summary>
public static class LdrImageDecoder
{
    /// <summary>Decodes an image from a byte buffer into an RGBA <see cref="SKBitmap"/>.</summary>
    /// <exception cref="InvalidDataException">The data is not a decodable image.</exception>
    public static SKBitmap Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return Decode(stream);
    }

    /// <summary>Decodes an image from a stream into an RGBA <see cref="SKBitmap"/>.</summary>
    /// <exception cref="InvalidDataException">The data is not a decodable image.</exception>
    public static unsafe SKBitmap Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Image<Rgba32> image;
        try
        {
            image = Image.Load<Rgba32>(stream);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidDataException("The data is not a decodable image.", ex);
        }

        using (image)
        {
            var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            var destination = new Span<byte>((void*)bitmap.GetPixels(), bitmap.ByteCount);
            image.CopyPixelDataTo(destination);
            return bitmap;
        }
    }

    /// <summary>
    /// Decodes an image from a byte buffer into a raw RGBA byte array (4 bytes per pixel,
    /// row-major, top-left origin) — the form GPU texture uploads want.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a decodable image.</exception>
    public static (byte[] Rgba, int Width, int Height) DecodeToRgbaBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);

        Image<Rgba32> image;
        try
        {
            image = Image.Load<Rgba32>(stream);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidDataException("The data is not a decodable image.", ex);
        }

        using (image)
        {
            var rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);
            return (rgba, image.Width, image.Height);
        }
    }
}
