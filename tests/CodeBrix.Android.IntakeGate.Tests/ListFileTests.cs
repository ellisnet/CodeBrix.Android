using System;
using System.IO;
using CodeBrix.Android.IntakeGate.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests;

public class ListFileTests
{
    [Fact]
    public void ReadLines_skips_comments_and_blank_lines_and_trims()
    {
        //Arrange
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllLines(path, new[] { "# comment", string.Empty, "  first  ", "second" });

        //Act
        var lines = ListFile.ReadLines(path);
        File.Delete(path);

        //Assert
        lines.Should().ContainInOrder("first", "second");
        lines.Count.Should().Be(2);
    }

    [Fact]
    public void ReadLines_returns_nothing_for_a_missing_file()
        => ListFile.ReadLines(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).Count.Should().Be(0);
}
