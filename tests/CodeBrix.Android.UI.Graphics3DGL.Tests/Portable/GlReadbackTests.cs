using System;
using CodeBrix.Android.UI.Graphics3DGL.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Graphics3DGL.Tests.Portable;

public class GlReadbackTests
{
    [Fact]
    public void A_bottom_up_BGRA_picture_becomes_a_top_down_RGBA_picture()
    {
        //Arrange
        // 1 x 2: the bottom row (first in memory) is blue, the top row is red; both BGRA.
        var bgra = new byte[] { 255, 0, 0, 255, 0, 0, 255, 128 };
        var rgba = new byte[8];

        //Act
        GlReadback.BgraBottomUpToRgbaTopDown(bgra, rgba, 1, 2);

        //Assert
        rgba.Should().Equal(255, 0, 0, 128, 0, 0, 255, 255);
    }

    [Fact]
    public void A_buffer_shorter_than_the_picture_is_refused()
    {
        //Arrange
        var bgra = new byte[4];
        var rgba = new byte[8];

        //Act
        var act = () => GlReadback.BgraBottomUpToRgbaTopDown(bgra, rgba, 1, 2);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_empty_picture_converts_nothing()
    {
        //Arrange
        var rgba = new byte[] { 7, 7, 7, 7 };

        //Act
        GlReadback.BgraBottomUpToRgbaTopDown([], rgba, 0, 5);

        //Assert
        rgba.Should().Equal(7, 7, 7, 7);
    }
}
