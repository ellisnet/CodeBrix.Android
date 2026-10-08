using CodeBrix.Android.IntakeGate.Packages;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Packages;

public class PlatformDependencyGateTests
{
    [Fact]
    public void Run_passes_when_no_dependency_is_a_Platform_repository_package()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", new[] { "Xamarin.Google.Android.Material", "CodeBrix.Platform.Fonts.Fluent.ApacheLicenseForever" });

        //Act
        var result = new PlatformDependencyGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_on_a_dependency_the_Platform_repository_produces_ignoring_case()
    {
        //Arrange
        using var packages = new FakePackages()
            .WithPlatformIds("CodeBrix.Platform.ApacheLicenseForever", "CodeBrix.Platform.Svg.ApacheLicenseForever")
            .AddPackage("CodeBrix.Android.Svg.ApacheLicenseForever", new[] { "codebrix.platform.svg.apachelicenseforever" });

        //Act
        var result = new PlatformDependencyGate().Run(packages.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("CodeBrix.Android.Svg.ApacheLicenseForever depends on codebrix.platform.svg");
    }

    [Fact]
    public void Run_fails_without_the_Platform_id_list()
    {
        //Arrange
        using var packages = new FakePackages().WithPlatformIds().AddPackage("CodeBrix.Android.ApacheLicenseForever");

        //Act
        var result = new PlatformDependencyGate().Run(packages.Load());

        //Assert
        result.Status.Should().Be("FAIL");
    }
}
