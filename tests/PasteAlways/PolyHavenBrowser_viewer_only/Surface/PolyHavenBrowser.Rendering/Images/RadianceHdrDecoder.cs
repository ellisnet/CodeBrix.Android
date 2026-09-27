using System.Text;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Decoder for Radiance RGBE images (<c>.hdr</c>) — the primary HDRI format served by
/// Poly Haven. Produces linear-light RGB floats.
/// </summary>
/// <remarks>
/// The decoding logic (header handling, old-style and new-style RLE scanlines, and the
/// RGBE-to-float conversion) is derived from the public-domain <c>stb_image.h</c> HDR
/// loader (https://github.com/nothings/stb, also available in C# as StbImageSharp),
/// rewritten here in idiomatic C#. stb_image is public domain / MIT-0; no attribution is
/// required, this note is a provenance pointer for future maintenance.
/// </remarks>
public static class RadianceHdrDecoder
{
    /// <summary>Decodes a Radiance <c>.hdr</c> file from a byte buffer.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid Radiance RGBE image.</exception>
    public static FloatImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return Decode(stream);
    }

    /// <summary>Decodes a Radiance <c>.hdr</c> file from a stream.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid Radiance RGBE image.</exception>
    public static FloatImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BufferedStream(stream);

        // ---- Header: signature line, then KEY=VALUE lines until a blank line. ----
        var signature = ReadLine(reader);
        if (signature is not ("#?RADIANCE" or "#?RGBE"))
        {
            throw new InvalidDataException("Not a Radiance RGBE (.hdr) file: missing #?RADIANCE signature.");
        }

        var formatSeen = false;
        while (true)
        {
            var line = ReadLine(reader) ?? throw new InvalidDataException("Unexpected end of file in .hdr header.");
            if (line.Length == 0)
            {
                break; // end of header
            }
            if (line.StartsWith("FORMAT=", StringComparison.Ordinal))
            {
                if (line != "FORMAT=32-bit_rle_rgbe")
                {
                    throw new InvalidDataException($"Unsupported .hdr format '{line}': only 32-bit_rle_rgbe is supported.");
                }
                formatSeen = true;
            }
            // Other header lines (EXPOSURE, comments, ...) are ignored for display purposes.
        }

        if (!formatSeen)
        {
            throw new InvalidDataException("Invalid .hdr header: no FORMAT line.");
        }

        // ---- Resolution line: standard orientation is "-Y <height> +X <width>". ----
        var resolution = ReadLine(reader) ?? throw new InvalidDataException("Unexpected end of file at .hdr resolution line.");
        var parts = resolution.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "-Y" || parts[2] != "+X"
            || !int.TryParse(parts[1], out var height) || !int.TryParse(parts[3], out var width)
            || width < 1 || height < 1)
        {
            throw new InvalidDataException($"Unsupported .hdr resolution line '{resolution}': only '-Y H +X W' orientation is supported.");
        }

        var pixels = new float[width * height * 3];
        var scanline = new byte[width * 4]; // interleaved RGBE for one row

        for (var y = 0; y < height; y++)
        {
            ReadScanline(reader, scanline, width);

            var row = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                RgbeToFloat(
                    scanline[x * 4], scanline[x * 4 + 1], scanline[x * 4 + 2], scanline[x * 4 + 3],
                    out pixels[row + x * 3], out pixels[row + x * 3 + 1], out pixels[row + x * 3 + 2]);
            }
        }

        return new FloatImage(width, height, pixels);
    }

    /// <summary>Converts one RGBE pixel to linear floats: value = mantissa * 2^(e-136).</summary>
    internal static void RgbeToFloat(byte r, byte g, byte b, byte e, out float rf, out float gf, out float bf)
    {
        if (e == 0)
        {
            rf = gf = bf = 0f;
            return;
        }

        var scale = MathF.Pow(2f, e - 136); // e - (128 + 8): shared exponent over 8-bit mantissas
        rf = r * scale;
        gf = g * scale;
        bf = b * scale;
    }

    private static void ReadScanline(Stream stream, byte[] scanline, int width)
    {
        var b0 = ReadByteChecked(stream);
        var b1 = ReadByteChecked(stream);
        var b2 = ReadByteChecked(stream);
        var b3 = ReadByteChecked(stream);

        // New-style RLE scanline: starts 0x02 0x02, then the 16-bit width, then the four
        // components run-length encoded planar (all R bytes, all G, all B, all E).
        // Only used for widths in [8, 32767], mirroring the reference encoder.
        if (b0 == 2 && b1 == 2 && (b2 & 0x80) == 0 && ((b2 << 8) | b3) == width && width >= 8)
        {
            for (var component = 0; component < 4; component++)
            {
                var x = 0;
                while (x < width)
                {
                    var count = ReadByteChecked(stream);
                    if (count > 128)
                    {
                        // Run: repeat the next byte (count - 128) times.
                        var value = ReadByteChecked(stream);
                        count -= 128;
                        if (x + count > width)
                        {
                            throw new InvalidDataException("Corrupt .hdr RLE scanline: run overflows the row.");
                        }
                        for (var i = 0; i < count; i++)
                        {
                            scanline[(x + i) * 4 + component] = value;
                        }
                    }
                    else
                    {
                        // Literal: the next count bytes verbatim.
                        if (count == 0 || x + count > width)
                        {
                            throw new InvalidDataException("Corrupt .hdr RLE scanline: literal overflows the row.");
                        }
                        for (var i = 0; i < count; i++)
                        {
                            scanline[(x + i) * 4 + component] = ReadByteChecked(stream);
                        }
                    }

                    x += count;
                }
            }

            return;
        }

        // Flat / old-style scanline: interleaved RGBE quads, where a quad of (1,1,1,n)
        // means "repeat the previous pixel n << (8 * consecutiveRepeatQuads) times".
        var shift = 0;
        var pixel = 0;
        while (pixel < width)
        {
            if (pixel > 0 || shift > 0)
            {
                b0 = ReadByteChecked(stream);
                b1 = ReadByteChecked(stream);
                b2 = ReadByteChecked(stream);
                b3 = ReadByteChecked(stream);
            }

            if (b0 == 1 && b1 == 1 && b2 == 1)
            {
                if (pixel == 0)
                {
                    throw new InvalidDataException("Corrupt .hdr scanline: repeat marker with no previous pixel.");
                }

                var repeat = b3 << (8 * shift);
                if (pixel + repeat > width)
                {
                    throw new InvalidDataException("Corrupt .hdr scanline: old-style run overflows the row.");
                }
                for (var i = 0; i < repeat; i++)
                {
                    Array.Copy(scanline, (pixel - 1) * 4, scanline, (pixel + i) * 4, 4);
                }
                pixel += repeat;
                shift++;
            }
            else
            {
                scanline[pixel * 4] = b0;
                scanline[pixel * 4 + 1] = b1;
                scanline[pixel * 4 + 2] = b2;
                scanline[pixel * 4 + 3] = b3;
                pixel++;
                shift = 0;
            }
        }
    }

    private static byte ReadByteChecked(Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new InvalidDataException("Unexpected end of file in .hdr pixel data.");
        }
        return (byte)value;
    }

    private static string? ReadLine(Stream stream)
    {
        var builder = new StringBuilder(64);
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                return builder.Length > 0 ? builder.ToString() : null;
            }
            if (value == '\n')
            {
                return builder.ToString();
            }
            if (value != '\r')
            {
                builder.Append((char)value);
            }
        }
    }
}
