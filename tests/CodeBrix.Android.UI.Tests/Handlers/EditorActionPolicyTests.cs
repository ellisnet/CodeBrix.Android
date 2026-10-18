using System;
using System.IO;
using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// EditorActionPolicy: the soft keyboard's action key raises Enter; on API 33 a key-event Enter on a single-line box
/// arrives as IME_NULL with the key event - it raises Enter and is consumed (down and up) so Android does not move the
/// focus to the next field, as the desktop heads never do; a multi-line box and anything else keep Android's handling.
/// </summary>
public sealed class EditorActionPolicyTests
{
    [Fact]
    public void the_soft_keyboard_action_key_raises_enter()
    {
        //Act
        var response = EditorActionPolicy.Decide(isImeNull: false, hasEnterKeyEvent: false, isKeyDown: false, acceptsReturn: false);

        //Assert
        response.Should().Be(EditorActionResponse.RaiseEnter);
    }

    [Fact]
    public void a_key_event_enter_down_on_a_single_line_box_raises_enter_and_is_consumed()
    {
        //Act
        var response = EditorActionPolicy.Decide(isImeNull: true, hasEnterKeyEvent: true, isKeyDown: true, acceptsReturn: false);

        //Assert
        response.Should().Be(EditorActionResponse.RaiseEnterAndConsume);
    }

    [Fact]
    public void the_key_up_of_that_enter_is_consumed_so_the_focus_stays()
    {
        //Act
        var response = EditorActionPolicy.Decide(isImeNull: true, hasEnterKeyEvent: true, isKeyDown: false, acceptsReturn: false);

        //Assert
        response.Should().Be(EditorActionResponse.ConsumeOnly);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void a_multi_line_box_or_an_ime_null_without_an_enter_key_is_ignored(bool hasEnterKeyEvent, bool acceptsReturn)
    {
        //Act
        var response = EditorActionPolicy.Decide(isImeNull: true, hasEnterKeyEvent, isKeyDown: true, acceptsReturn);

        //Assert
        response.Should().Be(EditorActionResponse.Ignore);
    }

    [Fact]
    public void the_text_box_handler_asks_the_policy_and_consumes_an_unhandled_key_event_enter()
    {
        //Arrange - TextBoxHandler is Android-only, so its source is the fence
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Handlers", "Text", "TextBoxHandler.cs"));

        //Assert
        source.Should().Contain("EditorActionPolicy.Decide(");
        source.Should().Contain("e.Handled = handled || response == EditorActionResponse.RaiseEnterAndConsume;");
        source.Should().NotContain("if (e.ActionId == AImeAction.ImeNull || Element?.XamlRoot is not { } root)");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return directory.FullName;
    }
}
