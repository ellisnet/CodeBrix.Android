using System;
using System.IO;
using System.IO.Compression;

namespace CodeBrix.Android.UIReqs.Protocol;

/// <summary>
/// A small managed PNG codec for UIReqs frames (no Skia): writes 8-bit RGBA, reads 8-bit
/// grayscale/RGB/RGBA (with or without alpha), non-interlaced - what screencap and the frame
/// archive produce. Pixels are tightly packed RGBA rows (straight alpha).
/// </summary>
public static class PngCodec
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encodes RGBA pixels as a PNG.</summary>
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        using var output = new MemoryStream();
        output.Write(Signature);

        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR", header);

        using (var raw = new MemoryStream())
        {
            using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
            {
                var stride = width * 4;
                for (var y = 0; y < height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(rgba, y * stride, stride);
                }
            }

            WriteChunk(output, "IDAT", raw.ToArray());
        }

        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    /// <summary>Decodes a PNG into RGBA pixels.</summary>
    public static (byte[] Rgba, int Width, int Height) Decode(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        for (var i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
            {
                throw new InvalidDataException("Not a PNG file.");
            }
        }

        var position = 8;
        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        using var idat = new MemoryStream();
        while (position + 8 <= png.Length)
        {
            var length = (int)ReadBigEndian(png, position);
            var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            var data = position + 8;
            switch (type)
            {
                case "IHDR":
                    width = (int)ReadBigEndian(png, data);
                    height = (int)ReadBigEndian(png, data + 4);
                    bitDepth = png[data + 8];
                    colorType = png[data + 9];
                    interlace = png[data + 12];
                    break;
                case "IDAT":
                    idat.Write(png, data, length);
                    break;
            }

            position = data + length + 4;
            if (type == "IEND")
            {
                break;
            }
        }

        if (bitDepth != 8 || interlace != 0)
        {
            throw new NotSupportedException($"PNG bit depth {bitDepth} / interlace {interlace} is not supported (8-bit, non-interlaced only).");
        }

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"PNG colour type {colorType} is not supported."),
        };

        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        idat.Position = 0;
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
        {
            zlib.ReadExactly(raw);
        }

        var previous = new byte[stride];
        var current = new byte[stride];
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Buffer.BlockCopy(raw, (y * (stride + 1)) + 1, current, 0, stride);
            Unfilter(filter, current, previous, channels);
            for (var x = 0; x < width; x++)
            {
                var o = ((y * width) + x) * 4;
                var i = x * channels;
                switch (channels)
                {
                    case 1:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = current[i];
                        rgba[o + 3] = 255;
                        break;
                    case 2:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = current[i];
                        rgba[o + 3] = current[i + 1];
                        break;
                    case 3:
                        rgba[o] = current[i];
                        rgba[o + 1] = current[i + 1];
                        rgba[o + 2] = current[i + 2];
                        rgba[o + 3] = 255;
                        break;
                    default:
                        rgba[o] = current[i];
                        rgba[o + 1] = current[i + 1];
                        rgba[o + 2] = current[i + 2];
                        rgba[o + 3] = current[i + 3];
                        break;
                }
            }

            (previous, current) = (current, previous);
        }

        return (rgba, width, height);
    }

    private static void Unfilter(byte filter, byte[] line, byte[] previous, int bpp)
    {
        for (var i = 0; i < line.Length; i++)
        {
            int left = i >= bpp ? line[i - bpp] : 0;
            int up = previous[i];
            int upLeft = i >= bpp ? previous[i - bpp] : 0;
            line[i] = filter switch
            {
                0 => line[i],
                1 => (byte)(line[i] + left),
                2 => (byte)(line[i] + up),
                3 => (byte)(line[i] + ((left + up) >> 1)),
                4 => (byte)(line[i] + Paeth(left, up, upLeft)),
                _ => throw new InvalidDataException("Unknown PNG filter " + filter),
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var header = new byte[8];
        WriteBigEndian(header, 0, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, header, 4);
        output.Write(header);
        output.Write(data);
        var crc = Crc(header.AsSpan(4, 4), data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint ReadBigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
}
