using System.Linq;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SilverAssertions;
using Windows.System;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

[Collection(HostFreeCoreCollection.Name)]
public class MenuModelBuilderTests
{
    public MenuModelBuilderTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Every_standard_item_kind_is_read_with_its_state()
    {
        //Arrange
        var menu = new MenuFlyout();
        var save = new MenuFlyoutItem { Text = "Save", Icon = new FontIcon { Glyph = "" } };
        save.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control });
        menu.Items.Add(save);
        menu.Items.Add(new ToggleMenuFlyoutItem { Text = "Bold", IsChecked = true });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new RadioMenuFlyoutItem { Text = "Large", GroupName = "size", IsChecked = true });
        var more = new MenuFlyoutSubItem { Text = "More", IsEnabled = false };
        more.Items.Add(new MenuFlyoutItem { Text = "About" });
        menu.Items.Add(more);

        //Act
        var entries = MenuModelBuilder.Build(menu.Items);

        //Assert
        entries.Select(e => e.Kind).Should().Equal(MenuEntryKind.Item, MenuEntryKind.Toggle, MenuEntryKind.Separator, MenuEntryKind.Radio, MenuEntryKind.SubMenu);
        entries[0].Shortcut.Should().Be(new MenuShortcut('s', MenuShortcut.MetaCtrl));
        entries[0].IconGlyph.Should().Be("");
        entries[1].IsChecked.Should().BeTrue();
        entries[3].RadioGroup.Should().Be("size");
        entries[4].IsEnabled.Should().BeFalse();
        entries[4].Children.Single().Text.Should().Be("About");
        entries[0].Source.Should().BeSameAs(save);
    }

    [Fact]
    public void A_collapsed_item_is_left_out()
    {
        //Arrange
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Shown" });
        menu.Items.Add(new MenuFlyoutItem { Text = "Hidden", Visibility = Visibility.Collapsed });

        //Act
        var entries = MenuModelBuilder.Build(menu.Items);

        //Assert
        entries.Select(e => e.Text).Should().Equal("Shown");
    }

    [Fact]
    public void Standard_items_are_natively_presentable()
    {
        //Arrange
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Open" });
        menu.Items.Add(new MenuFlyoutSeparator());

        //Act & Assert
        MenuModelBuilder.IsNativelyPresentable(menu.Items).Should().BeTrue();
    }

    [Fact]
    public void An_app_defined_item_type_keeps_the_menu_in_core()
    {
        //Arrange
        var menu = new MenuFlyout();
        var sub = new MenuFlyoutSubItem { Text = "More" };
        sub.Items.Add(new AppMenuItem { Text = "Custom" });
        menu.Items.Add(sub);

        //Act & Assert
        MenuModelBuilder.IsNativelyPresentable(menu.Items).Should().BeFalse();
    }

    private sealed class AppMenuItem : MenuFlyoutItem
    {
    }
}
