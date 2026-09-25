using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Gates;

public class ExpectedFileSetGateTests
{
    [Fact]
    public void Run_passes_when_the_file_set_matches()
    {
        //Arrange
        using var intake = new FakeIntake().AddFile("buildTransitive/a.targets").WithGateList("expected-files.txt", "buildTransitive/a.targets");

        //Act
        var result = new ExpectedFileSetGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_reports_missing_and_unexpected_files()
    {
        //Arrange
        using var intake = new FakeIntake().AddFile("lib/extra.xml").WithGateList("expected-files.txt", "lib/expected.xml");

        //Act
        var result = new ExpectedFileSetGate().Run(intake.Load());

        //Assert
        result.Errors.Should().Contain("missing: lib/expected.xml");
        result.Errors.Should().Contain("unexpected: lib/extra.xml");
    }

    [Fact]
    public void Run_fails_without_an_expected_list()
    {
        //Arrange
        using var intake = new FakeIntake().AddFile("lib/a.xml");

        //Act
        var result = new ExpectedFileSetGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("FAIL");
    }
}
