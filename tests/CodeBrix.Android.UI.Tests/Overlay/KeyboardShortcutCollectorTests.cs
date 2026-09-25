using System.Linq;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SilverAssertions;
using Windows.System;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

[Collection(HostFreeCoreCollection.Name)]
public class KeyboardShortcutCollectorTests
{
    public KeyboardShortcutCollectorTests() => HostFreeCore.EnsureInitialized();

    private static KeyboardAccelerator Accelerator(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) =>
        new() { Key = key, Modifiers = modifiers };

    [Fact]
    public void Labelled_accelerators_of_the_tree_are_collected_in_order()
    {
        //Arrange
        var save = new Button { Content = "Save" };
        save.KeyboardAccelerators.Add(Accelerator(VirtualKey.S));
        var open = new Button();
        AutomationProperties.SetName(open, "Open a file");
        open.KeyboardAccelerators.Add(Accelerator(VirtualKey.O));
        var root = new StackPanel { Children = { save, open } };

        //Act
        var shortcuts = KeyboardShortcutCollector.Collect(root);

        //Assert
        shortcuts.Should().Equal(
            new KeyboardShortcut("Save", VirtualKey.S, VirtualKeyModifiers.Control),
            new KeyboardShortcut("Open a file", VirtualKey.O, VirtualKeyModifiers.Control));
    }

    [Fact]
    public void An_unlabelled_or_collapsed_element_offers_nothing()
    {
        //Arrange
        var unlabelled = new Border();
        unlabelled.KeyboardAccelerators.Add(Accelerator(VirtualKey.U));
        var hidden = new Button { Content = "Hidden", Visibility = Visibility.Collapsed };
        hidden.KeyboardAccelerators.Add(Accelerator(VirtualKey.H));
        var root = new StackPanel { Children = { unlabelled, hidden } };

        //Act & Assert
        KeyboardShortcutCollector.Collect(root).Should().BeEmpty();
    }

    [Fact]
    public void The_items_of_a_button_menu_are_collected_once()
    {
        //Arrange
        var copy = new MenuFlyoutItem { Text = "Copy" };
        copy.KeyboardAccelerators.Add(Accelerator(VirtualKey.C));
        var copyAgain = new MenuFlyoutItem { Text = "Copy again" };
        copyAgain.KeyboardAccelerators.Add(Accelerator(VirtualKey.C));
        var sub = new MenuFlyoutSubItem { Text = "More" };
        sub.Items.Add(copyAgain);
        var menu = new MenuFlyout { Items = { copy, sub } };
        var root = new StackPanel { Children = { new Button { Content = "Edit", Flyout = menu } } };

        //Act
        var shortcuts = KeyboardShortcutCollector.Collect(root);

        //Assert
        shortcuts.Select(s => s.Label).Should().Equal("Copy");
    }
}
