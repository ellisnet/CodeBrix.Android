using System.Numerics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP6: the WinUI stroke rules the shapes' native view adds to the Android stroker.</summary>
[Collection(HostFreeCoreCollection.Name)]
public class StrokeMathTests
{
    public StrokeMathTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Dash_intervals_are_multiples_of_the_thickness_and_an_odd_array_repeats()
    {
        //Act
        var intervals = StrokeMath.DashIntervals(new DoubleCollection { 2, 1, 3 }, 4);

        //Assert
        intervals.Should().Equal(8f, 4f, 12f, 8f, 4f, 12f);
    }

    [Fact]
    public void No_dash_array_or_an_all_zero_one_means_no_dashes()
    {
        //Assert
        StrokeMath.DashIntervals(null, 2).Should().BeNull();
        StrokeMath.DashIntervals(new DoubleCollection(), 2).Should().BeNull();
        StrokeMath.DashIntervals(new DoubleCollection { 0, 0 }, 2).Should().BeNull();
    }

    [Fact]
    public void A_position_falls_in_a_dash_or_a_gap_following_the_offset()
    {
        //Arrange
        var intervals = new[] { 10f, 5f };

        //Assert
        StrokeMath.IsPositionInDash(0, intervals, 0).Should().BeTrue();
        StrokeMath.IsPositionInDash(12, intervals, 0).Should().BeFalse();
        StrokeMath.IsPositionInDash(16, intervals, 0).Should().BeTrue();
        StrokeMath.IsPositionInDash(0, intervals, 11).Should().BeFalse();
    }

    [Fact]
    public void The_end_of_a_figure_is_in_a_dash_at_a_gap_boundary_or_in_a_gap()
    {
        //Arrange
        var intervals = new[] { 10f, 5f };

        //Assert
        StrokeMath.EndpointState(8, intervals, 0).Should().Be(EndpointDashState.InRenderedDash);
        StrokeMath.EndpointState(15, intervals, 0).Should().Be(EndpointDashState.AtGapBoundary);
        StrokeMath.EndpointState(12, intervals, 0).Should().Be(EndpointDashState.InGap);
    }

    [Fact]
    public void Internal_dash_boundaries_leave_out_an_open_figures_own_ends()
    {
        //Act
        var boundaries = StrokeMath.InternalDashBoundaries(25, closed: false, new[] { 10f, 5f }, 0);

        //Assert
        boundaries.Should().Equal((10f, false), (15f, true));
    }

    [Fact]
    public void A_square_cap_extends_half_the_thickness_beyond_the_end()
    {
        //Act
        var corners = StrokeMath.CapPolygon(new Vector2(10, 10), new Vector2(1, 0), 4, PenLineCap.Square);

        //Assert
        corners.Should().Equal(new Vector2(10, 12), new Vector2(12, 12), new Vector2(12, 8), new Vector2(10, 8));
    }

    [Fact]
    public void A_triangle_cap_has_its_apex_half_the_thickness_beyond_the_end()
    {
        //Act
        var corners = StrokeMath.CapPolygon(new Vector2(0, 0), new Vector2(0, -1), 6, PenLineCap.Triangle);

        //Assert
        corners.Should().HaveCount(3);
        corners[1].Should().Be(new Vector2(0, -3));
    }

    [Fact]
    public void Flat_and_round_caps_have_no_polygon()
    {
        //Assert
        StrokeMath.CapPolygon(Vector2.Zero, Vector2.UnitX, 4, PenLineCap.Flat).Should().BeNull();
        StrokeMath.CapPolygon(Vector2.Zero, Vector2.UnitX, 4, PenLineCap.Round).Should().BeNull();
    }

    [Fact]
    public void A_sharp_join_over_the_miter_limit_gets_a_clipped_miter()
    {
        //Arrange: a 10-degree turn back
        var incoming = new Vector2(1, 0);
        var outgoing = StrokeMath.Normalize(-0.985f, 0.174f);

        //Act
        var trapezoid = StrokeMath.MiterClipTrapezoid(new Vector2(50, 50), incoming, outgoing, 5, 4);

        //Assert
        trapezoid.Should().NotBeNull();
        trapezoid.Should().HaveCount(4);
    }

    [Fact]
    public void A_right_angle_join_is_within_the_default_miter_limit()
    {
        //Act
        var trapezoid = StrokeMath.MiterClipTrapezoid(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), 5, StrokeMath.EffectiveMiterLimit(10));

        //Assert
        trapezoid.Should().BeNull();
    }

    [Fact]
    public void A_miter_limit_below_one_strokes_as_one()
    {
        //Assert
        StrokeMath.EffectiveMiterLimit(0).Should().Be(1f);
        StrokeMath.EffectiveMiterLimit(0.5).Should().Be(1f);
    }

    [Fact]
    public void A_miter_limit_of_one_or_more_is_used_as_it_is()
    {
        //Assert
        StrokeMath.EffectiveMiterLimit(3).Should().Be(3f);
        StrokeMath.EffectiveMiterLimit(10).Should().Be(10f);
    }
}
