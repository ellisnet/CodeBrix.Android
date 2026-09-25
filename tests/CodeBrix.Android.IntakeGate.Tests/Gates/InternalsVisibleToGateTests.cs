using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Gates;

public class InternalsVisibleToGateTests
{
    [Fact]
    public void Run_passes_when_the_Core_grants_the_listed_name()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, grants: new[] { "CodeBrix.Android.UI, PublicKey=00" })
            .WithGateList("ivt-grants.txt", "CodeBrix.Android.UI <- CodeBrix.Platform.UI.Core")
            .WithSourceProject("CodeBrix.Android.UI");

        //Act
        var result = new InternalsVisibleToGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_when_the_grant_is_missing()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, grants: new[] { "CodeBrix.Mobile.UI" })
            .WithGateList("ivt-grants.txt", "CodeBrix.Android.UI <- CodeBrix.Platform.UI.Core");

        //Act
        var result = new InternalsVisibleToGate().Run(intake.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("does not grant");
    }

    [Fact]
    public void Run_fails_for_a_source_project_without_a_row()
    {
        //Arrange
        using var intake = new FakeIntake()
            .WithGateList("ivt-grants.txt", "CodeBrix.Android.Analyzers <- (none)")
            .WithSourceProject("CodeBrix.Android.UI.Maps");

        //Act
        var result = new InternalsVisibleToGate().Run(intake.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("CodeBrix.Android.UI.Maps");
    }
}
