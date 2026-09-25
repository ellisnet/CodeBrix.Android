using System.Linq;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Policy;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class AdaptivePolicyTests
{
    private static readonly WindowSizeClass Compact = new(411, 915, false);
    private static readonly WindowSizeClass Medium = new(700, 900, false);
    private static readonly WindowSizeClass Expanded = new(1280, 800, false);
    private static readonly WindowSizeClass Docked = new(1920, 1080, true);

    public AdaptivePolicyTests() => AdaptivePolicy.Reset();

    [Theory]
    [InlineData(3, "BottomBar")]
    [InlineData(5, "BottomBar")]
    [InlineData(6, "ModalDrawer")]
    public void NavigationView_in_a_Compact_window_is_a_bottom_bar_up_to_five_destinations(int items, string expectedName) =>
        AdaptivePolicy.NavigationView(Compact, NavigationViewPaneDisplayMode.Auto, items).ToString().Should().Be(expectedName);

    [Theory]
    [InlineData(7, "Rail")]
    [InlineData(8, "ModalDrawer")]
    public void NavigationView_in_a_Medium_window_is_a_rail_up_to_seven_destinations(int items, string expectedName) =>
        AdaptivePolicy.NavigationView(Medium, NavigationViewPaneDisplayMode.Auto, items).ToString().Should().Be(expectedName);

    [Fact]
    public void NavigationView_in_an_Expanded_window_is_a_persistent_drawer() =>
        AdaptivePolicy.NavigationView(Expanded, NavigationViewPaneDisplayMode.Auto, 12).Should().Be(NavigationContainer.PersistentDrawer);

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Left)]
    [InlineData(NavigationViewPaneDisplayMode.LeftCompact)]
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal)]
    [InlineData(NavigationViewPaneDisplayMode.Top)]
    public void NavigationView_with_an_explicit_PaneDisplayMode_keeps_its_template(NavigationViewPaneDisplayMode mode) =>
        AdaptivePolicy.NavigationView(Compact, mode, 3).Should().Be(NavigationContainer.Template);

    [Fact]
    public void NavigationView_keeps_its_template_when_the_adaptive_mapping_is_off()
    {
        //Arrange
        AdaptivePolicy.AdaptiveNavigationView = false;

        //Act
        var container = AdaptivePolicy.NavigationView(Compact, NavigationViewPaneDisplayMode.Auto, 3);

        //Assert
        container.Should().Be(NavigationContainer.Template);
        AdaptivePolicy.Reset();
    }

    [Fact]
    public void ContentDialog_is_full_screen_only_for_XAML_content_in_a_Compact_window()
    {
        //Assert
        AdaptivePolicy.ContentDialog(Compact, textContent: false).Should().Be(DialogForm.FullScreen);
        AdaptivePolicy.ContentDialog(Compact, textContent: true).Should().Be(DialogForm.Basic);
        AdaptivePolicy.ContentDialog(Medium, textContent: false).Should().Be(DialogForm.Basic);
        AdaptivePolicy.ContentDialog(Expanded, textContent: false).Should().Be(DialogForm.Basic);
    }

    [Theory]
    [InlineData(SplitViewDisplayMode.Inline, SplitViewDisplayMode.Overlay)]
    [InlineData(SplitViewDisplayMode.CompactInline, SplitViewDisplayMode.CompactOverlay)]
    [InlineData(SplitViewDisplayMode.Overlay, SplitViewDisplayMode.Overlay)]
    [InlineData(SplitViewDisplayMode.CompactOverlay, SplitViewDisplayMode.CompactOverlay)]
    public void SplitView_degrades_inline_modes_to_overlay_in_a_Compact_window(SplitViewDisplayMode declared, SplitViewDisplayMode expected)
    {
        //Assert
        AdaptivePolicy.SplitView(Compact, declared).Should().Be(expected);
        AdaptivePolicy.SplitView(Medium, declared).Should().Be(declared);
    }

    [Fact]
    public void The_overlay_rows_follow_the_width_and_the_pointer()
    {
        //Assert
        AdaptivePolicy.Flyout(Compact).Should().Be(FlyoutForm.BottomSheet);
        AdaptivePolicy.Flyout(Medium).Should().Be(FlyoutForm.Anchored);
        AdaptivePolicy.ContextFlyoutAtPointer(Docked).Should().BeTrue();
        AdaptivePolicy.ContextFlyoutAtPointer(Expanded).Should().BeFalse();
        AdaptivePolicy.ToolTip(Docked).Should().Be(ToolTipTrigger.LongPressAndHover);
        AdaptivePolicy.ToolTip(Compact).Should().Be(ToolTipTrigger.LongPress);
    }

    [Fact]
    public void The_bar_rows_follow_the_width_and_the_pointer()
    {
        //Assert
        AdaptivePolicy.CommandBar(Compact).Should().Be(CommandBarPlacement.BottomAppBar);
        AdaptivePolicy.CommandBar(Medium).Should().Be(CommandBarPlacement.TopToolbar);
        AdaptivePolicy.MenuBar(Docked).Should().Be(MenuBarForm.MenuBarRow);
        AdaptivePolicy.MenuBar(Expanded).Should().Be(MenuBarForm.ToolbarOverflow);
        AdaptivePolicy.ToolBar(Compact).Should().Be(ToolBarForm.ScrollingStripWithOverflow);
        AdaptivePolicy.ToolBar(Medium).Should().Be(ToolBarForm.FullStrip);
        AdaptivePolicy.ToolBar(Expanded).Should().Be(ToolBarForm.FullStripWithLabels);
        AdaptivePolicy.TriPane(Compact).Should().Be(TriPaneForm.OnePane);
        AdaptivePolicy.TriPane(Medium).Should().Be(TriPaneForm.SidePaneAndOneStacked);
        AdaptivePolicy.TriPane(Expanded).Should().Be(TriPaneForm.ThreePanes);
        AdaptivePolicy.TabView(Compact).Should().Be(TabRowForm.ScrollableCloseByLongPress);
        AdaptivePolicy.TabView(Medium).Should().Be(TabRowForm.ScrollableWithCloseButtons);
    }

    [Fact]
    public void The_input_rows_follow_the_pointer()
    {
        //Assert
        AdaptivePolicy.Picker(Compact).Should().Be(PickerForm.Modal);
        AdaptivePolicy.Picker(Expanded).Should().Be(PickerForm.ModalTextInputFirst);
        AdaptivePolicy.Picker(new WindowSizeClass(411, 915, true)).Should().Be(PickerForm.ModalTextInputFirst);
        AdaptivePolicy.ScrollBars(Docked).Should().Be(ScrollBarForm.PersistentThin);
        AdaptivePolicy.ScrollBars(Expanded).Should().Be(ScrollBarForm.OverlayFading);
        AdaptivePolicy.ListSelection(Docked).Should().Be(SelectionInput.TouchAndPointer);
        AdaptivePolicy.Page(Docked).Should().Be(PageChrome.EdgeToEdgeWithCaptionBar);
        AdaptivePolicy.Page(Compact).Should().Be(PageChrome.EdgeToEdge);
    }

    [Theory]
    [InlineData("SharedAxisZ", true, "FadeThrough")]
    [InlineData("SharedAxisZ", false, "SharedAxisZ")]
    [InlineData("SharedAxisX", true, "SharedAxisX")]
    [InlineData("None", true, "None")]
    public void FrameMotion_fades_through_between_top_level_destinations(string kindName, bool underNativeNavigation, string expectedName) =>
        AdaptivePolicy.FrameMotion(System.Enum.Parse<NavigationMotionKind>(kindName), underNativeNavigation).ToString().Should().Be(expectedName);

    [Fact]
    public void Rows_document_the_fourteen_rows_of_the_plan_table()
    {
        //Assert
        AdaptivePolicy.Rows.Should().HaveCount(14);
        AdaptivePolicy.Rows.Select(r => r.Element).Should().OnlyHaveUniqueItems();
        AdaptivePolicy.Rows.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.AppliedBy));
        AdaptivePolicy.Name.Should().Be("Material 3 Expressive as of 2026-09-23");
    }
}
