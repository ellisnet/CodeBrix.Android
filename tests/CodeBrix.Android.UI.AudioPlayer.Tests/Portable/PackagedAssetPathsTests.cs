using System.IO;
using CodeBrix.Android.UI.AudioPlayer.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.AudioPlayer.Tests.Portable;

public class PackagedAssetPathsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "files", "codebrix-audio-assets");

    [Theory]
    [InlineData("Assets/silence2s.wav", "Assets/silence2s.wav")]
    [InlineData("CodeBrix.Some.Library/Sounds/click.ogg", "CodeBrix.Some.Library/Sounds/click.ogg")]
    [InlineData("a.wav", "a.wav")]
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
    [InlineData("/storage/emulated/0/Music/song.mp3")]
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

    [Fact]
    public void The_root_itself_and_a_sibling_with_the_same_prefix_name_no_asset()
    {
        //Arrange
        //Act
        var itself = PackagedAssetPaths.AssetPathOf(Root, Root);
        var sibling = PackagedAssetPaths.AssetPathOf(Root, Root + "-other/x.wav");
        var escaping = PackagedAssetPaths.AssetPathOf(Root, Path.Combine(Root, "..", "x.wav"));

        //Assert
        itself.Should().BeNull();
        sibling.Should().BeNull();
        escaping.Should().BeNull();
    }

    [Theory]
    [InlineData("Instruments/Piano/piano.sfz", "Instruments/Piano")]
    [InlineData("Instruments/Strings/strings.dspreset", "Instruments/Strings")]
    [InlineData("Instruments/gm.sf2", "Instruments/gm.sf2")]
    [InlineData("Instruments/pads.dslibrary", "Instruments/pads.dslibrary")]
    [InlineData("Instruments/Choir", "Instruments/Choir")]
    [InlineData("lonely.sfz", "lonely.sfz")]
    public void An_instrument_brings_its_folder_when_its_samples_sit_beside_it(string asset, string copy)
    {
        //Arrange
        //Act
        var result = PackagedAssetPaths.InstrumentAssetToCopy(asset);

        //Assert
        result.Should().Be(copy);
    }
}
