using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Svg.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.Svg";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Svg.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Core_draws_through_the_canvas_add_in_the_Android_assembly_registers_first()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References("CodeBrix.Platform.UI.Svg.Core");

        //Assert
        references.Should().Contain("CodeBrix.Platform.SkiaSharp.Views.Core");
        references.Should().Contain("CodeBrix.SkiaSvg");
    }

    [Fact]
    public void The_Core_ships_the_provider_the_Android_assembly_registers()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Svg.Core");

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.Svg.SvgProvider");
        types.Should().Contain("CodeBrix.Platform.UI.Svg.SvgCanvas");
    }
}
