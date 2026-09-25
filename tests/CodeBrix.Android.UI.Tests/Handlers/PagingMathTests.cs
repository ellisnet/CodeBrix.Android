using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP10-A: the arithmetic of the native PipsPager and PagerControl.</summary>
public class PagingMathTests
{
    [Theory]
    [InlineData(5, 5, 0, 0, 5)]
    [InlineData(12, 5, 6, 4, 5)]
    [InlineData(12, 5, 0, 0, 5)]
    [InlineData(12, 5, 11, 7, 5)]
    [InlineData(3, 5, 1, 0, 3)]
    [InlineData(0, 5, 0, 0, 0)]
    public void PipWindow_keeps_the_selected_pip_centred_within_the_pages(int pages, int max, int selected, int first, int count)
    {
        //Act
        var window = PagingMath.PipWindow(pages, max, selected);

        //Assert
        window.Should().Be((first, count));
    }

    [Theory]
    [InlineData(3, 2, 1, false, null)]
    [InlineData(3, 2, 1, true, 0)]
    [InlineData(3, 0, -1, true, 2)]
    [InlineData(3, 1, 1, false, 2)]
    [InlineData(-1, 40, 1, false, 41)]
    public void Step_moves_one_page_and_wraps_only_when_asked(int pages, int selected, int step, bool wrap, int? expected)
    {
        //Act
        var page = PagingMath.Step(pages, selected, step, wrap);

        //Assert
        page.Should().Be(expected);
    }

    [Theory]
    [InlineData("Auto", 5, "DropDown")]
    [InlineData("Auto", 10, "NumberField")]
    [InlineData("Auto", -1, "NumberField")]
    [InlineData("ComboBox", 50, "DropDown")]
    [InlineData("NumberBox", 3, "NumberField")]
    [InlineData("ButtonPanel", 3, "ButtonPanel")]
    public void Selector_follows_the_DisplayMode_and_Cores_Auto_rule(string mode, int pages, string expected)
    {
        //Act
        var kind = PagingMath.Selector(mode, pages);

        //Assert
        kind.ToString().Should().Be(expected);
    }

    [Fact]
    public void PanelNumbers_lists_every_page_up_to_seven()
    {
        //Act
        var numbers = PagingMath.PanelNumbers(5, 2);

        //Assert
        numbers.Should().Equal(0, 1, 2, 3, 4);
    }

    [Fact]
    public void PanelNumbers_puts_ellipses_around_the_selected_page_of_a_long_pager()
    {
        //Act
        var start = PagingMath.PanelNumbers(20, 1);
        var middle = PagingMath.PanelNumbers(20, 10);
        var end = PagingMath.PanelNumbers(20, 18);

        //Assert
        start.Should().Equal(0, 1, 2, 3, 4, PagingMath.Ellipsis, 19);
        middle.Should().Equal(0, PagingMath.Ellipsis, 9, 10, 11, PagingMath.Ellipsis, 19);
        end.Should().Equal(0, PagingMath.Ellipsis, 15, 16, 17, 18, 19);
    }
}
