using System.IO;
using System.Linq;
using CodeBrix.Android.IntakeGate.Packages;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Packages;

public class PackageGateRunnerTests
{
    [Fact]
    public void RunGates_runs_the_three_gates_in_order()
    {
        //Arrange
        using var packages = new FakePackages().AddPackage("CodeBrix.Android.ApacheLicenseForever", new[] { "Xamarin.Google.Android.Material" });

        //Act
        var results = PackageGateRunner.RunGates(packages.Load());

        //Assert
        results.Select(r => r.Code).Should().Equal("CBAP0001", "CBAP0002", "CBAP0003");
        results.Should().OnlyContain(r => r.Status == "PASS");
    }

    [Fact]
    public void RunGates_fails_when_the_folder_holds_no_package()
    {
        //Arrange
        using var packages = new FakePackages();

        //Act
        var results = PackageGateRunner.RunGates(packages.Load());

        //Assert
        results[0].Errors.Should().Contain("no .nupkg files found");
    }

    [Fact]
    public void Run_writes_the_report_with_each_package_dependency_and_returns_zero_when_every_gate_passes()
    {
        //Arrange
        using var packages = new FakePackages()
            .AddPackage("CodeBrix.Android.ApacheLicenseForever", new[] { "Xamarin.Google.Android.Material 1.14.0.6" }, new[] { FakePackages.LibAssembly("CodeBrix.Android.UI") });
        var set = packages.Load();
        var report = Path.Combine(packages.Root, "package-gates.txt");
        var idsFile = Path.Combine(packages.Root, "platform-repo-package-ids.txt");
        var ownersFile = Path.Combine(packages.Root, "owners.txt");

        //Act
        var exitCode = PackageGateRunner.Run(new[] { "--package-dir", packages.PackageDirectory, "--platform-ids", idsFile, "--owners", ownersFile, "--report", report });

        //Assert
        exitCode.Should().Be(0);
        set.Packages.Should().ContainSingle();
        File.ReadAllText(report).Should().Contain("dependency  Xamarin.Google.Android.Material 1.14.0.6  [net10.0-android36.1]");
    }

    [Fact]
    public void Run_returns_two_on_a_missing_option()
    {
        //Act
        var exitCode = PackageGateRunner.Run(new[] { "--package-dir", "x" });

        //Assert
        exitCode.Should().Be(2);
    }
}
