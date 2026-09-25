using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.WinUI.Graphics3DGL";
    private const string CoreName = "CodeBrix.Platform.WinUI.Graphics3DGL.Core";

    [Fact]
    public void The_Core_loads_the_Android_assembly_by_name()
    {
        //Arrange
        //Act
        var strings = CoreMetadata.UserStrings(CoreName);

        //Assert
        strings.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_Core_UI_Core_and_Composition_Core_grant_their_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var core = CoreMetadata.InternalsVisibleTo(CoreName);
        var ui = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");
        var composition = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Composition.Core");

        //Assert
        core.Should().Contain(AndroidName);
        ui.Should().Contain(AndroidName);
        composition.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_Core_carries_the_element_and_the_platform_contract_the_Android_assembly_implements()
    {
        //Arrange
        //Act
        var core = CoreMetadata.TypeNames(CoreName);
        var ui = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Core");

        //Assert
        core.Should().Contain("CodeBrix.Platform.WinUI.Graphics3DGL.GLCanvasElement");
        core.Should().Contain("CodeBrix.Platform.WinUI.Graphics3DGL.Contracts.IGLCanvasPlatform");
        ui.Should().Contain("CodeBrix.Platform.Graphics.INativeOpenGLWrapper");
    }

    [Fact]
    public void The_Core_leaves_the_flavor_public_types_to_the_platform_assembly()
    {
        //Arrange
        //Act
        var core = CoreMetadata.TypeNames(CoreName);

        //Assert
        core.Should().NotContain("CodeBrix.Platform.WinUI.Graphics3DGL.SkiaGLCanvasElement");
        core.Should().NotContain("CodeBrix.Platform.WinUI.Graphics3DGL.OffscreenGLContext");
        core.Should().NotContain("CodeBrix.Platform.WinUI.Graphics3DGL.SkiaGpuContext");
    }

    [Fact]
    public void The_Core_references_the_managed_OpenGL_binding()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(CoreName);

        //Assert
        references.Should().Contain("CodeBrix.Platform.OpenGL");
    }
}
