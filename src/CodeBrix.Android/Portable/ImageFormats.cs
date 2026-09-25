namespace CodeBrix.Android.Services;

/// <summary>Recognizes an encoded image from its first bytes (for file names and MIME types of shared images).</summary>
internal static class ImageFormats
{
    /// <summary>The MIME type of the encoded image, or "application/octet-stream".</summary>
    /// <param name="bytes">The encoded bytes.</param>
    internal static string MimeTypeOf(byte[] bytes)
    {
        if (bytes is { Length: >= 8 } && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes is { Length: >= 3 } && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes is { Length: >= 6 } && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
        {
            return "image/gif";
        }

        if (bytes is { Length: >= 2 } && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
        {
            return "image/bmp";
        }

        if (bytes is { Length: >= 12 } && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return "image/webp";
        }

        return "application/octet-stream";
    }

    /// <summary>A file name with the extension of the encoded image (".bin" when unknown).</summary>
    /// <param name="bytes">The encoded bytes.</param>
    /// <param name="stem">The name without extension.</param>
    internal static string FileNameFor(byte[] bytes, string stem) => stem + MimeTypeOf(bytes) switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/webp" => ".webp",
        _ => ".bin",
    };
}
