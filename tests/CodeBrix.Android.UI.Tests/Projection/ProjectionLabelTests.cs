using CodeBrix.Android.UI.Portable.Projection;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Projection;

public class ProjectionLabelTests
{
    [Fact]
    public void Build_shows_the_type_name_and_x_name()
    {
        //Act
        var label = ProjectionLabel.Build("Border", "Card", new ProjectionLabelKey(ProjectionLabelFormat.TypeOnly));

        //Assert
        label.Should().Be("Border #Card");
    }

    [Fact]
    public void Build_quotes_string_content()
    {
        //Act
        var label = ProjectionLabel.Build("Button", null, new ProjectionLabelKey(ProjectionLabelFormat.Content, "OK"));

        //Assert
        label.Should().Be("Button \"OK\"");
    }

    [Fact]
    public void Build_shows_text_or_else_the_placeholder_text()
    {
        //Act
        var withText = ProjectionLabel.Build("TextBox", null, new ProjectionLabelKey(ProjectionLabelFormat.Text, "abc", "type here"));
        var empty = ProjectionLabel.Build("TextBox", null, new ProjectionLabelKey(ProjectionLabelFormat.Text, "", "type here"));

        //Assert
        withText.Should().Be("TextBox \"abc\"");
        empty.Should().Be("TextBox placeholder=\"type here\"");
    }

    [Fact]
    public void Build_shows_item_count_and_selected_index()
    {
        //Act
        var label = ProjectionLabel.Build("ComboBox", null, new ProjectionLabelKey(ProjectionLabelFormat.Selection, Value: 3, Index: 0));

        //Assert
        label.Should().Be("ComboBox items=3 selected=0");
    }

    [Fact]
    public void Build_shows_value_of_maximum_or_indeterminate()
    {
        //Act
        var range = ProjectionLabel.Build("ProgressBar", null, new ProjectionLabelKey(ProjectionLabelFormat.Range, Value: 42.5, Maximum: 100));
        var indeterminate = ProjectionLabel.Build("ProgressBar", null, new ProjectionLabelKey(ProjectionLabelFormat.Range, Flag: true));

        //Assert
        range.Should().Be("ProgressBar 42.5/100");
        indeterminate.Should().Be("ProgressBar indeterminate");
    }

    [Fact]
    public void Build_shows_toggle_state_source_and_disabled()
    {
        //Act
        var toggle = ProjectionLabel.Build("ToggleSwitch", null, new ProjectionLabelKey(ProjectionLabelFormat.Toggle, Flag: true, IsDisabled: true));
        var image = ProjectionLabel.Build("Image", "Logo", new ProjectionLabelKey(ProjectionLabelFormat.Source, "ms-appx:///Assets/Logo.png"));
        var noSource = ProjectionLabel.Build("Image", null, new ProjectionLabelKey(ProjectionLabelFormat.Source));

        //Assert
        toggle.Should().Be("ToggleSwitch on (disabled)");
        image.Should().Be("Image #Logo source=\"ms-appx:///Assets/Logo.png\"");
        noSource.Should().Be("Image source=(none)");
    }

    [Fact]
    public void Build_keeps_long_text_on_one_short_line()
    {
        //Arrange
        var text = "line one\nline two " + new string('x', 100);

        //Act
        var label = ProjectionLabel.Build("TextBox", null, new ProjectionLabelKey(ProjectionLabelFormat.Text, text));

        //Assert
        label.Should().StartWith("TextBox \"line one line two ");
        label.Should().EndWith("…\"");
        label.Length.Should().Be("TextBox \"".Length + ProjectionLabel.MaxTextLength + 1);
    }

    [Fact]
    public void Label_keys_with_equal_values_are_equal()
    {
        //Arrange
        var a = new ProjectionLabelKey(ProjectionLabelFormat.Text, new string('a', 3), "p");
        var b = new ProjectionLabelKey(ProjectionLabelFormat.Text, "aaa", "p");

        //Act
        var equal = a == b;

        //Assert
        equal.Should().BeTrue();
    }
}
