// STUB (paste always): PolyHavenBrowser.Rendering.ExrDecoder (CodeBrix.Samples PolyHavenBrowser_viewer_only
// src/libs/PolyHavenBrowser.Rendering/Images/ExrDecoder.cs); the original decodes through TinyEXR.NET, a third-party
// package the paste-always heads may not take. Same namespace, type name and public members.
namespace PolyHavenBrowser.Rendering;

/// <summary>Stand-in for the OpenEXR decoder (compile-only).</summary>
public static class ExrDecoder
{
    /// <summary>Decodes an OpenEXR file from a byte buffer (not supported here).</summary>
    public static FloatImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        throw new InvalidDataException("OpenEXR decoding is not available in this build.");
    }

    /// <summary>Decodes an OpenEXR file from a stream (not supported here).</summary>
    public static FloatImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new InvalidDataException("OpenEXR decoding is not available in this build.");
    }
}
