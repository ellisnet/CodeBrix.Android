using System;
using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class WindowBoundsCalculatorTests
{
    [Fact]
    public void Compute_converts_the_window_to_dips_and_removes_the_insets()
    {
        //Act
        var (bounds, visible) = WindowBoundsCalculator.Compute(1080, 2400, 0, 96, 0, 48, 2.0);

        //Assert
        bounds.Should().Be(new DipRect(0, 0, 540, 1200));
        visible.Should().Be(new DipRect(0, 48, 540, 1128));
    }

    [Fact]
    public void Compute_keeps_side_insets_of_a_landscape_cutout()
    {
        //Act
        var (_, visible) = WindowBoundsCalculator.Compute(2400, 1080, 120, 0, 0, 0, 3.0);

        //Assert
        visible.Should().Be(new DipRect(40, 0, 760, 360));
    }

    [Fact]
    public void Compute_never_returns_negative_visible_sizes()
    {
        //Act
        var (_, visible) = WindowBoundsCalculator.Compute(100, 100, 80, 80, 80, 80, 1.0);

        //Assert
        visible.Width.Should().Be(0);
        visible.Height.Should().Be(0);
    }

    [Fact]
    public void Compute_rejects_a_non_positive_density()
    {
        //Act
        Action act = () => WindowBoundsCalculator.Compute(100, 100, 0, 0, 0, 0, 0);

        //Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
