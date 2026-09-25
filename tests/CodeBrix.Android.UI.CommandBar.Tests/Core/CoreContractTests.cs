using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.CommandBar.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.CommandBar";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.CommandBar.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Core_loads_the_Android_assembly_by_name_for_its_icon_contract()
    {
        //Arrange
        //Act
        var strings = CoreMetadata.UserStrings("CodeBrix.Platform.UI.CommandBar.Core");
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.UI.CommandBar.Core");

        //Assert
        strings.Should().Contain(AndroidName);
        types.Should().Contain("CodeBrix.Platform.UI.CommandBar.Contracts.IIconRasterizationPlatform");
    }
}
