using System;
using System.IO;
using System.Linq;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Repository;

/// <summary>
/// The repository's THIRD-PARTY-NOTICES.txt lists every source file that carries a provenance header (a file
/// derived from, or following the technique of, .NET MAUI or the upstream project of CodeBrix.Platform), so a
/// package never ships derived code its notices do not name. Reads the repository's own files only.
/// </summary>
public class ThirdPartyNoticesTests
{
    private static readonly string[] ProvenanceMarkers =
    {
        "// Derived from .NET MAUI",
        "// Technique from .NET MAUI",
        "// Derived from the upstream open-source XAML platform of CodeBrix.Platform",
    };

    [Fact]
    public void Every_file_with_a_provenance_header_is_listed_in_the_notices()
    {
        //Arrange
        var root = FindRepositoryRoot();
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));
        var derived = new[] { "src", "tools", "build" }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Where(path => File.ReadLines(path).Take(8).Any(line => ProvenanceMarkers.Any(m => line.StartsWith(m, StringComparison.Ordinal))))
            .ToList();

        //Act
        var unlisted = derived.Where(path => !notices.Contains(Path.GetFileName(path), StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        //Assert
        derived.Should().NotBeEmpty();
        unlisted.Should().BeEmpty();
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) || normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("CodeBrix.Android.slnx not found above " + AppContext.BaseDirectory);
    }
}
