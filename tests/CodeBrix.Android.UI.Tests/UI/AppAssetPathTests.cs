using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class AppAssetPathTests
{
    [Theory]
    [InlineData("ms-appx:///Assets/Logo.png", "Assets/Logo.png")]
    [InlineData("MS-APPX:///CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf", "CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf")]
    [InlineData("ms-appx:///Fonts/My.ttf#My Font", "Fonts/My.ttf")]
    [InlineData("/Assets/Fonts/x.ttf", "Assets/Fonts/x.ttf")]
    [InlineData("Assets\\Fonts\\x.ttf", "Assets/Fonts/x.ttf")]
    [InlineData("ms-appx:///Assets/My%20Image.png", "Assets/My Image.png")]
    public void TryGetAssetPath_maps_app_uris_and_paths_to_the_asset_path(string value, string expected)
    {
        //Act
        var ok = AppAssetPath.TryGetAssetPath(value, out var assetPath);

        //Assert
        ok.Should().BeTrue();
        assetPath.Should().Be(expected);
    }

    [Theory]
    [InlineData("Segoe UI")]
    [InlineData("https://example.com/a.png")]
    [InlineData("ms-appx:///")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetAssetPath_rejects_names_other_schemes_and_empty_values(string value)
    {
        //Act
        var ok = AppAssetPath.TryGetAssetPath(value, out var assetPath);

        //Assert
        ok.Should().BeFalse();
        assetPath.Should().BeNull();
    }
}
