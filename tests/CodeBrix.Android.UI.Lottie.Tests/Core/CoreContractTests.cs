using CodeBrix.Android.Tests.Shared;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Lottie.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.Lottie";
    private const string LottieCore = "CodeBrix.Platform.UI.Lottie.Core";

    [Fact]
    public void The_Lottie_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(LottieCore);

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_UI_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");

        //Assert
        grants.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_Lottie_Core_loads_the_Android_assembly_by_name_for_its_canvas()
    {
        //Arrange
        //Act
        var names = CoreMetadata.UserStrings(LottieCore);

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_Lottie_Core_ships_the_canvas_contract_and_the_engine_the_Android_assembly_relies_on()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(LottieCore);

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.Lottie.Contracts.ILottieCanvasPlatform");
        types.Should().Contain("CodeBrix.Platform.UI.Lottie.Engine.LottiePlayer");
        types.Should().Contain("CodeBrix.Platform.UI.Lottie.Engine.ITickSource");
        types.Should().Contain("CommunityToolkit.WinUI.Lottie.LottieVisualSource");
    }

    [Fact]
    public void The_Lottie_Core_decodes_with_Skottie_on_the_Skia_canvas_host_seam()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(LottieCore);

        //Assert
        references.Should().Contain("SkiaSharp.Skottie");
        references.Should().Contain("CodeBrix.Platform.SkiaSharp.Views.Core");
    }
}
