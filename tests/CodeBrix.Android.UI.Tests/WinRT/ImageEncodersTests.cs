using System;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Windows.Graphics.Imaging;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class ImageEncodersTests
{
    [Fact]
    public void Encoder_ids_match_the_core_bitmap_encoder_ids()
    {
        //Assert
        ImageEncoders.PngEncoderId.Should().Be(BitmapEncoder.PngEncoderId);
        ImageEncoders.JpegEncoderId.Should().Be(BitmapEncoder.JpegEncoderId);
    }

    [Fact]
    public void FromEncoderId_maps_png_and_jpeg_and_nothing_else()
    {
        //Assert
        ImageEncoders.FromEncoderId(BitmapEncoder.PngEncoderId).Should().Be(ImageEncoderKind.Png);
        ImageEncoders.FromEncoderId(BitmapEncoder.JpegEncoderId).Should().Be(ImageEncoderKind.Jpeg);
        ImageEncoders.FromEncoderId(BitmapEncoder.BmpEncoderId).Should().Be(ImageEncoderKind.None);
        ImageEncoders.FromEncoderId(Guid.Empty).Should().Be(ImageEncoderKind.None);
    }
}
