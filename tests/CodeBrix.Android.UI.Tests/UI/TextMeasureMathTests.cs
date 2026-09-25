using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class TextMeasureMathTests
{
    [Fact]
    public void AvailableWidthToPx_floors_finite_widths()
    {
        //Act
        var px = TextMeasureMath.AvailableWidthToPx(100.7, 2.625);

        //Assert
        px.Should().Be(264);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    [InlineData(1e12)]
    public void AvailableWidthToPx_treats_unbounded_widths_as_unconstrained(double available)
    {
        //Act
        var px = TextMeasureMath.AvailableWidthToPx(available, 2.0);

        //Assert
        px.Should().Be(TextMeasureMath.UnconstrainedPx);
    }

    [Fact]
    public void AvailableWidthToPx_never_returns_a_negative_width()
    {
        //Act
        var px = TextMeasureMath.AvailableWidthToPx(-5, 2.0);

        //Assert
        px.Should().Be(0);
    }

    [Fact]
    public void PxToDip_and_DipToPx_are_inverse()
    {
        //Act
        var dip = TextMeasureMath.PxToDip(TextMeasureMath.DipToPx(14, 2.75), 2.75);

        //Assert
        dip.Should().BeApproximately(14, 1e-4);
    }

    [Fact]
    public void CharacterSpacingToEm_converts_thousandths_of_an_em()
    {
        //Act
        var em = TextMeasureMath.CharacterSpacingToEm(250);

        //Assert
        em.Should().Be(0.25f);
    }
}
