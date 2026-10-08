using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests;

public class IntakeOptionsTests
{
    [Fact]
    public void Parse_reads_every_option()
    {
        //Act
        var options = IntakeOptions.Parse(new[]
        {
            "--intake-dir", "/tmp/i", "--gates-dir", "/tmp/g", "--bcl-dir", "/tmp/b", "--src-dir", "/tmp/s",
            "--platform-version", "1.2.3", "--sources", "/tmp/i/s.txt", "--packages", "/tmp/i/p.txt", "--seam-gate", "true",
        });

        //Assert
        // Path options are normalised with Path.GetFullPath, which is host-specific ("/tmp/i" stays as is on
        // Linux and macOS, becomes "C:\tmp\i" on Windows), so the expected values go through the same call.
        options.IntakeDirectory.Should().Be(Path.GetFullPath("/tmp/i"));
        options.GatesDirectory.Should().Be(Path.GetFullPath("/tmp/g"));
        options.BclDirectory.Should().Be(Path.GetFullPath("/tmp/b"));
        options.SourceDirectory.Should().Be(Path.GetFullPath("/tmp/s"));
        options.SourcesFile.Should().Be(Path.GetFullPath("/tmp/i/s.txt"));
        options.PackagesFile.Should().Be(Path.GetFullPath("/tmp/i/p.txt"));
        options.PlatformVersion.Should().Be("1.2.3");
        options.SeamGateEnabled.Should().BeTrue();
    }

    [Fact]
    public void Parse_rejects_an_unknown_option()
    {
        //Act
        Action act = () => IntakeOptions.Parse(new[] { "--nonsense", "x" });

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_requires_the_intake_folder()
    {
        //Act
        Action act = () => IntakeOptions.Parse(new[] { "--gates-dir", "/tmp/g" });

        //Assert
        act.Should().Throw<ArgumentException>();
    }
}
