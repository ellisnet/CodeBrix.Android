using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.TextLayout.Tests.Core;

/// <summary>
/// AP7-B TerminalView fence (FIXLIST [AP7-B TerminalView]): the Android TextLayout add-in must bring every MANAGED package
/// assembly its Core is built on. The Core's FontDetails holds HarfBuzzSharp types; the add-in declared only HarfBuzzSharp's
/// native assets, so an app with no other HarfBuzzSharp consumer failed its first text layout with a TypeLoadException
/// (found by the TerminalViewDemo sample gate; the UIReqs app hid it: the Svg add-in's CodeBrix.SkiaSvg brought the assembly).
/// </summary>
public class PackageClosureTests
{
    [Fact]
    public void The_Android_TextLayout_project_references_every_Skia_and_HarfBuzz_package_its_Core_references()
    {
        //Arrange
        var root = typeof(PackageClosureTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "CodeBrix.Android.Tests.RepoRoot").Value;
        var project = XDocument.Load(Path.Combine(root, "src", "AddIns", "CodeBrix.Android.UI.TextLayout", "CodeBrix.Android.UI.TextLayout.csproj"));
        var packages = project.Descendants("PackageReference").Select(p => (string)p.Attribute("Include")).ToList();

        //Act
        var managed = CoreMetadata.References("CodeBrix.Platform.UI.TextLayout.Core")
            .Where(r => r.StartsWith("SkiaSharp") || r.StartsWith("HarfBuzzSharp"))
            .ToList();

        //Assert
        managed.Should().Contain("HarfBuzzSharp");
        packages.Should().Contain(managed);
    }
}
