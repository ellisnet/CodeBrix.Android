using CodeBrix.Android.UI.Portable.Layout;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Layout;

public class LayoutReplayMathTests
{
    [Fact]
    public void ChildPixels_at_density_1_is_the_Core_rectangle()
    {
        //Act
        var rect = LayoutReplayMath.ChildPixels(new Point(20, 20), new Rect(300, 10, 104, 19), 1);

        //Assert
        rect.Left.Should().Be(300);
        rect.Top.Should().Be(10);
        rect.Width.Should().Be(104);
        rect.Height.Should().Be(19);
    }

    [Fact]
    public void Neighbours_tile_without_gap_or_overlap_at_a_fractional_density()
    {
        //Arrange (three 33.333-DIP columns at density 2.625)
        const double density = 2.625;
        var width = 100.0 / 3;

        //Act
        var a = LayoutReplayMath.ChildPixels(new Point(0, 0), new Rect(0, 0, width, 10), density);
        var b = LayoutReplayMath.ChildPixels(new Point(0, 0), new Rect(width, 0, width, 10), density);
        var c = LayoutReplayMath.ChildPixels(new Point(0, 0), new Rect(2 * width, 0, width, 10), density);

        //Assert
        b.Left.Should().BeLessThanOrEqualTo(a.Right);
        c.Left.Should().BeLessThanOrEqualTo(b.Right);
        c.Right.Should().Be(263);
    }

    [Fact]
    public void Nesting_does_not_accumulate_rounding_error()
    {
        //Arrange: a child at 10.4 DIPs inside a parent at 10.4 DIPs, density 3.5
        const double density = 3.5;
        var parentOrigin = new Point(10.4, 0);
        var parent = LayoutReplayMath.ChildPixels(new Point(0, 0), new Rect(10.4, 0, 50, 10), density);
        var child = LayoutReplayMath.ChildPixels(parentOrigin, new Rect(10.4, 0, 20, 10), density);

        //Act (the child's window position = parent's window position + its position inside the parent)
        var childWindowLeft = parent.Left + child.Left;

        //Assert (window position rounded once: floor(20.8 * 3.5) = 72)
        childWindowLeft.Should().Be(72);
    }

    [Fact]
    public void An_empty_rectangle_has_no_pixels()
    {
        //Act
        var rect = LayoutReplayMath.ChildPixels(new Point(0, 0), Rect.Empty, 2);

        //Assert
        rect.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(112.00000000000001, 2.625, 294)]
    [InlineData(10, 1, 10)]
    [InlineData(10.1, 1, 11)]
    public void ToPixels_ceils_after_removing_floating_point_noise(double dips, double density, int expected)
    {
        //Act
        var pixels = LayoutReplayMath.ToPixels(dips, density);

        //Assert
        pixels.Should().Be(expected);
    }
}
