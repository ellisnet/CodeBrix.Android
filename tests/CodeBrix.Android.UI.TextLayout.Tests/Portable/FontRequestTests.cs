using CodeBrix.Android.UI.TextLayout.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.TextLayout.Tests.Portable;

public class FontRequestTests
{
    [Fact]
    public void A_zero_weight_and_stretch_are_normal()
    {
        //Act
        var request = FontRequest.Create("Segoe UI", "ms-appx:///Fonts/Roboto.ttf", 0, 0, 0);

        //Assert
        request.Weight.Should().Be(400);
        request.Stretch.Should().Be(5);
        request.Italic.Should().BeFalse();
    }

    [Fact]
    public void Oblique_and_italic_styles_both_ask_for_an_italic_face()
    {
        //Act
        var oblique = FontRequest.Create("A", "B", 700, 5, 1);
        var italic = FontRequest.Create("A", "B", 700, 5, 2);

        //Assert
        oblique.Italic.Should().BeTrue();
        italic.Italic.Should().BeTrue();
        oblique.Should().Be(italic);
    }

    [Fact]
    public void Out_of_range_weight_and_stretch_are_clamped()
    {
        //Act
        var request = FontRequest.Create("A", "B", 1500, 12, 0);

        //Assert
        request.Weight.Should().Be(1000);
        request.Stretch.Should().Be(9);
    }

    [Fact]
    public void A_different_default_family_is_a_different_request()
    {
        //Act
        var first = FontRequest.Create("Segoe UI", "ms-appx:///Fonts/A.ttf", 400, 5, 0);
        var second = FontRequest.Create("Segoe UI", "ms-appx:///Fonts/B.ttf", 400, 5, 0);

        //Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void Surrounding_blanks_do_not_make_a_different_request()
    {
        //Act
        var first = FontRequest.Create(" Segoe UI ", "B", 400, 5, 0);
        var second = FontRequest.Create("Segoe UI", "B", 400, 5, 0);

        //Assert
        first.Should().Be(second);
    }
}
