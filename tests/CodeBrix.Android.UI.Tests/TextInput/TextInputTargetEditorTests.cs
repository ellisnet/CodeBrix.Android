using System.Collections.Generic;
using CodeBrix.Android.UI.Portable.TextInput;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.TextInput;

/// <summary>
/// AP7-B AdvancedTextEdit: the TARGET extension of the shared soft-keyboard connection - what an Android input method's
/// calls do to a custom text-entry control's own text (surrounding text, composition, commit, delete around the cursor,
/// set selection, batches, and when the input method is told the selection moved).
/// </summary>
public class TextInputTargetEditorTests
{
    [Fact]
    public void A_replacement_of_the_selection_is_typed()
    {
        //Arrange
        //Act
        var kind = TextInputTargetEditor.Plan(2, 4, "xy", 2, 4, "cd", out var typed);

        //Assert
        kind.Should().Be(TextInputEditKind.Typed);
        typed.Should().Be("xy");
    }

    [Fact]
    public void A_composition_that_grows_at_the_caret_types_only_the_added_letters()
    {
        //Arrange
        //Act
        var kind = TextInputTargetEditor.Plan(0, 3, "hell", 3, 3, "hel", out var typed);

        //Assert
        kind.Should().Be(TextInputEditKind.Appended);
        typed.Should().Be("l");
    }

    [Fact]
    public void A_composition_that_changes_a_letter_is_replaced_as_it_is()
    {
        //Arrange
        //Act
        var kind = TextInputTargetEditor.Plan(0, 3, "the", 3, 3, "teh", out var typed);

        //Assert
        kind.Should().Be(TextInputEditKind.Replaced);
        typed.Should().BeNull();
    }

    [Fact]
    public void Text_the_range_already_holds_changes_nothing()
    {
        //Arrange
        //Act
        var kind = TextInputTargetEditor.Plan(0, 3, "abc", 3, 3, "abc", out _);

        //Assert
        kind.Should().Be(TextInputEditKind.None);
    }

    [Fact]
    public void A_growth_that_would_start_with_half_a_surrogate_pair_is_replaced_not_appended()
    {
        //Arrange
        var face = "\U0001F600";

        //Act
        var kind = TextInputTargetEditor.Plan(0, 2, "a" + face[0] + face[1], 2, 2, "a" + face[0], out _);

        //Assert
        kind.Should().Be(TextInputEditKind.Replaced);
    }

    [Fact]
    public void The_text_around_the_cursor_is_clamped_and_never_splits_a_surrogate_pair()
    {
        //Arrange
        var face = "\U0001F600";
        var editor = new TextInputTargetEditor(new FakeTextTarget(face + "ab" + face, caret: 3));

        //Act
        var before = editor.GetTextBeforeCursor(2);
        var beforeAll = editor.GetTextBeforeCursor(100);
        var after = editor.GetTextAfterCursor(2);
        var afterAll = editor.GetTextAfterCursor(100);

        //Assert
        before.Should().Be("a");
        beforeAll.Should().Be(face + "a");
        after.Should().Be("b");
        afterAll.Should().Be("b" + face);
    }

    [Fact]
    public void Nothing_selected_is_no_selected_text()
    {
        //Arrange
        var target = new FakeTextTarget("hello", caret: 2);
        var editor = new TextInputTargetEditor(target);

        //Act
        var none = editor.GetSelectedText();
        target.Select(1, 4);
        var some = editor.GetSelectedText();

        //Assert
        none.Should().BeNull();
        some.Should().Be("ell");
    }

    [Fact]
    public void A_word_composed_letter_by_letter_reaches_the_control_as_typing_and_stays_underlined()
    {
        //Arrange
        var target = new FakeTextTarget("x ", caret: 2);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.SetComposingText("h", 1);
        editor.SetComposingText("he", 1);
        editor.SetComposingText("hey", 1);

        //Assert
        target.Text.Should().Be("x hey");
        target.Log.Should().Equal("type:h", "type:e", "type:y");
        target.Composition.Should().Be((2, 5));
        editor.ComposingStart.Should().Be(2);
        editor.ComposingEnd.Should().Be(5);
        target.SelectionStart.Should().Be(5);
    }

