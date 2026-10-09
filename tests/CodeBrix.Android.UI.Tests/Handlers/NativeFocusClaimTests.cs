using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

public class NativeFocusClaimTests
{
    [Theory]
    [InlineData(FocusState.Programmatic)]
    [InlineData(FocusState.Pointer)]
    [InlineData(FocusState.Keyboard)]
    public void A_box_Core_focused_asks_for_the_Android_focus(FocusState state)
    {
        //Act
        var action = NativeFocusClaim.OnCoreFocus(state, editorHasFocus: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.RequestNow);
    }

    [Fact]
    public void A_box_whose_editor_already_has_the_Android_focus_asks_nothing()
    {
        //Act
        var action = NativeFocusClaim.OnCoreFocus(FocusState.Programmatic, editorHasFocus: true);

        //Assert
        action.Should().Be(NativeFocusClaimAction.None);
    }

    [Fact]
    public void An_unfocused_box_asks_nothing()
    {
        //Act
        var action = NativeFocusClaim.OnCoreFocus(FocusState.Unfocused, editorHasFocus: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.None);
    }

    [Fact]
    public void A_refused_request_is_retried_after_the_next_layout()
    {
        //Act
        var action = NativeFocusClaim.AfterRequest(granted: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.RetryAfterLayout);
    }

    [Fact]
    public void A_granted_request_needs_no_retry()
    {
        //Act
        var action = NativeFocusClaim.AfterRequest(granted: true);

        //Assert
        action.Should().Be(NativeFocusClaimAction.None);
    }

    [Fact]
    public void A_pending_retry_asks_again_after_a_layout_while_Core_keeps_the_focus()
    {
        //Act
        var action = NativeFocusClaim.OnLayout(pending: true, FocusState.Pointer, editorHasFocus: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.RequestNow);
    }

    [Fact]
    public void A_pending_retry_is_dropped_when_Core_moved_the_focus_away()
    {
        //Act
        var action = NativeFocusClaim.OnLayout(pending: true, FocusState.Unfocused, editorHasFocus: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.None);
    }

    [Fact]
    public void A_layout_without_a_pending_retry_asks_nothing()
    {
        //Act
        var action = NativeFocusClaim.OnLayout(pending: false, FocusState.Programmatic, editorHasFocus: false);

        //Assert
        action.Should().Be(NativeFocusClaimAction.None);
    }
}
