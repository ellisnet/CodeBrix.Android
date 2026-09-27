using System.Linq;
using CodeBrix.Android.UI.Portable.TextInput;
using SilverAssertions;
using Windows.System;
using Xunit;

namespace CodeBrix.Android.UI.Tests.TextInput;

/// <summary>
/// AP7-B: what a soft keyboard's committed text and delete requests become for a custom text-entry control (the
/// TerminalView and AdvancedTextEdit add-ins read KeyDown): the Platform software keyboard's key table, one Enter per
/// line break, Backspace/Delete presses for a delete around the cursor.
/// </summary>
public class TextInputKeystrokesTests
{
    [Fact]
    public void Letters_digits_and_the_space_bar_carry_their_key_and_their_character()
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForText("aZ5 ");

        //Assert
        strokes.Should().Equal(
            new TextInputKeystroke(VirtualKey.A, 'a'),
            new TextInputKeystroke(VirtualKey.Z, 'Z'),
            new TextInputKeystroke(VirtualKey.Number5, '5'),
            new TextInputKeystroke(VirtualKey.Space, ' '));
    }

    [Fact]
    public void Punctuation_and_letters_no_US_key_names_carry_only_their_character()
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForText("-é中");

        //Assert
        strokes.Should().Equal(
            new TextInputKeystroke(VirtualKey.None, '-'),
            new TextInputKeystroke(VirtualKey.None, 'é'),
            new TextInputKeystroke(VirtualKey.None, '中'));
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\r\nb")]
    public void Every_kind_of_line_break_is_one_Enter_press_with_no_character(string text)
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForText(text);

        //Assert
        strokes.Should().Equal(
            new TextInputKeystroke(VirtualKey.A, 'a'),
            new TextInputKeystroke(VirtualKey.Enter, null),
            new TextInputKeystroke(VirtualKey.B, 'b'));
    }

    [Fact]
    public void A_tab_is_the_Tab_key_and_a_backspace_or_DEL_character_is_Backspace()
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForText("\t\b\x7f");

        //Assert
        strokes.Select(s => s.Key).Should().Equal(VirtualKey.Tab, VirtualKey.Back, VirtualKey.Back);
        strokes.Should().OnlyContain(s => s.Character == null);
    }

    [Fact]
    public void No_text_types_nothing()
    {
        //Arrange
        //Act
        var none = TextInputKeystrokes.ForText(null);
        var empty = TextInputKeystrokes.ForText(string.Empty);

        //Assert
        none.Should().BeEmpty();
        empty.Should().BeEmpty();
    }

    [Fact]
    public void A_delete_around_the_cursor_is_Backspace_presses_before_it_and_Delete_presses_after_it()
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForDeletion(2, 1);

        //Assert
        strokes.Should().Equal(
            new TextInputKeystroke(VirtualKey.Back, null),
            new TextInputKeystroke(VirtualKey.Back, null),
            new TextInputKeystroke(VirtualKey.Delete, null));
    }

    [Fact]
    public void A_negative_delete_count_deletes_nothing()
    {
        //Arrange
        //Act
        var strokes = TextInputKeystrokes.ForDeletion(-1, -3);

        //Assert
        strokes.Should().BeEmpty();
    }

    [Fact]
    public void The_terminal_profile_turns_suggestions_off_and_makes_Enter_a_key()
    {
        //Arrange
        //Act
        var terminal = CoreTextInputProfile.Terminal;
        var text = CoreTextInputProfile.Default;

        //Assert
        terminal.Suggestions.Should().BeFalse();
        terminal.MultiLine.Should().BeFalse();
        text.Suggestions.Should().BeTrue();
        text.MultiLine.Should().BeTrue();
    }
}
