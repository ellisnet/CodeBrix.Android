using System;
using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class FontManifestTests
{
    private const string Manifest = """
        {
          "fonts": [
            { "font_style": "Normal", "font_weight": 300, "font_stretch": "Normal", "family_name": "ms-appx:///F/Fonts/X-Light.ttf" },
            { "font_style": "Normal", "font_weight": 400, "font_stretch": "Normal", "family_name": "ms-appx:///F/Fonts/X-Regular.ttf" },
            { "font_style": "Normal", "font_weight": 700, "font_stretch": "Normal", "family_name": "ms-appx:///F/Fonts/X-Bold.ttf" },
            { "font_style": "Italic", "font_weight": 400, "font_stretch": "Normal", "family_name": "ms-appx:///F/Fonts/X-Italic.ttf" },
            { "font_style": "Normal", "font_weight": 400, "font_stretch": "Condensed", "family_name": "ms-appx:///F/Fonts/X_Condensed-Regular.ttf" }
          ]
        }
        """;

    [Fact]
    public void Parse_reads_every_face()
    {
        //Act
        var manifest = FontManifest.Parse(Manifest);

        //Assert
        manifest.Faces.Count.Should().Be(5);
        manifest.Faces[3].IsItalic.Should().BeTrue();
        manifest.Faces[4].Stretch.Should().Be(3);
    }

    [Theory]
    [InlineData(400, false, 5, "X-Regular")]
    [InlineData(700, false, 5, "X-Bold")]
    [InlineData(600, false, 5, "X-Bold")]
    [InlineData(500, false, 5, "X-Regular")]
    [InlineData(200, false, 5, "X-Light")]
    [InlineData(400, true, 5, "X-Italic")]
    [InlineData(700, true, 5, "X-Italic")]
    [InlineData(400, false, 3, "X_Condensed-Regular")]
    [InlineData(400, false, 2, "X_Condensed-Regular")]
    [InlineData(400, false, 0, "X-Regular")]
    public void Select_applies_css_font_matching(int weight, bool italic, int stretch, string expectedFile)
    {
        //Arrange
        var manifest = FontManifest.Parse(Manifest);

        //Act
        var face = manifest.Select(weight, italic, stretch);

        //Assert
        face.HasValue.Should().BeTrue();
        face.Value.Source.Should().Be("ms-appx:///F/Fonts/" + expectedFile + ".ttf");
    }

    [Fact]
    public void Select_returns_null_for_an_empty_manifest()
    {
        //Act
        var face = FontManifest.Parse("{\"fonts\":[]}").Select(400, false, 5);

        //Assert
        face.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Parse_rejects_invalid_json()
    {
        //Act
        Action act = () => FontManifest.Parse("{ not json");

        //Assert
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("UltraCondensed", 1)]
    [InlineData("SemiExpanded", 6)]
    [InlineData("Normal", 5)]
    [InlineData(null, 5)]
    public void ParseStretch_maps_names_to_font_stretch_values(string name, int expected)
    {
        //Act
        var value = FontManifest.ParseStretch(name);

        //Assert
        value.Should().Be(expected);
    }
}
