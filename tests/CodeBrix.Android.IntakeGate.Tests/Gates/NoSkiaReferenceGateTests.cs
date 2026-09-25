using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.IntakeGate.Tests.Gates;

public class NoSkiaReferenceGateTests
{
    [Fact]
    public void Run_fails_when_a_framework_Core_references_SkiaSharp()
    {
        //Arrange
        using var intake = new FakeIntake().AddAssembly("CodeBrix.Platform.UI.Core", FakeIntake.FrameworkPackage, new[] { "SkiaSharp" });

        //Act
        var result = new NoSkiaReferenceGate().Run(intake.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("SkiaSharp");
    }

    [Fact]
    public void Run_allows_HarfBuzzSharp_in_a_listed_Skia_canvas_addin_Core()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.UI.TextLayout.Core", "CodeBrix.Platform.TextLayout.ApacheLicenseForever", new[] { "HarfBuzzSharp" })
            .WithGateList("skia-canvas-addins.txt", "CodeBrix.Platform.TextLayout.ApacheLicenseForever");

        //Act
        var result = new NoSkiaReferenceGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }

    [Fact]
    public void Run_fails_when_an_unlisted_addin_Core_references_SkiaSharp()
    {
        //Arrange
        using var intake = new FakeIntake().AddAssembly("CodeBrix.Platform.UI.FlexPanel.Core", "CodeBrix.Platform.FlexPanel.ApacheLicenseForever", new[] { "SkiaSharp" });

        //Act
        var result = new NoSkiaReferenceGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("FAIL");
    }

    [Fact]
    public void Run_fails_when_a_Core_references_a_Skia_twin()
    {
        //Arrange
        using var intake = new FakeIntake().AddAssembly("CodeBrix.Platform.UI.Toolkit.Core", FakeIntake.FrameworkPackage, new[] { "CodeBrix.Platform.UI", "CodeBrix.Platform.Xaml" });

        //Act
        var result = new NoSkiaReferenceGate().Run(intake.Load());

        //Assert
        result.Errors.Should().ContainSingle().Which.Should().Contain("non-Core assembly CodeBrix.Platform.UI");
    }

    [Fact]
    public void Run_allows_a_CodeBrix_Platform_assembly_from_an_external_package_listed_in_allowed_references()
    {
        //Arrange
        using var intake = new FakeIntake()
            .AddAssembly("CodeBrix.Platform.WinUI.Graphics3DGL.Core", "CodeBrix.Platform.Graphics3DGL.ApacheLicenseForever", new[] { "CodeBrix.Platform.OpenGL" })
            .WithGateList("allowed-references.txt", "CodeBrix.Platform.OpenGL   CodeBrix.Platform.OpenGL.MitLicenseForever");

        //Act
        var result = new NoSkiaReferenceGate().Run(intake.Load());

        //Assert
        result.Status.Should().Be("PASS");
    }
}
