using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.FlexPanel.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.FlexPanel";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.FlexPanel.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Core_is_the_whole_add_in_with_no_platform_contract()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.UI.FlexPanel.Core");

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.FlexPanel.FlexPanel");
        types.Should().NotContain(t => t.Contains(".Contracts."));
    }
}
