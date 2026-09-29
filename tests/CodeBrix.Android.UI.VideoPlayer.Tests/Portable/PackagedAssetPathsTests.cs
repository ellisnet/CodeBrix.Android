using System.IO;
using CodeBrix.Android.UI.VideoPlayer.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.VideoPlayer.Tests.Portable;

public class PackagedAssetPathsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "files", "codebrix-audio-assets");

    [Theory]
    [InlineData("Assets/clip.webm", "Assets/clip.webm")]
    [InlineData("Assets/Authoring/sample.cbv", "Assets/Authoring/sample.cbv")]
    public void A_path_under_the_root_names_its_asset(string below, string asset)
    {
        //Arrange
        var path = Path.Combine(Root, below);

        //Act
        var result = PackagedAssetPaths.AssetPathOf(Root, path);

        //Assert
        result.Should().Be(asset);
    }

    [Theory]
    [InlineData("/storage/emulated/0/Movies/clip.webm")]
    [InlineData("")]
    [InlineData(null)]
    public void A_path_outside_the_root_names_no_asset(string path)
    {
        //Arrange
        //Act
        var result = PackagedAssetPaths.AssetPathOf(Root, path);

        //Assert
        result.Should().BeNull();
    }
}
