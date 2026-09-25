using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class FontFamilySourceTests
{
    [Fact]
    public void Parse_splits_a_fallback_list_into_asset_and_name_entries()
    {
        //Act
        var entries = FontFamilySource.Parse("ms-appx:///Fonts/My.ttf#My Font, Segoe UI");

        //Assert
        entries.Count.Should().Be(2);
        entries[0].IsAsset.Should().BeTrue();
        entries[0].AssetPath.Should().Be("Fonts/My.ttf");
        entries[0].FamilyName.Should().Be("My Font");
        entries[1].IsAsset.Should().BeFalse();
        entries[1].FamilyName.Should().Be("Segoe UI");
    }

    [Fact]
    public void Parse_returns_no_entries_for_an_empty_source()
    {
        //Act
        var entries = FontFamilySource.Parse("  ");

        //Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public void GetManifestPath_appends_the_manifest_extension()
    {
        //Act
        var path = FontFamilySource.GetManifestPath("CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf");

        //Assert
        path.Should().Be("CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf.manifest");
    }
}
