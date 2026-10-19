using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Build;

/// <summary>
/// Package-pin fences (PIN BUMP step 6 in MAINTAINER-README): the root Directory.Packages.props pins the non-Platform-repo
/// dependencies of the re-shipped Core assemblies at the versions the pinned CodeBrix.Platform build declares, and a
/// sample-level Directory.Packages.props that imports the root never re-Includes an id the root pins (NU1506 fails the
/// paste-always heads; a sample that needs another version uses Update).
/// </summary>
public class PackagePinsTests
{
    [Fact]
    public void Root_pins_match_the_versions_the_pinned_platform_packages_declare()
    {
        //Arrange
        var rootPins = PackageVersions(Path.Combine(RepoRoot(), "Directory.Packages.props"))
            .ToDictionary(p => p.Id, p => p.Version, StringComparer.OrdinalIgnoreCase);
        var declared = PinnedPackageNuspecs()
            .SelectMany(n => XDocument.Load(n).Descendants().Where(e => e.Name.LocalName == "dependency"))
            .Select(d => (Id: d.Attribute("id").Value, Version: d.Attribute("version").Value.Trim('[', ']')))
            .Where(d => !d.Id.StartsWith("CodeBrix.Platform", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        declared.Should().NotBeEmpty("the intake downloaded the pinned Platform packages");

        //Act
        var drifted = declared
            .Where(d => rootPins.TryGetValue(d.Id, out var pinned) && pinned != d.Version)
            .Select(d => $"{d.Id}: pinned {rootPins[d.Id]}, the Platform build declares {d.Version}")
            .ToList();

        //Assert
        drifted.Should().BeEmpty("Directory.Packages.props follows the pinned Platform build (PIN BUMP step 6)");
    }

    [Fact]
    public void Sample_level_package_versions_never_re_include_an_id_the_root_pins()
    {
        //Arrange
        var root = RepoRoot();
        var rootIds = new HashSet<string>(PackageVersions(Path.Combine(root, "Directory.Packages.props")).Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
        var sampleProps = new[] { "tests/PasteAlways", "samples" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "Directory.Packages.props", SearchOption.AllDirectories))
            .Where(ImportsTheRoot)
            .ToList();
        sampleProps.Should().NotBeEmpty("the paste-always heads carry their own Directory.Packages.props");

        //Act
        var duplicates = sampleProps
            .SelectMany(f => PackageVersions(f).Where(p => rootIds.Contains(p.Id)).Select(p => $"{Path.GetRelativePath(root, f)}: {p.Id}"))
            .ToList();

        //Assert
        duplicates.Should().BeEmpty("a sample that needs another version of a root-pinned id uses <PackageVersion Update=...>");
    }

    private static bool ImportsTheRoot(string propsFile) =>
        XDocument.Load(propsFile).Descendants().Any(e => e.Name.LocalName == "Import" && (e.Attribute("Project")?.Value ?? string.Empty).Contains("Directory.Packages.props", StringComparison.Ordinal));

    private static IEnumerable<(string Id, string Version)> PackageVersions(string propsFile) =>
        XDocument.Load(propsFile).Descendants()
            .Where(e => e.Name.LocalName == "PackageVersion" && e.Attribute("Include") != null)
            .Select(e => (e.Attribute("Include").Value, e.Attribute("Version")?.Value ?? string.Empty));

    private static IEnumerable<string> PinnedPackageNuspecs()
    {
        var root = RepoRoot();
        var pin = XDocument.Load(Path.Combine(root, "build", "PlatformPin.props")).Descendants();
        var versions = new[] { "CodeBrixPlatformVersion", "CodeBrixPlatformSkiaSharpViewsVersion" }
            .Select(name => pin.First(e => e.Name.LocalName == name).Value.Trim())
            .ToList();
        var packages = Path.Combine(root, "artifacts", "intake", ".packages");
        Directory.Exists(packages).Should().BeTrue("the intake ran (the solution build runs it first)");
        return Directory.GetDirectories(packages)
            .SelectMany(id => versions.Select(v => Path.Combine(id, v)))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.nuspec"));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
