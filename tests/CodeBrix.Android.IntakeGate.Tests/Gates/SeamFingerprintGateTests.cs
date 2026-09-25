using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Gates;

public class SeamFingerprintGateTests
{
    [Fact]
    public void Run_is_skipped_while_the_gate_is_off()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage)
            .WithGateList("seam-fingerprint.txt", "IElementHandlerFactoryPlatform");

        //Act
        var result = new SeamFingerprintGate().Run(intake.Load(seamGate: false));

        //Assert
        result.Status.Should().Be("SKIPPED");
    }

    [Fact]
    public void Run_fails_when_on_and_a_type_is_missing()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IElementHandler" })
            .WithGateList("seam-fingerprint.txt", "IElementHandler", "IElementHandlerFactoryPlatform");

        //Act
        var result = new SeamFingerprintGate().Run(intake.Load(seamGate: true));

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("IElementHandlerFactoryPlatform");
    }

    [Fact]
    public void Run_passes_when_on_and_every_entry_is_present()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IElementHandler", "IElementHandlerFactoryPlatform" })
            .WithGateList("seam-fingerprint.txt", "IElementHandler", "IElementHandlerFactoryPlatform");

        //Act
        var result = new SeamFingerprintGate().Run(intake.Load(seamGate: true));

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_looks_in_the_named_assembly_when_a_line_names_one()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IElementHandler" })
            .AddAssembly("CodeBrix.Platform.Foundation.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IFontSourcePlatform" })
            .WithGateList("seam-fingerprint.txt", "IElementHandler", "[CodeBrix.Platform.Foundation.Core] IFontSourcePlatform");

        //Act
        var result = new SeamFingerprintGate().Run(intake.Load(seamGate: true));

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_when_a_named_assembly_lacks_the_type()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IFontSourcePlatform" })
            .AddAssembly("CodeBrix.Platform.Foundation.Core", FakeIntake.FrameworkPackage, typeNames: new[] { "IElementHandler" })
            .WithGateList("seam-fingerprint.txt", "[CodeBrix.Platform.Foundation.Core] IFontSourcePlatform");

        //Act
        var result = new SeamFingerprintGate().Run(intake.Load(seamGate: true));

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("[CodeBrix.Platform.Foundation.Core] IFontSourcePlatform");
    }
}
