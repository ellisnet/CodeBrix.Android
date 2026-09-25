using CodeBrix.Android.Services;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class ImageFormatsTests
{
    [Fact]
    public void A_png_signature_is_a_png()
    {
        //Arrange
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        //Act & Assert
        ImageFormats.MimeTypeOf(bytes).Should().Be("image/png");
        ImageFormats.FileNameFor(bytes, "clipboard").Should().Be("clipboard.png");
    }

    [Fact]
    public void A_jpeg_signature_is_a_jpeg()
    {
        //Act & Assert
        ImageFormats.MimeTypeOf(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }).Should().Be("image/jpeg");
    }

    [Fact]
    public void Unknown_bytes_are_an_octet_stream()
    {
        //Act & Assert
        ImageFormats.MimeTypeOf(new byte[] { 1, 2, 3 }).Should().Be("application/octet-stream");
        ImageFormats.FileNameFor(new byte[] { 1, 2, 3 }, "shared").Should().Be("shared.bin");
    }
}
