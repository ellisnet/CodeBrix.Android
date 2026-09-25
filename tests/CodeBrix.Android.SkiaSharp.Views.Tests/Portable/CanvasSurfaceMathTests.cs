using CodeBrix.Android.SkiaSharp.Views.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.SkiaSharp.Views.Tests.Portable;

public class CanvasSurfaceMathTests
{
    [Fact]
    public void The_surface_a_handler_is_told_about_is_the_pixel_buffer()
    {
        //Arrange
        //Act
        var size = CanvasSurfaceMath.UserVisibleSize(1050, 788, 400, 300, ignorePixelScaling: false);

        //Assert
        size.Should().Be((1050, 788));
    }

    [Fact]
    public void With_IgnorePixelScaling_the_handler_is_told_the_size_in_DIPs()
    {
        //Arrange
        //Act
        var size = CanvasSurfaceMath.UserVisibleSize(1050, 788, 400.7, 300.2, ignorePixelScaling: true);

        //Assert
        size.Should().Be((400, 300));
    }

    [Theory]
    [InlineData(400, 300, true)]
    [InlineData(0, 300, false)]
    [InlineData(400, 0, false)]
    [InlineData(-1, 300, false)]
    [InlineData(double.NaN, 300, false)]
    [InlineData(double.PositiveInfinity, 300, false)]
    public void Only_an_element_with_a_positive_finite_size_is_painted(double width, double height, bool expected) =>
        CanvasSurfaceMath.IsPaintable(width, height).Should().Be(expected);

    [Theory]
    [InlineData(2.625, 2.625f)]
    [InlineData(1.0, 1.0f)]
    [InlineData(0.0, 1.0f)]
    [InlineData(double.NaN, 1.0f)]
    public void One_canvas_unit_is_one_DIP(double density, float expected) =>
        CanvasSurfaceMath.CanvasScale(density).Should().Be(expected);
}
