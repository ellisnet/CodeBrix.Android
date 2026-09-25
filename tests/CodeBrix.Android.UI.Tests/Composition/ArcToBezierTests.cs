using System;
using System.Linq;
using CodeBrix.Android.UI.Composition.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Composition;

public class ArcToBezierTests
{
    // Cubic approximation constant of a quarter circle.
    private const double Kappa = 0.5522847498;

    [Fact]
    public void Convert_draws_a_quarter_circle_with_one_standard_cubic()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 50, 50, 0, 50, 50, 0, isLargeArc: false, isClockwise: true);

        //Assert
        segments.Count.Should().Be(1);
        var s = segments[0];
        s.X.Should().BeApproximately(50, 1e-9);
        s.Y.Should().BeApproximately(0, 1e-9);
        s.C1X.Should().BeApproximately(0, 1e-6);
        s.C1Y.Should().BeApproximately(50 - (50 * Kappa), 1e-6);
        s.C2X.Should().BeApproximately(50 - (50 * Kappa), 1e-6);
        s.C2Y.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void Convert_splits_a_half_circle_into_quarter_segments_through_the_top()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 50, 100, 50, 50, 50, 0, isLargeArc: false, isClockwise: true);

        //Assert
        segments.Count.Should().Be(2);
        segments[0].X.Should().BeApproximately(50, 1e-9);
        segments[0].Y.Should().BeApproximately(0, 1e-9);
        segments[1].X.Should().Be(100);
        segments[1].Y.Should().Be(50);
    }

    [Fact]
    public void Convert_goes_the_other_way_for_a_counter_clockwise_sweep()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 50, 100, 50, 50, 50, 0, isLargeArc: false, isClockwise: false);

        //Assert
        segments[0].Y.Should().BeApproximately(100, 1e-9);
    }

    [Fact]
    public void Convert_uses_three_quarters_for_a_large_arc()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 50, 50, 0, 50, 50, 0, isLargeArc: true, isClockwise: false);

        //Assert
        segments.Count.Should().Be(3);
        segments.Last().X.Should().Be(50);
        segments.Last().Y.Should().Be(0);
    }

    [Fact]
    public void Convert_scales_up_radii_that_are_too_small()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 0, 100, 0, 10, 10, 0, isLargeArc: false, isClockwise: true);

        //Assert
        segments.Count.Should().Be(2);
        segments[0].X.Should().BeApproximately(50, 1e-6);
        Math.Abs(segments[0].Y).Should().BeApproximately(50, 1e-6);
    }

    [Fact]
    public void Convert_treats_a_zero_radius_as_a_line()
    {
        //Act
        var segments = ArcToBezier.Convert(0, 0, 10, 20, 0, 5, 0, isLargeArc: false, isClockwise: true);

        //Assert
        segments.Should().ContainSingle();
        segments[0].Should().Be(new CubicSegment(0, 0, 10, 20, 10, 20));
    }

    [Fact]
    public void Convert_returns_nothing_when_the_end_is_the_start()
    {
        //Act
        var segments = ArcToBezier.Convert(5, 5, 5, 5, 10, 10, 0, isLargeArc: true, isClockwise: true);

        //Assert
        segments.Should().BeEmpty();
    }
}
