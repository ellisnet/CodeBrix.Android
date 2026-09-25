using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

[Collection(HostFreeCoreCollection.Name)]
public class DialogTextTests
{
    public DialogTextTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_string_is_text()
    {
        //Act
        var isText = DialogText.TryGetText("Delete the file?", null, out var text);

        //Assert
        isText.Should().BeTrue();
        text.Should().Be("Delete the file?");
    }

    [Fact]
    public void No_value_is_empty_text()
    {
        //Act
        var isText = DialogText.TryGetText(null, null, out var text);

        //Assert
        isText.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public void A_plain_TextBlock_is_its_text()
    {
        //Arrange (what SimpleViewModel's ShowInfo puts in its dialog)
        var textBlock = new TextBlock { Text = "The processed text has been copied." };

        //Act
        var isText = DialogText.TryGetText(textBlock, null, out var text);

        //Assert
        isText.Should().BeTrue();
        text.Should().Be("The processed text has been copied.");
    }

    [Fact]
    public void A_TextBlock_of_runs_and_line_breaks_is_their_text()
    {
        //Arrange
        var textBlock = new TextBlock();
        textBlock.Inlines.Add(new Run { Text = "first" });
        textBlock.Inlines.Add(new LineBreak());
        textBlock.Inlines.Add(new Run { Text = "second" });

        //Act
        var isText = DialogText.TryGetText(textBlock, null, out var text);

        //Assert
        isText.Should().BeTrue();
        text.Should().Be("first\nsecond");
    }

    [Fact]
    public void A_TextBlock_with_formatted_spans_is_not_text()
    {
        //Arrange
        var textBlock = new TextBlock();
        textBlock.Inlines.Add(new Run { Text = "see " });
        textBlock.Inlines.Add(new Bold());

        //Act
        var isText = DialogText.TryGetText(textBlock, null, out _);

        //Assert
        isText.Should().BeFalse();
    }

    [Fact]
    public void A_panel_of_elements_is_not_text()
    {
        //Act
        var isText = DialogText.TryGetText(new StackPanel(), null, out _);

        //Assert
        isText.Should().BeFalse();
    }

    [Fact]
    public void A_value_shown_through_a_template_is_not_text()
    {
        //Act
        var isText = DialogText.TryGetText("value", new DataTemplate(), out _);

        //Assert
        isText.Should().BeFalse();
    }

    [Fact]
    public void Another_object_is_shown_by_its_ToString()
    {
        //Act
        var isText = DialogText.TryGetText(42, null, out var text);

        //Assert
        isText.Should().BeTrue();
        text.Should().Be("42");
    }
}
