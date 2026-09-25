using CodeBrix.Android.UI.Overlay;
using SilverAssertions;
using Windows.System;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

public class MenuShortcutTests
{
    [Fact]
    public void Ctrl_S_is_the_letter_s_with_the_ctrl_meta_flag()
    {
        //Act
        var shortcut = MenuShortcut.From(VirtualKey.S, VirtualKeyModifiers.Control);

        //Assert
        shortcut.Should().Be(new MenuShortcut('s', MenuShortcut.MetaCtrl));
    }

    [Fact]
    public void Every_modifier_maps_to_its_meta_flag()
    {
        //Act
        var shortcut = MenuShortcut.From(VirtualKey.Number1, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Windows);

        //Assert
        shortcut.Should().Be(new MenuShortcut('1', MenuShortcut.MetaCtrl | MenuShortcut.MetaShift | MenuShortcut.MetaAlt | MenuShortcut.MetaMeta));
    }

    [Fact]
    public void A_key_an_android_menu_cannot_show_has_no_shortcut()
    {
        //Act
        var shortcut = MenuShortcut.From(VirtualKey.F5, VirtualKeyModifiers.None);

        //Assert
        shortcut.Should().BeNull();
    }
}
