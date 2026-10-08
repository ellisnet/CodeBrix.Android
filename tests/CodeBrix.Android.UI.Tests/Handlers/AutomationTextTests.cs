using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// AP9-3 fences for the AutomationProperties mapping's platform-free rules (Handlers/Views/Portable/AutomationText):
/// the automation name becomes the native content description (null for no name, so TalkBack falls back to the
/// view's own text), the app's name wins over a handler's own label, and the automation id becomes a string tag.
/// </summary>
public class AutomationTextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" \t\n")]
    public void ContentDescriptionOf_no_name_is_null(string name)
    {
        //Act
        var description = AutomationText.ContentDescriptionOf(name);

        //Assert
        description.Should().BeNull();
    }

    [Theory]
    [InlineData("Save", "Save")]
    [InlineData("  Save the file ", "Save the file")]
    [InlineData("Line one\nline two", "Line one\nline two")]
    public void ContentDescriptionOf_a_name_is_the_trimmed_name(string name, string expected)
    {
        //Act
        var description = AutomationText.ContentDescriptionOf(name);

        //Assert
        description.Should().Be(expected);
    }

    [Fact]
    public void ContentDescriptionOr_prefers_the_automation_name_over_the_handler_label()
    {
        //Act
        var description = AutomationText.ContentDescriptionOr(" Customer rating ", "Rating 3 of 5");

        //Assert
        description.Should().Be("Customer rating");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ContentDescriptionOr_without_a_name_keeps_the_handler_label(string name)
    {
        //Act
        var description = AutomationText.ContentDescriptionOr(name, "Rating 3 of 5");

        //Assert
        description.Should().Be("Rating 3 of 5");
    }

    [Fact]
    public void ContentDescriptionOr_without_a_name_or_label_is_null()
    {
        //Act
        var description = AutomationText.ContentDescriptionOr(null, null);

        //Assert
        description.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("SaveButton", "SaveButton")]
    [InlineData(" spaced ", " spaced ")]
    public void TagOf_is_the_id_as_it_is_or_null(string id, string expected)
    {
        //Act
        var tag = AutomationText.TagOf(id);

        //Assert
        tag.Should().Be(expected);
    }
}
