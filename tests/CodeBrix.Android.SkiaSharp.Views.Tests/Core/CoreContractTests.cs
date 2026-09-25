using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.SkiaSharp.Views.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.SkiaSharp.Views";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.SkiaSharp.Views.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void UI_Core_grants_the_canvas_host_seam_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");

        //Assert
        grants.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_Core_loads_the_Android_assembly_by_name()
    {
        //Arrange
        //Act
        var strings = CoreMetadata.UserStrings("CodeBrix.Platform.SkiaSharp.Views.Core");

        //Assert
        strings.Should().Contain(AndroidName);
    }
}
