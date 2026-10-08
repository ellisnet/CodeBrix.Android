using CodeBrix.Android.IntakeGate.Packages;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Packages;

public class NoSkiaTwinGateTests
{
    [Fact]
    public void Run_passes_for_Cores_the_XAML_parser_and_Android_assemblies_in_the_Android_folder()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", entries: new[]
            {
                FakePackages.LibAssembly("CodeBrix.Platform.UI.Core"),
                FakePackages.LibAssembly("CodeBrix.Platform.Xaml"),
                FakePackages.LibAssembly("CodeBrix.Android.UI", "CodeBrix.Platform.UI.Core", "CodeBrix.Platform.Xaml"),
                FakePackages.AssemblyAt("analyzers/dotnet/cs/CodeBrix.Platform.UI.SourceGenerators.dll", "CodeBrix.Platform.UI.SourceGenerators"),
                FakePackages.File("buildTransitive/CodeBrix.Android.ApacheLicenseForever.targets"),
            });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_when_a_package_carries_the_twin_of_a_Core()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", entries: new[] { FakePackages.LibAssembly("CodeBrix.Platform.UI.Core") })
            .AddPackage("CodeBrix.Android.Svg.ApacheLicenseForever", entries: new[] { FakePackages.AssemblyAt("runtimes/any/CodeBrix.Platform.UI.dll", "CodeBrix.Platform.UI") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("Skia twin of CodeBrix.Platform.UI.Core");
    }

    [Fact]
    public void Run_fails_on_a_non_Core_Platform_assembly_under_lib()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.Svg.ApacheLicenseForever", entries: new[] { FakePackages.LibAssembly("CodeBrix.Platform.UI.Svg") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().Contain(e => e.Contains("not a Core assembly"));
    }

    [Fact]
    public void Run_allows_a_listed_external_Platform_named_assembly()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.Graphics3DGL.ApacheLicenseForever", entries: new[]
            {
                FakePackages.LibAssembly("CodeBrix.Android.WinUI.Graphics3DGL", "CodeBrix.Platform.OpenGL", "CodeBrix.Platform.WinUI.Graphics3DGL.Core"),
            });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_when_an_Android_assembly_references_a_twin()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", entries: new[] { FakePackages.LibAssembly("CodeBrix.Android.UI", "CodeBrix.Platform.UI") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("references the non-Core assembly CodeBrix.Platform.UI");
    }

    [Fact]
    public void Run_fails_on_the_runtime_replace_folder()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", entries: new[] { FakePackages.File("codebrix-platform-runtime/net10.0/skia/readme.txt") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("runtime-replace");
    }

    [Fact]
    public void Run_fails_on_a_lib_file_outside_the_Android_folder()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", entries: new[] { FakePackages.File("lib/net10.0/CodeBrix.Android.UI.xml") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("is not in lib/net10.0-android36.1/");
    }

    [Fact]
    public void Run_fails_when_one_assembly_ships_in_two_packages()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.SkiaSharp.Views.ApacheLicenseForever", entries: new[] { FakePackages.LibAssembly("CodeBrix.Platform.SkiaSharp.Views.Core") })
            .AddPackage("CodeBrix.Android.Svg.ApacheLicenseForever", entries: new[] { FakePackages.LibAssembly("CodeBrix.Platform.SkiaSharp.Views.Core") });

        //Act
        var result = new NoSkiaTwinGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("ships in both");
    }
}
