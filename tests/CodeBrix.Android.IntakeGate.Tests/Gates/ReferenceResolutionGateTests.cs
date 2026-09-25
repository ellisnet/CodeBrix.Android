using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Gates;

public class ReferenceResolutionGateTests
{
    [Fact]
    public void Run_accepts_intake_BCL_and_allowed_references()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.Foundation.Core", FakeIntake.FrameworkPackage)
            .AddAssembly("CodeBrix.Platform.Core", FakeIntake.FrameworkPackage, new[] { "System.Runtime", "CodeBrix.Platform.Foundation.Core", "Microsoft.Extensions.Logging" })
            .WithGateList("allowed-references.txt", "Microsoft.Extensions.Logging   Microsoft.Extensions.Logging");

        //Act
        var result = new ReferenceResolutionGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_for_an_unknown_reference()
    {
        //Arrange
        using var intake = new FakeIntake().AddAssembly("CodeBrix.Platform.Core", FakeIntake.FrameworkPackage, new[] { "Some.Unknown.Library" });

        //Act
        var result = new ReferenceResolutionGate().Run(intake.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("Some.Unknown.Library");
    }
}
