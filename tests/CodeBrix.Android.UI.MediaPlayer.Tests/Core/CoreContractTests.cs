using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.MediaPlayer.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.MediaPlayer";

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
    public void The_framework_carries_the_engine_and_presenter_contracts_the_Android_assembly_implements()
    {
        //Arrange
        //Act
        var ui = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Core");
        var platform = CoreMetadata.TypeNames("CodeBrix.Platform.Core");

        //Assert
        platform.Should().Contain("CodeBrix.Platform.Media.Playback.IMediaPlayerExtension");
        ui.Should().Contain("Microsoft.UI.Xaml.Controls.IMediaPlayerPresenterExtension");
    }
}
