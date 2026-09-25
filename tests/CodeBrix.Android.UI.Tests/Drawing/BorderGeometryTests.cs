using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Drawing;

[Collection(HostFreeCoreCollection.Name)]
public class BorderGeometryTests
{
    public BorderGeometryTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Per_side_thickness_scales_with_the_density()
    {
        //Act
        var geometry = BorderGeometry.Compute(200, 100, new Thickness(1, 2, 3, 4), new CornerRadius(0), 2);

        //Assert
        geometry.Left.Should().Be(2);
        geometry.Top.Should().Be(4);
        geometry.InnerRight.Should().Be(194);
        geometry.InnerBottom.Should().Be(92);
        geometry.HasBorder.Should().BeTrue();
    }

    [Fact]
    public void Inner_radii_are_the_outer_radii_minus_the_adjacent_thickness()
    {
        //Act
        var geometry = BorderGeometry.Compute(200, 100, new Thickness(4), new CornerRadius(10, 2, 10, 10), 1);

        //Assert (top-left 10-4; top-right 2-4 clamps to 0)
        geometry.OuterRadii.Should().Equal(10f, 10f, 2f, 2f, 10f, 10f, 10f, 10f);
        geometry.InnerRadii[0].Should().Be(6);
        geometry.InnerRadii[2].Should().Be(0);
    }

    [Fact]
    public void Overlapping_radii_are_scaled_down_together()
    {
        //Act (60 + 60 > 100 on the top edge)
        var geometry = BorderGeometry.Compute(100, 200, new Thickness(0), new CornerRadius(60), 1);

        //Assert
        geometry.OuterRadii[0].Should().BeApproximately(50, 0.01f);
        geometry.OuterRadii[2].Should().BeApproximately(50, 0.01f);
    }

    [Fact]
    public void No_thickness_means_no_border()
    {
        //Act
        var geometry = BorderGeometry.Compute(100, 100, new Thickness(0), new CornerRadius(0), 1);

        //Assert
        geometry.HasBorder.Should().BeFalse();
        geometry.IsRounded.Should().BeFalse();
    }

    [Theory]
    [InlineData(BackgroundSizing.InnerBorderEdge, true)]
    [InlineData(BackgroundSizing.OuterBorderEdge, false)]
    public void FillsInnerOnly_follows_BackgroundSizing(BackgroundSizing sizing, bool expected)
    {
        //Act
        var inner = BorderGeometry.FillsInnerOnly(sizing);

        //Assert
        inner.Should().Be(expected);
    }
}
