using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Overlay;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

public class MenuLayoutTests
{
    private static MenuEntry Item(string text) => new() { Kind = MenuEntryKind.Item, Text = text };

    private static MenuEntry Separator() => new() { Kind = MenuEntryKind.Separator };

    private static MenuEntry Radio(string text, string group, bool isChecked = false) =>
        new() { Kind = MenuEntryKind.Radio, Text = text, RadioGroup = group, IsChecked = isChecked };

    [Fact]
    public void Items_without_separators_share_one_group()
    {
        //Act
        var placed = MenuLayout.Place(new[] { Item("Open"), Item("Save") });

        //Assert
        placed.Select(p => p.GroupId).Should().Equal(0, 0);
    }

    [Fact]
    public void A_separator_starts_the_next_group_and_is_not_shown()
    {
        //Act
        var placed = MenuLayout.Place(new[] { Item("Cut"), Separator(), Item("Paste") });

        //Assert
        placed.Select(p => p.Entry.Text).Should().Equal("Cut", "Paste");
        placed.Select(p => p.GroupId).Should().Equal(0, 1);
    }

    [Fact]
    public void Leading_trailing_and_doubled_separators_show_nothing()
    {
        //Act
        var placed = MenuLayout.Place(new[] { Separator(), Item("A"), Separator(), Separator(), Item("B"), Separator() });

        //Assert
        placed.Select(p => p.GroupId).Should().Equal(0, 1);
    }

    [Fact]
    public void A_run_of_radio_items_is_its_own_exclusive_group()
    {
        //Act
        var placed = MenuLayout.Place(new[] { Item("Bold"), Radio("Small", "size"), Radio("Large", "size"), Item("Reset") });

        //Assert
        placed.Select(p => p.GroupId).Should().Equal(0, 1, 1, 2);
        placed.Select(p => p.ExclusiveGroup).Should().Equal(false, true, true, false);
    }

    [Fact]
    public void Radio_items_of_different_group_names_get_different_groups()
    {
        //Act
        var placed = MenuLayout.Place(new[] { Radio("Small", "size"), Radio("Red", "colour") });

        //Assert
        placed[0].GroupId.Should().NotBe(placed[1].GroupId);
    }

    [Fact]
    public void The_last_checked_radio_item_of_a_group_is_the_checked_one()
    {
        //Arrange (a menu built from code that says two)
        var small = Radio("Small", "size", isChecked: true);
        var large = Radio("Large", "size", isChecked: true);
        var placed = MenuLayout.Place(new[] { small, large });

        //Act
        var checkedEntries = MenuLayout.CheckedRadioEntries(placed);

        //Assert
        checkedEntries.Should().BeEquivalentTo(new List<MenuEntry> { large });
    }
}
