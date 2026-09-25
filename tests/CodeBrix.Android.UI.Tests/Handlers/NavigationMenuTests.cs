using System.Collections.ObjectModel;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// AP5: the menu a native navigation container shows for a NavigationView, and the rule for when a NavigationView
/// keeps its Fluent template.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class NavigationMenuTests
{
    public NavigationMenuTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Entries_lists_items_headers_separators_and_Settings_last()
    {
        //Arrange
        var view = new NavigationView();
        view.MenuItems.Add(new NavigationViewItemHeader { Content = "Mail" });
        view.MenuItems.Add(new NavigationViewItem { Content = "Inbox", Icon = new SymbolIcon(Symbol.Mail) });
        view.MenuItems.Add(new NavigationViewItemSeparator());
        view.MenuItems.Add(new NavigationViewItem { Content = "Archive", IsEnabled = false });
        var settings = new NavigationViewItem { Content = "Settings" };

        //Act
        var entries = NavigationMenu.Entries(view, settings);

        //Assert
        entries.Should().HaveCount(5);
        entries[0].Kind.Should().Be(NavigationMenuEntryKind.Header);
        entries[0].Text.Should().Be("Mail");
        entries[1].Text.Should().Be("Inbox");
        entries[1].Icon.Should().BeOfType<SymbolIcon>();
        entries[2].Kind.Should().Be(NavigationMenuEntryKind.Separator);
        entries[3].IsEnabled.Should().BeFalse();
        entries[4].Kind.Should().Be(NavigationMenuEntryKind.Settings);
        entries[4].Item.Should().BeSameAs(settings);
        NavigationMenu.DestinationCount(entries).Should().Be(3);
    }

    [Fact]
    public void Entries_has_no_Settings_when_IsSettingsVisible_is_false()
    {
        //Arrange
        var view = new NavigationView { IsSettingsVisible = false };
        view.MenuItems.Add(new NavigationViewItem { Content = "Home" });

        //Act
        var entries = NavigationMenu.Entries(view, new NavigationViewItem { Content = "Settings" });

        //Assert
        NavigationMenu.DestinationCount(entries).Should().Be(1);
    }

    [Fact]
    public void Entries_takes_data_items_from_MenuItemsSource()
    {
        //Arrange
        var view = new NavigationView { IsSettingsVisible = false, MenuItemsSource = new ObservableCollection<string> { "Red", "Green" } };

        //Act
        var entries = NavigationMenu.Entries(view, null);

        //Assert
        entries.Should().HaveCount(2);
        entries[1].Text.Should().Be("Green");
        NavigationMenu.IndexOf(entries, "Green").Should().Be(1);
    }

    [Fact]
    public void IndexOf_finds_the_selected_item_or_its_content()
    {
        //Arrange
        var view = new NavigationView();
        var music = new NavigationViewItem { Content = "Music" };
        view.MenuItems.Add(new NavigationViewItem { Content = "Home" });
        view.MenuItems.Add(music);
        var entries = NavigationMenu.Entries(view, null);

        //Assert
        NavigationMenu.IndexOf(entries, music).Should().Be(1);
        NavigationMenu.IndexOf(entries, "Music").Should().Be(1);
        NavigationMenu.IndexOf(entries, null).Should().Be(-1);
    }

    [Fact]
    public void CanMapNatively_accepts_a_plain_menu()
    {
        //Arrange
        var view = new NavigationView();
        view.MenuItems.Add(new NavigationViewItem { Content = "Home", Icon = new SymbolIcon(Symbol.Home) });

        //Act
        var native = NavigationMenu.CanMapNatively(view, out var reason);

        //Assert
        native.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void CanMapNatively_keeps_the_template_for_a_pane_footer_nested_items_or_element_content()
    {
        //Arrange
        var footer = new NavigationView { PaneFooter = new TextBlock { Text = "Footer" } };
        var nested = new NavigationView();
        var parent = new NavigationViewItem { Content = "Parent" };
        parent.MenuItems.Add(new NavigationViewItem { Content = "Child" });
        nested.MenuItems.Add(parent);
        var element = new NavigationView();
        element.MenuItems.Add(new NavigationViewItem { Content = new TextBlock { Text = "Rich" } });

        //Assert
        NavigationMenu.CanMapNatively(footer, out var footerReason).Should().BeFalse();
        footerReason.Should().Contain("PaneFooter");
        NavigationMenu.CanMapNatively(nested, out var nestedReason).Should().BeFalse();
        nestedReason.Should().Contain("nested");
        NavigationMenu.CanMapNatively(element, out var elementReason).Should().BeFalse();
        elementReason.Should().Contain("element content");
    }

    [Fact]
    public void TextOf_turns_content_into_a_label()
    {
        //Assert
        NavigationMenu.TextOf(null).Should().BeEmpty();
        NavigationMenu.TextOf("Home").Should().Be("Home");
        NavigationMenu.TextOf(42).Should().Be("42");
    }
}
