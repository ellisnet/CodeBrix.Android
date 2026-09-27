using System.Collections.Generic;
using System.Text;
using CodeBrix.Android.UI.Portable.TextInput;
using CodeBrix.Platform.UI.TerminalView.Engine;
using CodeBrix.Platform.UI.TerminalView.Input;
using SilverAssertions;
using Windows.System;
using Xunit;

namespace CodeBrix.Android.UI.TerminalView.Tests.Input;

/// <summary>
/// What reaches a terminal's host when a person types on Android: the keystrokes CodeBrix.Android.UI's text-input
/// connection makes of a soft keyboard's text, and hardware keys, fed through the TerminalView Core's OWN key mapping
/// and VT encoder in the order TerminalControl.OnKeyDown / OnKeyUp use them (modifier tracking, then the chord
/// commands, then the encoding with the layout-composed character).
/// </summary>
public class SoftKeyboardToTerminalTests
{
    private const string Esc = "\u001b";

    [Fact]
    public void A_committed_command_line_reaches_the_host_as_typed_and_its_line_break_as_CR()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        var sent = terminal.Type(TextInputKeystrokes.ForText("ls -la /tmp\n"));

        //Assert
        sent.Should().Be("ls -la /tmp\r");
    }

    [Fact]
    public void Capitals_digits_and_symbols_of_the_soft_keyboard_keep_their_character()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        var sent = terminal.Type(TextInputKeystrokes.ForText("SET key \"v@1\";"));

        //Assert
        sent.Should().Be("SET key \"v@1\";");
    }

    [Fact]
    public void Letters_outside_ASCII_reach_the_host_unchanged()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        var sent = terminal.Type(TextInputKeystrokes.ForText("café 中文"));

        //Assert
        sent.Should().Be("café 中文");
    }

    [Fact]
    public void A_soft_keyboard_delete_is_the_DEL_byte_per_character_and_a_forward_delete_its_escape_sequence()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        var back = terminal.Type(TextInputKeystrokes.ForDeletion(3, 0));
        var forward = terminal.Type(TextInputKeystrokes.ForDeletion(0, 1));

        //Assert
        back.Should().Be("\x7f\x7f\x7f");
        forward.Should().Be(Esc + "[3~");
    }

    [Fact]
    public void A_soft_keyboard_tab_is_a_tab()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        var sent = terminal.Type(TextInputKeystrokes.ForText("\t"));

        //Assert
        sent.Should().Be("\t");
    }

    [Fact]
    public void A_hardware_Control_chord_is_its_C0_control_code()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        terminal.Down(VirtualKey.LeftControl);
        var sent = terminal.Press(VirtualKey.C, 'c');
        terminal.Up(VirtualKey.LeftControl);

        //Assert
        sent.Should().Be("\x03");
    }

    [Fact]
    public void A_hardware_Alt_chord_is_ESC_then_the_key()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        terminal.Down(VirtualKey.LeftMenu);
        var sent = terminal.Press(VirtualKey.X, 'x');
        terminal.Up(VirtualKey.LeftMenu);

        //Assert
        sent.Should().Be(Esc + "x");
    }

    [Fact]
    public void Control_Shift_C_and_V_are_the_clipboard_chords_and_send_nothing()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        terminal.Down(VirtualKey.LeftControl);
        terminal.Down(VirtualKey.LeftShift);
        var copy = terminal.Press(VirtualKey.C, 'C');
        var paste = terminal.Press(VirtualKey.V, 'V');
        terminal.Up(VirtualKey.LeftShift);
        terminal.Up(VirtualKey.LeftControl);

        //Assert
        copy.Should().Be("<Copy>");
        paste.Should().Be("<Paste>");
    }

    [Fact]
    public void Shift_PageUp_and_PageDown_page_the_scrollback_and_send_nothing()
    {
        //Arrange
        var terminal = new KeyboardRig();

        //Act
        terminal.Down(VirtualKey.LeftShift);
        var up = terminal.Press(VirtualKey.PageUp, null);
        var down = terminal.Press(VirtualKey.PageDown, null);
        terminal.Up(VirtualKey.LeftShift);

        //Assert
        up.Should().Be("<ScrollPageUp>");
        down.Should().Be("<ScrollPageDown>");
    }

    [Theory]
    [InlineData(false, "[A")]
    [InlineData(true, "OA")]
    public void The_Up_key_is_its_cursor_sequence_in_both_cursor_modes(bool applicationCursor, string sequence)
    {
        //Arrange
        var terminal = new KeyboardRig { ApplicationCursor = applicationCursor };

        //Act
        var sent = terminal.Press(VirtualKey.Up, null);

        //Assert
        sent.Should().Be(Esc + sequence);
    }

    [Fact]
    public void Soft_keyboard_text_after_a_released_hardware_modifier_is_plain_text()
    {
        //Arrange
        var terminal = new KeyboardRig();
        terminal.Down(VirtualKey.LeftControl);
        terminal.Press(VirtualKey.C, 'c');
        terminal.Up(VirtualKey.LeftControl);

        //Act
        var sent = terminal.Type(TextInputKeystrokes.ForText("c"));

        //Assert
        sent.Should().Be("c");
    }

    /// <summary>TerminalControl's key path (OnKeyDown / OnKeyUp) over the Core's own mapper and encoder.</summary>
    private sealed class KeyboardRig
    {
        private readonly TerminalInputEncoder _encoder = new();

        internal bool ApplicationCursor { get; init; }

        internal string Type(IReadOnlyList<TextInputKeystroke> strokes)
        {
            var sent = new StringBuilder();
            foreach (var stroke in strokes)
            {
                sent.Append(Press(stroke.Key, stroke.Character));
                Up(stroke.Key);
            }

            return sent.ToString();
        }

        internal void Down(VirtualKey key) => Press(key, null);

        internal void Up(VirtualKey key) => _encoder.UpdateModifier(VirtualKeyMapper.ToModifierKey(key), isDown: false);

        internal string Press(VirtualKey key, char? character)
        {
            if (_encoder.UpdateModifier(VirtualKeyMapper.ToModifierKey(key), isDown: true))
            {
                return string.Empty;
            }

            var terminalKey = VirtualKeyMapper.ToTerminalKey(key);
            var command = _encoder.GetCommand(terminalKey);
            if (command != TerminalKeyCommand.None)
            {
                return "<" + command + ">";
            }

            return _encoder.Encode(terminalKey, character, ApplicationCursor) ?? string.Empty;
        }
    }
}
