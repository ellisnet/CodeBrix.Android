using CodeBrix.Android.UI.Portable.Layout;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Layout;

public class SafeAreaMathTests
{
    private static readonly SafeAreaPadding PortraitBars = new(0, 24, 0, 48);

    [Fact]
    public void A_page_filling_the_window_absorbs_the_whole_safe_area()
    {
        //Act
        var insets = SafeAreaMath.Overlap(0, 0, 400, 800, 400, 800, PortraitBars);

        //Assert
        insets.Should().Be(new SafeAreaPadding(0, 24, 0, 48));
    }

    [Fact]
    public void A_page_already_below_the_status_bar_absorbs_no_top_inset()
    {
        //Act (a page nested in an absorbing page starts at the safe top)
        var insets = SafeAreaMath.Overlap(0, 24, 400, 728, 400, 800, PortraitBars);

        //Assert
        insets.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Only_the_overlapped_part_of_an_inset_is_absorbed()
    {
        //Act (the page starts 10 DIPs down, under the 24-DIP status bar)
        var insets = SafeAreaMath.Overlap(0, 10, 400, 100, 400, 800, PortraitBars);

        //Assert
        insets.Top.Should().Be(14);
        insets.Bottom.Should().Be(0);
    }

    [Fact]
    public void Landscape_side_insets_are_absorbed_on_the_edge_they_touch()
    {
        //Arrange (navigation bar on the right in landscape)
        var bars = new SafeAreaPadding(0, 24, 48, 0);

        //Act
        var left = SafeAreaMath.Overlap(0, 0, 300, 400, 800, 400, bars);
        var right = SafeAreaMath.Overlap(500, 0, 300, 400, 800, 400, bars);

        //Assert
        left.Right.Should().Be(0);
        left.Top.Should().Be(24);
        right.Right.Should().Be(48);
    }

    [Fact]
    public void Nothing_is_absorbed_more_than_the_page_is_tall()
    {
        //Act (an 8-DIP strip at the top overlapping both bars of a 30-DIP window)
        var insets = SafeAreaMath.Overlap(0, 0, 100, 30, 100, 30, new SafeAreaPadding(0, 24, 0, 24));

        //Assert
        (insets.Top + insets.Bottom).Should().BeApproximately(30, 0.0001);
    }

    [Fact]
    public void Pixels_convert_to_dips_with_the_density()
    {
        //Act (the status bar is 24 dp: 63 px at density 2.625, 84 px at 3.5)
        var a = new SafeAreaPadding(0, 63, 0, 126).ToDips(2.625);
        var b = new SafeAreaPadding(0, 84, 0, 168).ToDips(3.5);

        //Assert
        a.Top.Should().Be(24);
        b.Top.Should().Be(24);
        a.Bottom.Should().Be(48);
    }
}
