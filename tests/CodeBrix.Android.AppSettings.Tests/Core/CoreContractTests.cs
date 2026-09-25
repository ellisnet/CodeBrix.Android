using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.AppSettings.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.AppSettings";

    [Fact]
    public void The_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.AppSettings.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Core_loads_the_Android_assembly_by_name_for_its_storage_contract()
    {
        //Arrange
        //Act
        var strings = CoreMetadata.UserStrings("CodeBrix.Platform.AppSettings.Core");

        //Assert
        strings.Should().Contain(AndroidName);
    }
}
