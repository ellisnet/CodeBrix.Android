using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.UI.Xaml;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Layout;

public class ContentHostMathTests
{
    [Fact]
    public void The_content_is_offered_the_size_minus_border_padding_and_insets()
    {
        //Arrange
        var inner = ContentHostMath.Inner(new Thickness(1), new Thickness(10, 5, 10, 5), new SafeAreaPadding(0, 24, 0, 48));

        //Act
        var offered = ContentHostMath.Deflate(new Size(400, 800), inner);

        //Assert
        offered.Width.Should().Be(378);
        offered.Height.Should().Be(800 - 1 - 5 - 24 - 1 - 5 - 48);
    }

    [Fact]
    public void An_infinite_dimension_stays_infinite()
    {
        //Act
        var offered = ContentHostMath.Deflate(new Size(double.PositiveInfinity, 100), new Thickness(4));

        //Assert
        double.IsPositiveInfinity(offered.Width).Should().BeTrue();
        offered.Height.Should().Be(92);
    }

    [Fact]
    public void Stretch_content_fills_the_inner_rectangle()
    {
        //Act
        var rect = ContentHostMath.ArrangeRect(new Size(400, 800), new Thickness(0, 24, 0, 48), new Size(100, 20),
            HorizontalAlignment.Stretch, VerticalAlignment.Stretch);

        //Assert
        rect.Should().Be(new Rect(0, 24, 400, 728));
    }

    [Fact]
    public void Aligned_content_keeps_its_desired_size_and_is_placed_by_the_alignment()
    {
        //Act
        var rect = ContentHostMath.ArrangeRect(new Size(400, 300), new Thickness(10), new Size(100, 20),
            HorizontalAlignment.Center, VerticalAlignment.Bottom);

        //Assert
        rect.Should().Be(new Rect(150, 270, 100, 20));
    }

    [Fact]
    public void The_host_desires_the_content_plus_its_inner_edges()
    {
        //Act
        var desired = ContentHostMath.Inflate(new Size(100, 20), new Thickness(2, 24, 2, 48));

        //Assert
        desired.Should().Be(new Size(104, 92));
    }
}
