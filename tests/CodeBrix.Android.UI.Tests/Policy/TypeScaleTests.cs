using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class TypeScaleTests
{
    public TypeScaleTests() => TypeScale.Reset();

    [Fact]
    public void TextSizePx_is_in_sp_when_IsTextScaleFactorEnabled() =>
        TypeScale.TextSizePx(20, 2.625, 1.3, isTextScaleFactorEnabled: true).Should().BeApproximately(68.25f, 0.001f);

    [Fact]
    public void TextSizePx_is_in_dp_when_IsTextScaleFactorEnabled_is_off() =>
        TypeScale.TextSizePx(20, 2.625, 1.3, isTextScaleFactorEnabled: false).Should().BeApproximately(52.5f, 0.001f);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void TextSizePx_ignores_a_font_scale_that_is_not_positive(double scale) =>
        TypeScale.TextSizePx(14, 1, scale, true).Should().Be(14f);

    [Theory]
    [InlineData("CaptionTextBlockStyle", "labelSmall", 11)]
    [InlineData("BodyTextBlockStyle", "bodyMedium", 14)]
    [InlineData("BodyStrongTextBlockStyle", "titleSmall", 14)]
    [InlineData("SubtitleTextBlockStyle", "titleMedium", 16)]
    [InlineData("TitleTextBlockStyle", "headlineSmall", 24)]
    [InlineData("TitleLargeTextBlockStyle", "headlineMedium", 28)]
    [InlineData("DisplayTextBlockStyle", "displaySmall", 36)]
    public void RoleOf_maps_the_framework_styles_onto_Material_roles(string key, string role, double size)
    {
        //Act
        var mapped = TypeScale.RoleOf(key);

        //Assert
        mapped.Role.Should().Be(role);
        mapped.Size.Should().Be(size);
    }

    [Fact]
    public void RoleOf_is_null_for_another_style() => TypeScale.RoleOf("PrimaryButtonStyle").Should().BeNull();

    [Fact]
    public void TrackingEm_is_the_letter_spacing_in_em() =>
        TypeScale.RoleOf("CaptionTextBlockStyle").TrackingEm.Should().BeApproximately(0.5 / 11, 1e-9);

    [Fact]
    public void EffectiveFontScale_prefers_the_override()
    {
        //Arrange
        TypeScale.FontScaleOverride = 1.5;

        //Act
        var scale = TypeScale.EffectiveFontScale(1.15);

        //Assert
        scale.Should().Be(1.5);
        TypeScale.Reset();
        TypeScale.EffectiveFontScale(1.15).Should().Be(1.15);
    }
}
