using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Intake;

/// <summary>
/// Acceptance of the pinned CodeBrix.Platform intake (artifacts/intake/&lt;version&gt;/, written by
/// build/intake/CodeBrix.Android.Intake.proj): the gates passed, the Core these tests run is
/// the extracted one, and it carries no SkiaSharp.
/// </summary>
public class IntakeAcceptanceTests
{
    private static readonly Lazy<string> RepoRoot = new(FindRepoRoot);
    private static readonly Lazy<string> PinnedVersion = new(ReadPinnedVersion);

    [Fact]
    public void Intake_report_is_for_the_pinned_version()
    {
        //Act
        var report = ReadIntakeFile("intake-gates.txt");

        //Assert
        report.Should().StartWith("CodeBrix.Android intake - CodeBrixPlatformVersion " + PinnedVersion.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Intake_gate_passed(int gate)
    {
        //Act
        var line = ReadIntakeFile("intake-gates.txt").Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("gate " + gate + " ", StringComparison.Ordinal));

        //Assert
        line.Should().NotBeNull();
        line.TrimEnd().Should().EndWith("PASS");
    }

    [Fact]
    public void Seam_fingerprint_gate_is_enforced_for_the_seam_build()
    {
        //Act
        var report = ReadIntakeFile("intake-gates.txt");
        var line = report.Split('\n').First(l => l.TrimStart().StartsWith("gate 5 ", StringComparison.Ordinal));

        //Assert
        line.TrimEnd().Should().EndWith("PASS");
        report.Should().NotContain("OFF until the pin moves to the seam build");
    }

    [Theory]
    [InlineData(typeof(UIElement))]
    [InlineData(typeof(Visual))]
    [InlineData(typeof(DispatcherQueue))]
    [InlineData(typeof(Windows.Foundation.Point))]
    public void Core_assembly_under_test_is_the_extracted_one(Type coreType)
    {
        //Arrange
        var assembly = coreType.Assembly;
        var extracted = Path.Combine(IntakeDir(), "lib", Path.GetFileName(assembly.Location));

        //Act
        var same = File.Exists(extracted) && Sha256(extracted) == Sha256(assembly.Location);

        //Assert
        assembly.GetName().Name.Should().EndWith(".Core");
        same.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(UIElement))]
    [InlineData(typeof(Visual))]
    [InlineData(typeof(DispatcherQueue))]
    [InlineData(typeof(Windows.Foundation.Point))]
    public void Core_assembly_references_no_SkiaSharp(Type coreType)
    {
        //Act
        var references = coreType.Assembly.GetReferencedAssemblies().Select(r => r.Name).ToList();

        //Assert
        references.Should().NotContain(r => r.StartsWith("SkiaSharp", StringComparison.Ordinal) || r.StartsWith("HarfBuzzSharp", StringComparison.Ordinal));
        references.Should().NotContain("CodeBrix.Platform.UI");
    }

    private static string IntakeDir() => Path.Combine(RepoRoot.Value, "artifacts", "intake", PinnedVersion.Value);

    private static string ReadIntakeFile(string name) => File.ReadAllText(Path.Combine(IntakeDir(), name));

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ReadPinnedVersion()
    {
        var pin = File.ReadAllText(Path.Combine(RepoRoot.Value, "build", "PlatformPin.props"));
        return Regex.Match(pin, "<CodeBrixPlatformVersion>([^<]+)</CodeBrixPlatformVersion>").Groups[1].Value;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(IntakeAcceptanceTests).Assembly.Location));
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeBrix.Android.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The CodeBrix.Android repository root was not found above the test assembly.");
    }
}
