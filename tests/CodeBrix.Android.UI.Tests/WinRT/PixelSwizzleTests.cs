using System;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class PixelSwizzleTests
{
    [Fact]
    public void SwapRedBlue_converts_bgra_to_rgba_in_place()
    {
        //Arrange
        var pixels = new byte[] { 1, 2, 3, 4, 10, 20, 30, 40 };

        //Act
        PixelSwizzle.SwapRedBlue(pixels);

        //Assert
        pixels.Should().Equal(3, 2, 1, 4, 30, 20, 10, 40);
    }

    [Fact]
    public void CopySwapRedBlue_leaves_the_source_unchanged()
    {
        //Arrange
        var source = new byte[] { 1, 2, 3, 4 };
        var destination = new byte[4];

        //Act
        PixelSwizzle.CopySwapRedBlue(source, destination);

        //Assert
        destination.Should().Equal(3, 2, 1, 4);
        source.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void SwapRedBlue_rejects_partial_pixels()
    {
        //Act
        Action act = () => PixelSwizzle.SwapRedBlue(new byte[5]);

        //Assert
        act.Should().Throw<ArgumentException>();
    }
}
