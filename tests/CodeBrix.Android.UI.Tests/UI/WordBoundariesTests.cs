using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class WordBoundariesTests
{
    [Theory]
    [InlineData("hello world", 2, false, 0, 5)]
    [InlineData("hello world", 5, false, 0, 5)]
    [InlineData("hello world", 5, true, 5, 1)]
    [InlineData("hello world", 6, true, 6, 5)]
    [InlineData("hello world", 11, false, 6, 5)]
    [InlineData("a, b", 1, true, 1, 1)]
    [InlineData("snake_case id", 3, false, 0, 10)]
    public void GetWordAt_returns_the_run_around_the_index(string text, int index, bool right, int start, int length)
    {
        //Act
        var word = WordBoundaries.GetWordAt(text, index, right);

        //Assert
        word.Should().Be((start, length));
    }

    [Fact]
    public void GetWordAt_returns_an_empty_word_for_empty_text()
    {
        //Act
        var word = WordBoundaries.GetWordAt(string.Empty, 3, true);

        //Assert
        word.Should().Be((0, 0));
    }
}
