using CodeBrix.Android.UI.CommandBar.Portable;
using SilverAssertions;
using Xunit;
using K = CodeBrix.Android.UI.CommandBar.Portable.OverflowAncestorKind;

namespace CodeBrix.Android.UI.CommandBar.Tests.Portable;

public class OverflowItemDismissalTests
{
    [Fact]
    public void An_item_in_the_overflow_panel_closes_the_flyout()
    {
        //Arrange
        var ancestors = new[] { K.Other, K.ToolBarPanel, K.Other, K.Other, K.PopupChild };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, ancestors);

        //Assert
        closes.Should().BeTrue();
    }

    [Fact]
    public void An_item_of_a_group_in_the_overflow_closes_the_flyout()
    {
        //Arrange
        var ancestors = new[] { K.ToolBarPanel, K.Other, K.ToolBarPanel, K.PopupChild };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, ancestors);

        //Assert
        closes.Should().BeTrue();
    }

    [Fact]
    public void A_drop_down_item_in_the_overflow_keeps_the_flyout_open()
    {
        //Arrange
        var ancestors = new[] { K.ToolBarPanel, K.PopupChild };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(true, ancestors);

        //Assert
        closes.Should().BeFalse();
    }

    [Fact]
    public void An_item_of_a_bar_that_sits_in_a_popup_keeps_the_popup_open()
    {
        //Arrange
        var ancestors = new[] { K.ToolBarPanel, K.Other, K.ToolBar, K.Other, K.PopupChild };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, ancestors);

        //Assert
        closes.Should().BeFalse();
    }

    [Fact]
    public void A_tool_item_in_a_popup_without_a_tool_bar_panel_keeps_the_popup_open()
    {
        //Arrange
        var ancestors = new[] { K.Other, K.Other, K.PopupChild };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, ancestors);

        //Assert
        closes.Should().BeFalse();
    }

    [Fact]
    public void An_item_on_the_bar_itself_closes_nothing()
    {
        //Arrange
        var ancestors = new[] { K.ToolBarPanel, K.Other, K.ToolBar, K.Other };

        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, ancestors);

        //Assert
        closes.Should().BeFalse();
    }

    [Fact]
    public void No_ancestors_close_nothing()
    {
        //Act
        var closes = OverflowItemDismissal.ClosesFlyout(false, null);

        //Assert
        closes.Should().BeFalse();
    }
}
