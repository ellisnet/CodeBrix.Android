using System;
using System.IO;
using CodeBrix.Android.ParityScore.Scanning;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Scanning;

public class ExplainedListTests
{
    [Fact]
    public void Parse_skips_comments_and_blank_lines()
    {
        //Act
        var list = ExplainedList.Parse(new[] { "# comment", string.Empty, "A.B\tCProperty\tcore-tree\tWhy." });

        //Assert
        list.Count.Should().Be(1);
        list.Find("A.B.CProperty").Category.Should().Be("core-tree");
    }

    [Fact]
    public void Star_line_explains_the_property_on_any_type()
    {
        //Act
        var list = ExplainedList.Parse(new[] { "*\tTagProperty\tcore-tree\tApplication data." });

        //Assert
        list.Find("Some.Type.TagProperty").Should().NotBeNull();
        list.Find("Some.Type.NameProperty").Should().BeNull();
    }

    [Fact]
    public void Line_without_four_fields_is_rejected()
    {
        //Act
        Action act = () => ExplainedList.Parse(new[] { "A.B\tCProperty\tcore-tree" });

        //Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Shipped_list_parses()
    {
        //Arrange
        var root = FixtureSupport.RepositoryRoot();
        root.Should().NotBeNull();

        //Act
        var list = ExplainedList.Load(Path.Combine(root, "tools", "CodeBrix.Android.ParityScore", "declined-explained.tsv"));

        //Assert
        list.Count.Should().BeGreaterThan(20);
        list.Find("Microsoft.UI.Xaml.FrameworkElement.WidthProperty").Category.Should().Be("core-layout");
    }

    [Fact]
    public void Missing_file_is_an_empty_list()
    {
        //Act
        var list = ExplainedList.Load(Path.Combine(FixtureSupport.TempDirectory(), "none.tsv"));

        //Assert
        list.Count.Should().Be(0);
    }
}
