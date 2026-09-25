using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP10-A: what a WinUI CommandBar looks like as a Material app bar, and when it keeps its Fluent template.</summary>
[Collection(HostFreeCoreCollection.Name)]
public class AppBarModelTests
{
    public AppBarModelTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Primary_commands_are_actions_while_there_is_room_and_secondary_commands_the_overflow()
    {
        //Arrange
        var bar = new CommandBar();
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add", Icon = new SymbolIcon(Symbol.Add) });
        bar.PrimaryCommands.Add(new AppBarToggleButton { Label = "Bold", IsChecked = true });
        bar.SecondaryCommands.Add(new AppBarButton { Label = "Settings" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries.Should().HaveCount(3);
        entries[0].Label.Should().Be("Add");
        entries[0].Placement.Should().Be(AppBarEntryPlacement.ActionIfRoom);
        entries[0].Icon.Should().BeOfType<SymbolIcon>();
        entries[1].Kind.Should().Be(AppBarEntryKind.ToggleButton);
        entries[1].IsChecked.Should().BeTrue();
        entries[2].Placement.Should().Be(AppBarEntryPlacement.Overflow);
        entries[2].Group.Should().Be(1);
    }

    [Fact]
    public void Without_dynamic_overflow_primary_commands_are_always_actions()
    {
        //Arrange
        var bar = new CommandBar { IsDynamicOverflowEnabled = false };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries[0].Placement.Should().Be(AppBarEntryPlacement.Action);
    }

    [Fact]
    public void A_Minimal_bar_keeps_every_command_in_the_overflow()
    {
        //Arrange
        var bar = new CommandBar { ClosedDisplayMode = AppBarClosedDisplayMode.Minimal };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add" });
        bar.SecondaryCommands.Add(new AppBarButton { Label = "Settings" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries.Should().OnlyContain(e => e.Placement == AppBarEntryPlacement.Overflow);
    }

    [Fact]
    public void Secondary_separators_start_menu_groups_and_primary_separators_are_left_out()
    {
        //Arrange
        var bar = new CommandBar();
        bar.PrimaryCommands.Add(new AppBarButton { Label = "One" });
        bar.PrimaryCommands.Add(new AppBarSeparator());
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Two" });
        bar.SecondaryCommands.Add(new AppBarButton { Label = "A" });
        bar.SecondaryCommands.Add(new AppBarSeparator());
        bar.SecondaryCommands.Add(new AppBarButton { Label = "B" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries.Should().HaveCount(4);
        entries[2].Group.Should().Be(1);
        entries[3].Group.Should().Be(2);
    }

    [Fact]
    public void Collapsed_commands_are_left_out_and_disabled_ones_are_disabled()
    {
        //Arrange
        var bar = new CommandBar();
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Hidden", Visibility = Visibility.Collapsed });
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Off", IsEnabled = false });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries.Should().ContainSingle();
        entries[0].Label.Should().Be("Off");
        entries[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void A_disabled_bar_disables_every_command()
    {
        //Arrange
        var bar = new CommandBar { IsEnabled = false };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void A_collapsed_overflow_button_hides_the_secondary_commands()
    {
        //Arrange
        var bar = new CommandBar { OverflowButtonVisibility = CommandBarOverflowButtonVisibility.Collapsed };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add" });
        bar.SecondaryCommands.Add(new AppBarButton { Label = "Settings" });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries.Should().ContainSingle();
    }

    [Fact]
    public void Labels_show_beside_icons_only_with_DefaultLabelPosition_Right()
    {
        //Arrange
        var bar = new CommandBar { DefaultLabelPosition = CommandBarDefaultLabelPosition.Right };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add" });
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Quiet", LabelPosition = CommandBarLabelPosition.Collapsed });

        //Act
        var entries = AppBarModel.Entries(bar);

        //Assert
        entries[0].ShowLabel.Should().BeTrue();
        entries[1].ShowLabel.Should().BeFalse();
    }

    [Fact]
    public void A_bar_with_text_content_and_app_bar_buttons_maps_natively()
    {
        //Arrange
        var bar = new CommandBar { Content = "Title" };
        bar.PrimaryCommands.Add(new AppBarButton());
        bar.SecondaryCommands.Add(new AppBarSeparator());

        //Act
        var native = AppBarModel.CanMapNatively(bar, out var reason);

        //Assert
        native.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void A_bar_with_element_content_keeps_its_template()
    {
        //Arrange
        var bar = new CommandBar { Content = new TextBlock { Text = "Title" } };

        //Act
        var native = AppBarModel.CanMapNatively(bar, out var reason);

        //Assert
        native.Should().BeFalse();
        reason.Should().Contain("Content");
    }

    [Fact]
    public void A_bar_with_an_AppBarElementContainer_keeps_its_template()
    {
        //Arrange
        var bar = new CommandBar();
        bar.PrimaryCommands.Add(new AppBarElementContainer { Content = new TextBlock() });

        //Act
        var native = AppBarModel.CanMapNatively(bar, out var reason);

        //Assert
        native.Should().BeFalse();
        reason.Should().Contain("AppBarElementContainer");
    }

    [Fact]
    public void The_command_row_of_a_CommandBarFlyout_keeps_its_template()
    {
        //Arrange
        var bar = new Microsoft.UI.Xaml.Controls.Primitives.CommandBarFlyoutCommandBar();

        //Act
        var native = AppBarModel.CanMapNatively(bar, out _);

        //Assert
        native.Should().BeFalse();
    }
}
