using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP10-B: the rules of the native InfoBadge, PersonPicture and RatingControl.</summary>
public class StatusMathTests
{
    [Theory]
    [InlineData(-1, false, "Dot")]
    [InlineData(-1, true, "Icon")]
    [InlineData(0, true, "Value")]
    [InlineData(7, false, "Value")]
    public void BadgeKind_prefers_a_value_then_an_icon_then_a_dot(int value, bool hasIcon, string expected)
    {
        //Act
        var kind = StatusMath.BadgeKind(value, hasIcon);

        //Assert
        kind.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(5, "5")]
    [InlineData(999, "999")]
    [InlineData(1000, "999+")]
    [InlineData(-3, "0")]
    public void BadgeText_caps_large_numbers_the_Material_way(int value, string expected)
    {
        //Act
        var text = StatusMath.BadgeText(value);

        //Assert
        text.Should().Be(expected);
    }

    [Fact]
    public void BadgeSize_is_a_six_dip_dot_or_a_sixteen_dip_badge_that_widens_for_long_numbers()
    {
        //Act
        var dot = StatusMath.BadgeSize(InfoBadgeKind.Dot, 0);
        var icon = StatusMath.BadgeSize(InfoBadgeKind.Icon, 0);
        var one = StatusMath.BadgeSize(InfoBadgeKind.Value, 6.2);
        var many = StatusMath.BadgeSize(InfoBadgeKind.Value, 20.4);

        //Assert
        dot.Should().Be(new Size(6, 6));
        icon.Should().Be(new Size(16, 16));
        one.Should().Be(new Size(16, 16));
        many.Should().Be(new Size(29, 16));
    }

    [Theory]
    [InlineData(null, "Ada Lovelace", "AL")]
    [InlineData(null, "ada", "A")]
    [InlineData(null, "  Grace   Brewster  Hopper ", "GH")]
    [InlineData("je", "Ada Lovelace", "JE")]
    [InlineData(null, "(Ada) Lovelace", "AL")]
    [InlineData(null, "", "")]
    [InlineData(null, null, "")]
    public void Initials_follow_the_WinUI_rule(string initials, string displayName, string expected)
    {
        //Act
        var shown = StatusMath.Initials(initials, displayName);

        //Assert
        shown.Should().Be(expected);
    }

    [Fact]
    public void InitialsFontSize_is_forty_two_percent_of_the_diameter()
    {
        //Act
        var size = StatusMath.InitialsFontSize(100);

        //Assert
        size.Should().Be(42);
    }

    [Theory]
    [InlineData("new", 3, "new")]
    [InlineData(null, 3, "3")]
    [InlineData(null, 120, "99+")]
    [InlineData(null, 0, null)]
    public void PictureBadgeText_prefers_the_text_then_the_number(string badgeText, int number, string expected)
    {
        //Act
        var text = StatusMath.PictureBadgeText(badgeText, number);

        //Assert
        text.Should().Be(expected);
    }

    [Theory]
    [InlineData(3, -1, 5, 3f)]
    [InlineData(-1, 2.5, 5, 2.5f)]
    [InlineData(-1, -1, 5, 0f)]
    [InlineData(9, -1, 5, 5f)]
    public void ShownRating_shows_the_value_or_the_placeholder_within_the_stars(double value, double placeholder, int max, float expected)
    {
        //Act
        var rating = StatusMath.ShownRating(value, placeholder, max);

        //Assert
        rating.Should().Be(expected);
    }

    [Theory]
    [InlineData(3.4f, -1, true, 4)]
    [InlineData(0.2f, -1, false, 1)]
    [InlineData(3f, 3, true, -1)]
    [InlineData(3f, 3, false, 3)]
    public void ValueFromRating_takes_whole_stars_and_clears_on_the_same_value_when_allowed(float rating, double current, bool clear, double expected)
    {
        //Act
        var value = StatusMath.ValueFromRating(rating, current, clear);

        //Assert
        value.Should().Be(expected);
    }
}
