using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.TextLayout.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.TextLayout";

    [Fact]
    public void The_TextLayout_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.TextLayout.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Foundation_Core_grants_the_font_source_contract_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.Foundation.Core");
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.Foundation.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        types.Should().Contain("CodeBrix.Platform.Foundation.Contracts.IFontSourcePlatform`1");
    }

    [Fact]
    public void The_TextLayout_Core_loads_the_Android_assembly_by_name_for_its_font_source()
    {
        //Arrange
        //Act
        var names = CoreMetadata.UserStrings("CodeBrix.Platform.UI.TextLayout.Core");

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_PlotterView_Core_also_finds_the_font_source_in_the_Android_TextLayout_assembly()
    {
        //Arrange
        //Act
        var names = CoreMetadata.UserStrings("CodeBrix.Platform.UI.PlotterView.Core");

        //Assert
        names.Should().Contain(AndroidName);
    }
}
