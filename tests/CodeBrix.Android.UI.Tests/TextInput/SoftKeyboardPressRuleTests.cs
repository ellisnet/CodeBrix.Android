using CodeBrix.Android.UI.Portable.TextInput;
using Microsoft.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.TextInput;

/// <summary>
/// AP9-4: a custom text control (TerminalView, AdvancedTextEdit) summons the soft keyboard for a finger or pen tap only -
/// a press on the control that has the focus, or the focus a tap gives it (Tapped, right after the release) - never for
/// focus alone (programmatic, keyboard, a page's first focus) and never for a mouse click; at most once per tap.
/// </summary>
public class SoftKeyboardPressRuleTests
{
    [Fact]
    public void Focus_with_no_tap_before_it_never_summons_the_keyboard()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var shows = rule.OnFocused(1000, lastReleaseOnControl: false);

        //Assert
        shows.Should().BeFalse();
    }

    [Fact]
    public void A_finger_and_a_pen_summon_the_keyboard_a_mouse_does_not()
    {
        //Arrange
        //Act
        //Assert
        SoftKeyboardPressRule.SummonsKeyboard(PointerDeviceType.Touch).Should().BeTrue();
        SoftKeyboardPressRule.SummonsKeyboard(PointerDeviceType.Pen).Should().BeTrue();
        SoftKeyboardPressRule.SummonsKeyboard(PointerDeviceType.Mouse).Should().BeFalse();
    }

    [Fact]
    public void A_finger_press_on_the_focused_control_shows_the_keyboard_on_the_press()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var onPress = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: true);
        var onRelease = rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: true, now: 100);
        var onRefocus = rule.OnFocused(110, lastReleaseOnControl: true);

        //Assert
        onPress.Should().BeTrue();
        onRelease.Should().BeFalse();
        onRefocus.Should().BeFalse();
    }

    [Fact]
    public void The_same_press_and_release_seen_on_the_control_and_at_the_root_show_the_keyboard_once()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var first = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: true);
        var second = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: true);
        var release = rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: true, now: 100);
        var releaseAgain = rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: true, now: 100);

        //Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        release.Should().BeFalse();
        releaseAgain.Should().BeFalse();
    }

    [Fact]
    public void A_press_that_gives_the_control_its_focus_while_it_is_routed_shows_the_keyboard_when_the_root_sees_it()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var onControl = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        var atRoot = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: true);
        var release = rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: true, now: 100);

        //Assert
        onControl.Should().BeFalse();
        atRoot.Should().BeTrue();
        release.Should().BeFalse();
    }

    [Fact]
    public void A_press_that_gives_the_control_its_focus_while_the_release_is_routed_shows_the_keyboard_on_the_release()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var press = rule.OnPressed(1, 10, PointerDeviceType.Pen, onFocusedControl: false);
        var release = rule.OnReleased(1, 11, PointerDeviceType.Pen, onFocusedControl: true, now: 100);

        //Assert
        press.Should().BeFalse();
        release.Should().BeTrue();
    }

    [Fact]
    public void A_tap_that_gives_the_control_its_focus_after_the_release_shows_the_keyboard_once()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();
        rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: false, now: 100);

        //Act
        var focused = rule.OnFocused(112, lastReleaseOnControl: true);
        var focusedAgain = rule.OnFocused(120, lastReleaseOnControl: true);

        //Assert
        focused.Should().BeTrue();
        focusedAgain.Should().BeFalse();
    }

    [Fact]
    public void A_focus_long_after_the_tap_does_not_summon_the_keyboard()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();
        rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: false, now: 100);

        //Act
        var focused = rule.OnFocused(100 + SoftKeyboardPressRule.FocusAfterReleaseMilliseconds + 1, lastReleaseOnControl: true);

        //Assert
        focused.Should().BeFalse();
    }

    [Fact]
    public void A_focus_after_a_tap_elsewhere_does_not_summon_the_keyboard()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();
        rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: false, now: 100);

        //Act
        var focused = rule.OnFocused(110, lastReleaseOnControl: false);

        //Assert
        focused.Should().BeFalse();
    }

    [Fact]
    public void A_mouse_click_never_shows_the_keyboard_on_the_press_or_on_the_focus_it_gives()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var press = rule.OnPressed(1, 10, PointerDeviceType.Mouse, onFocusedControl: true);
        var release = rule.OnReleased(1, 11, PointerDeviceType.Mouse, onFocusedControl: true, now: 100);
        var focused = rule.OnFocused(110, lastReleaseOnControl: true);

        //Assert
        press.Should().BeFalse();
        release.Should().BeFalse();
        focused.Should().BeFalse();
    }

    [Fact]
    public void A_cancelled_press_shows_nothing()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var press = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        rule.OnCanceled(1);
        var focused = rule.OnFocused(110, lastReleaseOnControl: true);

        //Assert
        press.Should().BeFalse();
        focused.Should().BeFalse();
    }

    [Fact]
    public void Every_new_tap_after_a_dismissal_shows_the_keyboard_again()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        var first = rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: true);
        rule.OnReleased(1, 11, PointerDeviceType.Touch, onFocusedControl: true, now: 100);
        var second = rule.OnPressed(2, 20, PointerDeviceType.Touch, onFocusedControl: true);
        rule.OnReleased(2, 21, PointerDeviceType.Touch, onFocusedControl: true, now: 900);

        //Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void A_release_of_another_pointer_does_not_show_the_keyboard()
    {
        //Arrange
        var rule = new SoftKeyboardPressRule();

        //Act
        rule.OnPressed(1, 10, PointerDeviceType.Touch, onFocusedControl: false);
        var release = rule.OnReleased(2, 21, PointerDeviceType.Touch, onFocusedControl: true, now: 100);

        //Assert
        release.Should().BeFalse();
    }
}
