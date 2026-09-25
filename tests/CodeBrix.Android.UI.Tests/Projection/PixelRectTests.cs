using CodeBrix.Android.UI.Portable.Projection;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Projection;

public class PixelRectTests
{
    [Fact]
    public void FromDips_scales_by_density()
    {
        //Act
        var rect = PixelRect.FromDips(10, 20, 30, 40, 2.0);

        //Assert
        rect.Should().Be(new PixelRect(20, 40, 80, 120));
    }

    [Fact]
    public void FromDips_floors_the_start_and_ceils_the_end_so_the_box_is_covered()
    {
        //Act
        var rect = PixelRect.FromDips(10.4, 5.6, 20.2, 10.1, 1.5);

        //Assert (15.6 -> 15, 8.4 -> 8, 45.9 -> 46, 23.55 -> 24)
        rect.Should().Be(new PixelRect(15, 8, 46, 24));
    }

    [Fact]
    public void FromDips_ignores_floating_point_noise()
    {
        //Act
        var rect = PixelRect.FromDips(0.1 + 0.2, 0, 0.7, 1, 10);

        //Assert (0.30000000000000004 * 10 must not floor to 2 or ceil the end to 11)
        rect.Should().Be(new PixelRect(3, 0, 10, 10));
    }

    [Fact]
    public void FromDips_of_NaN_or_no_density_is_empty()
    {
        //Act
        var nan = PixelRect.FromDips(double.NaN, 0, 10, 10, 1);
        var noDensity = PixelRect.FromDips(0, 0, 10, 10, 0);

        //Assert
        nan.IsEmpty.Should().BeTrue();
        noDensity.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Intersect_returns_the_overlap_or_empty()
    {
        //Arrange
        var a = new PixelRect(0, 0, 100, 100);

        //Act
        var overlap = a.Intersect(new PixelRect(50, 60, 150, 160));
        var none = a.Intersect(new PixelRect(100, 0, 200, 100));

        //Assert
        overlap.Should().Be(new PixelRect(50, 60, 100, 100));
        overlap.Width.Should().Be(50);
        overlap.Height.Should().Be(40);
        none.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsInside_and_Unbounded()
    {
        //Arrange
        var inner = new PixelRect(10, 10, 20, 20);

        //Act
        var inside = inner.IsInside(new PixelRect(0, 0, 100, 100));
        var outside = inner.IsInside(new PixelRect(15, 0, 100, 100));
        var unbounded = inner.IsInside(PixelRect.Unbounded);

        //Assert
        inside.Should().BeTrue();
        outside.Should().BeFalse();
        unbounded.Should().BeTrue();
    }
}
