using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebView.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.WebView";

    [Fact]
    public void UI_Core_and_Platform_Core_grant_their_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var ui = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");
        var platform = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.Core");

        //Assert
        ui.Should().Contain(AndroidName);
        platform.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_framework_carries_the_web_view_contract_the_Android_assembly_implements()
    {
        //Arrange
        //Act
        var ui = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Core");
        var platform = CoreMetadata.TypeNames("CodeBrix.Platform.Core");

        //Assert
        ui.Should().Contain("Microsoft.Web.WebView2.Core.INativeWebViewProvider");
        platform.Should().Contain("CodeBrix.Platform.UI.Xaml.Controls.INativeWebView");
    }
}