    [Fact]
    public void Committing_an_autocorrection_replaces_the_composition_and_ends_it()
    {
        //Arrange
        var target = new FakeTextTarget(string.Empty);
        var editor = new TextInputTargetEditor(target);
        editor.SetComposingText("teh", 1);

        //Act
        editor.Commit("the", 1);

        //Assert
        target.Text.Should().Be("the");
        target.Log[^1].Should().Be("replace:0,3,the");
        editor.IsComposing.Should().BeFalse();
        target.Composition.Should().Be((-1, -1));
        target.SelectionStart.Should().Be(3);
        target.SelectionEnd.Should().Be(3);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 6)]
    [InlineData(0, 2)]
    [InlineData(-1, 1)]
    public void The_cursor_goes_where_the_input_method_asks_after_a_commit(int newCursorPosition, int expected)
    {
        //Arrange
        var target = new FakeTextTarget("abcdef", caret: 2);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.Commit("XYZ", newCursorPosition);

        //Assert
        target.Text.Should().Be("abXYZcdef");
        target.SelectionStart.Should().Be(expected);
        target.SelectionEnd.Should().Be(expected);
    }

    [Fact]
    public void A_delete_around_the_cursor_removes_the_text_on_both_sides_and_keeps_the_selection()
    {
        //Arrange
        var target = new FakeTextTarget("abcdefgh");
        target.Select(3, 5);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.DeleteSurroundingText(2, 1);

        //Assert
        target.Text.Should().Be("adegh");
        target.SelectionStart.Should().Be(1);
        target.SelectionEnd.Should().Be(3);
    }

    [Fact]
    public void A_delete_never_leaves_half_a_surrogate_pair()
    {
        //Arrange
        var face = "\U0001F600";
        var target = new FakeTextTarget("a" + face, caret: 3);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.DeleteSurroundingText(1, 0);

        //Assert
        target.Text.Should().Be("a");
        target.SelectionStart.Should().Be(1);
    }

    [Fact]
    public void A_delete_in_code_points_counts_a_surrogate_pair_as_one()
    {
        //Arrange
        var face = "\U0001F600";
        var target = new FakeTextTarget("ab" + face + face, caret: 6);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.DeleteSurroundingTextInCodePoints(2, 0);

        //Assert
        target.Text.Should().Be("ab");
    }

    [Fact]
    public void An_earlier_word_made_the_composition_again_is_replaced_by_the_commit()
    {
        //Arrange
        var target = new FakeTextTarget("one tow three", caret: 13);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.SetComposingRegion(7, 4);
        var region = target.Composition;
        editor.Commit("two", 1);

        //Assert
        region.Should().Be((4, 7));
        target.Text.Should().Be("one two three");
        target.SelectionStart.Should().Be(7);
    }

    [Fact]
    public void A_selection_the_input_method_asks_for_is_clamped_to_the_text()
    {
        //Arrange
        var target = new FakeTextTarget("abc", caret: 0);
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.SetSelection(-5, 99);

        //Assert
        target.SelectionStart.Should().Be(0);
        target.SelectionEnd.Should().Be(3);
    }

    [Fact]
    public void The_input_method_hears_once_per_edit_and_once_per_batch_and_each_call_is_one_control_edit_group()
    {
        //Arrange
        var target = new FakeTextTarget(string.Empty);
        var editor = new TextInputTargetEditor(target);
        var reports = new List<bool>();
        editor.Report += reports.Add;

        //Act
        editor.Commit("a", 1);
        var afterOne = reports.Count;
        editor.BeginBatchEdit();
        editor.Commit("b", 1);
        editor.Commit("c", 1);
        var duringBatch = reports.Count;
        var stillOpen = editor.EndBatchEdit();

        //Assert
        afterOne.Should().Be(1);
        duringBatch.Should().Be(1);
        stillOpen.Should().BeFalse();
        reports.Should().Equal(false, false);
        target.Text.Should().Be("abc");
        target.Batches.Should().Be(3);
    }

    [Fact]
    public void A_change_made_elsewhere_ends_the_composition_and_is_reported()
    {
        //Arrange
        var target = new FakeTextTarget(string.Empty);
        var editor = new TextInputTargetEditor(target);
        editor.SetComposingText("wor", 1);
        var reports = new List<bool>();
        editor.Report += reports.Add;

        //Act
        target.TypeFromElsewhere("!");

        //Assert
        editor.IsComposing.Should().BeFalse();
        target.Composition.Should().Be((-1, -1));
        reports.Should().NotBeEmpty();
        reports.Should().NotContain(true);
    }

    [Fact]
    public void A_whole_new_text_asks_the_input_method_to_read_it_again()
    {
        //Arrange
        var target = new FakeTextTarget("old");
        var editor = new TextInputTargetEditor(target);
        var reports = new List<bool>();
        editor.Report += reports.Add;

        //Act
        target.ResetFromElsewhere("brand new");

        //Assert
        reports.Should().Equal(true);
    }

    [Fact]
    public void A_read_only_range_refuses_the_keyboard_and_ends_its_composition()
    {
        //Arrange
        var target = new FakeTextTarget("locked", caret: 3) { ReadOnly = (0, 6) };
        var editor = new TextInputTargetEditor(target);

        //Act
        editor.SetComposingText("zz", 1);
        editor.Commit("zz", 1);
        editor.DeleteSurroundingText(2, 2);

        //Assert
        target.Text.Should().Be("locked");
        editor.IsComposing.Should().BeFalse();
        target.Log.Should().BeEmpty();
    }

    [Fact]
    public void Select_all_selects_the_whole_text_even_when_the_control_has_no_command_for_it()
    {
        //Arrange
        var target = new FakeTextTarget("hello", caret: 1);
        var editor = new TextInputTargetEditor(target);

        //Act
        var selectAll = editor.Perform(CoreTextInputCommand.SelectAll);
        var copy = editor.Perform(CoreTextInputCommand.Copy);
        var paste = editor.Perform(CoreTextInputCommand.Paste);

        //Assert
        selectAll.Should().BeTrue();
        target.SelectionStart.Should().Be(0);
        target.SelectionEnd.Should().Be(5);
        copy.Should().BeTrue();
        paste.Should().BeFalse();
    }

    [Fact]
    public void Ending_the_session_removes_the_underline_and_disposes_the_target()
    {
        //Arrange
        var target = new FakeTextTarget(string.Empty);
        var editor = new TextInputTargetEditor(target);
        editor.SetComposingText("ab", 1);

        //Act
        editor.Detach();
        target.TypeFromElsewhere("c");

        //Assert
        target.Composition.Should().Be((-1, -1));
        target.Disposed.Should().BeTrue();
    }
}
