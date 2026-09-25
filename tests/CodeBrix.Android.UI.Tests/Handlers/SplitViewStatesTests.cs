using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP10-A: the SplitView template state Core names and the one the adaptive table shows.</summary>
public class SplitViewStatesTests
{
    private static readonly WindowSizeClass Compact = new(400, 800, false);
    private static readonly WindowSizeClass Expanded = new(1080, 1920, false);

    [Theory]
    [InlineData(false, SplitViewDisplayMode.Overlay, SplitViewPanePlacement.Left, "Closed")]
    [InlineData(true, SplitViewDisplayMode.Overlay, SplitViewPanePlacement.Left, "OpenOverlayLeft")]
    [InlineData(false, SplitViewDisplayMode.Inline, SplitViewPanePlacement.Right, "Closed")]
    [InlineData(true, SplitViewDisplayMode.Inline, SplitViewPanePlacement.Right, "OpenInlineRight")]
    [InlineData(false, SplitViewDisplayMode.CompactOverlay, SplitViewPanePlacement.Left, "ClosedCompactLeft")]
    [InlineData(true, SplitViewDisplayMode.CompactOverlay, SplitViewPanePlacement.Left, "OpenCompactOverlayLeft")]
    [InlineData(false, SplitViewDisplayMode.CompactInline, SplitViewPanePlacement.Left, "ClosedCompactLeft")]
    [InlineData(true, SplitViewDisplayMode.CompactInline, SplitViewPanePlacement.Left, "OpenInlineLeft")]
    public void CoreState_names_the_states_the_way_Core_does(bool open, SplitViewDisplayMode mode, SplitViewPanePlacement placement, string expected)
    {
        //Act
        var state = SplitViewStates.CoreState(open, mode, placement);

        //Assert
        state.Should().Be(expected);
    }

    [Fact]
    public void A_Compact_window_shows_an_open_Inline_pane_as_an_Overlay_pane()
    {
        //Act
        var compact = SplitViewStates.Shown(Compact, true, SplitViewDisplayMode.Inline, SplitViewPanePlacement.Left);
        var expanded = SplitViewStates.Shown(Expanded, true, SplitViewDisplayMode.Inline, SplitViewPanePlacement.Left);

        //Assert
        compact.Should().Be("OpenOverlayLeft");
        expanded.Should().Be("OpenInlineLeft");
    }

    [Fact]
    public void A_Compact_window_shows_an_open_CompactInline_pane_as_a_CompactOverlay_pane()
    {
        //Act
        var open = SplitViewStates.Shown(Compact, true, SplitViewDisplayMode.CompactInline, SplitViewPanePlacement.Left);
        var shut = SplitViewStates.Shown(Compact, false, SplitViewDisplayMode.CompactInline, SplitViewPanePlacement.Left);

        //Assert
        open.Should().Be("OpenCompactOverlayLeft");
        shut.Should().Be("ClosedCompactLeft");
    }
}
