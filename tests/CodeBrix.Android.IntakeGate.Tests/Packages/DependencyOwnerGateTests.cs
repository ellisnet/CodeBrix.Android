using CodeBrix.Android.IntakeGate.Packages;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Packages;

public class DependencyOwnerGateTests
{
    [Fact]
    public void Run_passes_for_Microsoft_Xamarin_CodeBrix_and_SQLitePCLRaw_dependencies()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.AppSettings.ApacheLicenseForever",
                new[] { "Xamarin.AndroidX.Core", "Microsoft.Extensions.Logging", "CodeBrix.Sqlite.ApacheLicenseForever", "SQLitePCLRaw.core" });

        //Act
        var result = new DependencyOwnerGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_on_a_dependency_whose_owner_is_not_allowed()
    {
        //Arrange
        using var packages = new FakePackages().AddPackage("CodeBrix.Android.ApacheLicenseForever", new[] { "Newtonsoft.Json 13.0.3" });

        //Act
        var result = new DependencyOwnerGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("Newtonsoft.Json");
    }

    [Fact]
    public void Run_fails_without_an_owner_list()
    {
        //Arrange
        using var packages = new FakePackages().WithOwners().AddPackage("CodeBrix.Android.ApacheLicenseForever");

        //Act
        var result = new DependencyOwnerGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("FAIL");
    }

    [Theory]
    [InlineData("Xamarin.*", "xamarin.androidx.window", true)]
    [InlineData("SkiaSharp*", "SkiaSharp.NativeAssets.Android", true)]
    [InlineData("SkiaSharp*", "SkiaSharp", true)]
    [InlineData("CodeBrix.*", "CodeBrixed.Other", false)]
    [InlineData("Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Abstractions", false)]
    public void Matches_treats_a_trailing_star_as_any_rest(string pattern, string id, bool expected)
    {
        //Act
        var matches = DependencyOwnerGate.Matches(pattern, id);

        //Assert
        matches.Should().Be(expected);
    }
}
