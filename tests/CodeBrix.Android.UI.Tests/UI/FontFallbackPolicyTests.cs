using System.Collections.Generic;
using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class FontFallbackPolicyTests
{
    private const string Roboto = "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf";
    private const string RobotoAsset = "CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf";

    [Fact]
    public void A_font_file_family_loads_its_own_file()
    {
        //Arrange
        var tried = new List<string>();

        //Act
        var result = FontFallbackPolicy.Resolve("ms-appx:///Fonts/My.ttf#My Font", Roboto, path => { tried.Add(path); return true; });

        //Assert
        result.Should().Be(new FontResolution(FontResolutionSource.FamilyAsset, "Fonts/My.ttf"));
        tried.Should().Equal("Fonts/My.ttf");
    }

    [Fact]
    public void A_family_name_resolves_to_the_app_default_font_file()
    {
        //Act
        var result = FontFallbackPolicy.Resolve("Segoe UI", Roboto, _ => true);

        //Assert
        result.Should().Be(new FontResolution(FontResolutionSource.DefaultAsset, RobotoAsset));
    }

    [Fact]
    public void An_empty_family_resolves_to_the_app_default_font_file()
    {
        //Act
        var result = FontFallbackPolicy.Resolve(null, Roboto, _ => true);

        //Assert
        result.Source.Should().Be(FontResolutionSource.DefaultAsset);
    }

    [Fact]
    public void A_font_file_that_does_not_load_falls_back_to_the_app_default()
    {
        //Arrange
        var tried = new List<string>();

        //Act
        var result = FontFallbackPolicy.Resolve("ms-appx:///Fonts/Missing.ttf, Segoe UI", Roboto, path => { tried.Add(path); return path == RobotoAsset; });

        //Assert
        result.Should().Be(new FontResolution(FontResolutionSource.DefaultAsset, RobotoAsset));
        tried.Should().Equal("Fonts/Missing.ttf", RobotoAsset);
    }

    [Fact]
    public void Without_a_default_font_file_a_name_gets_the_Android_default()
    {
        //Act
        var result = FontFallbackPolicy.Resolve("Segoe UI", "Segoe UI", _ => true);

        //Assert
        result.Should().Be(new FontResolution(FontResolutionSource.AndroidDefault, null));
    }

    [Fact]
    public void The_default_file_is_not_tried_twice_when_it_is_the_family()
    {
        //Arrange
        var tried = new List<string>();

        //Act
        var result = FontFallbackPolicy.Resolve(Roboto, Roboto, path => { tried.Add(path); return false; });

        //Assert
        result.Source.Should().Be(FontResolutionSource.AndroidDefault);
        tried.Should().Equal(RobotoAsset);
    }
}
