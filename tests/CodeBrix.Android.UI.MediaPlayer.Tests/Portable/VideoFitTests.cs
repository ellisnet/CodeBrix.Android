using CodeBrix.Android.UI.MediaPlayer.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.MediaPlayer.Tests.Portable;

public class VideoFitTests
{
    [Fact]
    public void Uniform_fits_a_tall_picture_inside_a_wide_box_with_bars_left_and_right()
    {
        //Arrange
        //Act
        var rect = VideoFit.Destination(360, 640, 800, 450, VideoStretch.Uniform);

        //Assert
        rect.Should().Be((273.4375, 0.0, 253.125, 450.0));
    }

    [Fact]
    public void UniformToFill_covers_the_box_and_cuts_the_picture_s_edges()
    {
        //Arrange
        //Act
        var rect = VideoFit.Destination(360, 640, 800, 450, VideoStretch.UniformToFill);

        //Assert
        rect.Width.Should().Be(800);
        rect.Left.Should().Be(0);
        rect.Top.Should().BeLessThan(0);
    }

    [Fact]
    public void Fill_is_the_box_whatever_the_picture()
    {
        //Arrange
        //Act
        var rect = VideoFit.Destination(360, 640, 800, 450, VideoStretch.Fill);

        //Assert
        rect.Should().Be((0.0, 0.0, 800.0, 450.0));
    }

    [Fact]
    public void None_keeps_the_picture_s_size_centred()
    {
        //Arrange
        //Act
        var rect = VideoFit.Destination(200, 100, 800, 450, VideoStretch.None);

        //Assert
        rect.Should().Be((300.0, 175.0, 200.0, 100.0));
    }

    [Fact]
    public void An_unknown_picture_size_fills_the_box()
    {
        //Arrange
        //Act
        var rect = VideoFit.Destination(0, 0, 800, 450, VideoStretch.Uniform);

        //Assert
        rect.Should().Be((0.0, 0.0, 800.0, 450.0));
    }
}
