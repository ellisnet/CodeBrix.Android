using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Graphics2DSK.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.Graphics2DSK";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.WinUI.Graphics2DSK.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Core_is_the_whole_add_in_and_needs_only_the_framework_and_Skia()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References("CodeBrix.Platform.WinUI.Graphics2DSK.Core");

        //Assert
        references.Should().Contain("SkiaSharp");
        references.Should().NotContain("CodeBrix.Platform.SkiaSharp.Views.Core");
    }

    [Fact]
    public void The_Core_paints_through_the_canvas_host_factory_the_canvas_add_in_registers()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.WinUI.Graphics2DSK.Core");

        //Assert
        types.Should().Contain("CodeBrix.Platform.WinUI.Graphics2DSK.SKCanvasElement");
    }
}
