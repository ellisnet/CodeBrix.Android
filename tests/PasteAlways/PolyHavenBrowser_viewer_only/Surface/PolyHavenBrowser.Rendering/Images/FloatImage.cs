namespace PolyHavenBrowser.Rendering;

/// <summary>
/// A linear-light floating-point RGB image (3 floats per pixel, row-major, top-left origin).
/// This is the common in-memory form for HDR content (Radiance <c>.hdr</c> and OpenEXR
/// <c>.exr</c> files) before tone mapping for display.
/// </summary>
public sealed class FloatImage
{
    /// <summary>Creates an image over an existing RGB pixel buffer (not copied).</summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixels">The pixel data: 3 floats (R, G, B) per pixel, row-major from the top-left.</param>
    public FloatImage(int width, int height, float[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height * 3)
        {
            throw new ArgumentException(
                $"Pixel buffer length {pixels.Length} does not match {width}x{height} RGB ({width * height * 3} floats).",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>The image width in pixels.</summary>
    public int Width { get; }

    /// <summary>The image height in pixels.</summary>
    public int Height { get; }

    /// <summary>The pixel data: 3 floats (R, G, B) per pixel, row-major from the top-left.</summary>
    public float[] Pixels { get; }

    /// <summary>Reads the pixel at (<paramref name="x"/>, <paramref name="y"/>) without bounds checks beyond the array's own.</summary>
    public void GetPixel(int x, int y, out float r, out float g, out float b)
    {
        var i = (y * Width + x) * 3;
        r = Pixels[i];
        g = Pixels[i + 1];
        b = Pixels[i + 2];
    }

    /// <summary>
    /// Creates a <see cref="FloatImage"/> from an RGBA float buffer (4 floats per pixel),
    /// dropping the alpha channel. Used for OpenEXR data, which TinyEXR returns as RGBA.
    /// </summary>
    public static FloatImage FromRgba(float[] rgbaPixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgbaPixels);
        if (rgbaPixels.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"Pixel buffer length {rgbaPixels.Length} does not match {width}x{height} RGBA ({width * height * 4} floats).",
                nameof(rgbaPixels));
        }

        var rgb = new float[width * height * 3];
        for (int src = 0, dst = 0; src < rgbaPixels.Length; src += 4, dst += 3)
        {
            rgb[dst] = rgbaPixels[src];
            rgb[dst + 1] = rgbaPixels[src + 1];
            rgb[dst + 2] = rgbaPixels[src + 2];
        }

        return new FloatImage(width, height, rgb);
    }
}
