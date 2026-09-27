using System;
using System.IO;
using CodeBrix.Android.ParityScore.Options;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Options;

public class ParityOptionsTests
{
    [Fact]
    public void Parse_reads_every_option()
    {
        //Arrange
        var folder = FixtureSupport.TempDirectory();
        File.WriteAllText(Path.Combine(folder, "CodeBrix.Android.UI.dll"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "Other.dll"), string.Empty);

        //Act
        var options = ParityOptions.Parse(new[] { "--intake-lib", "/tmp/lib", "--android", folder, "--out", "/tmp/out", "--explained", "/tmp/e.tsv", "--label", "pin 1" });

        //Assert
        options.IntakeLibDirectory.Should().Be("/tmp/lib");
        options.OutputDirectory.Should().Be("/tmp/out");
        options.ExplainedFile.Should().Be("/tmp/e.tsv");
        options.Labels.Should().Equal("pin 1");
        options.AndroidAssemblies.Should().HaveCount(1);
    }

    [Fact]
    public void Same_assembly_in_two_folders_is_read_once()
    {
        //Arrange
        var first = FixtureSupport.TempDirectory();
        var second = FixtureSupport.TempDirectory();
        File.WriteAllText(Path.Combine(first, "CodeBrix.Android.dll"), string.Empty);
        File.WriteAllText(Path.Combine(second, "CodeBrix.Android.dll"), string.Empty);

        //Act
        var options = ParityOptions.Parse(new[] { "--intake-lib", "/tmp/lib", "--android", first, "--android", second, "--out", "/tmp/out" });

        //Assert
        options.AndroidAssemblies.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_rejects_an_unknown_option()
    {
        //Act
        Action act = () => ParityOptions.Parse(new[] { "--nonsense", "x" });

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_requires_the_android_assemblies()
    {
        //Act
        Action act = () => ParityOptions.Parse(new[] { "--intake-lib", "/tmp/lib", "--out", "/tmp/out" });

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_rejects_an_option_without_value()
    {
        //Act
        Action act = () => ParityOptions.Parse(new[] { "--intake-lib" });

        //Assert
        act.Should().Throw<ArgumentException>();
    }
}
