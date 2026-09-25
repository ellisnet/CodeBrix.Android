using CodeBrix.Android.UI.Portable.Projection;
using Microsoft.UI.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Projection;

public class ProjectionTextStyleTests
{
    [Fact]
    public void NoWrap_is_one_line()
    {
        //Act
        var style = ProjectionTextStyle.From(TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, 5);

        //Assert
        style.Should().Be(new ProjectionTextStyle(ProjectionTextAlignment.Start, 1, true, false));
    }

    [Fact]
    public void Wrap_uses_MaxLines_or_no_limit()
    {
        //Act
        var limited = ProjectionTextStyle.From(TextAlignment.Left, TextWrapping.Wrap, TextTrimming.None, 3);
        var unlimited = ProjectionTextStyle.From(TextAlignment.Left, TextWrapping.WrapWholeWords, TextTrimming.None, 0);

        //Assert
        limited.MaxLines.Should().Be(3);
        limited.SingleLine.Should().BeFalse();
        unlimited.MaxLines.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData(TextAlignment.Left, ProjectionTextAlignment.Start)]
    [InlineData(TextAlignment.Justify, ProjectionTextAlignment.Start)]
    [InlineData(TextAlignment.Center, ProjectionTextAlignment.Center)]
    [InlineData(TextAlignment.Right, ProjectionTextAlignment.End)]
    public void Alignment_maps_to_start_center_end(TextAlignment alignment, object expected)
    {
        //Act
        var style = ProjectionTextStyle.From(alignment, TextWrapping.Wrap, TextTrimming.None, 0);

        //Assert
        style.Alignment.Should().Be((ProjectionTextAlignment)expected);
    }

    [Fact]
    public void Trimming_ellipsizes()
    {
        //Act
        var style = ProjectionTextStyle.From(TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, 0);

        //Assert
        style.Ellipsize.Should().BeTrue();
    }
}
