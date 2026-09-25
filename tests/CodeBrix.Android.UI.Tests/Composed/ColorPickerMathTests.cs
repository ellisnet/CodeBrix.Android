using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;
using WColor = Windows.UI.Color;

namespace CodeBrix.Android.UI.Tests.Composed;

/// <summary>AP6: the native ColorPicker's colour math (HSV, hex, the spectrum's axes).</summary>
[Collection(HostFreeCoreCollection.Name)]
public class ColorPickerMathTests
{
    public ColorPickerMathTests() => HostFreeCore.EnsureInitialized();

    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(128, 128, 128, 0, 0, 0.502)]
    public void Rgb_converts_to_hsv(byte r, byte g, byte b, double hue, double saturation, double value)
    {
        //Act
        var hsv = ColorPickerMath.ToHsv(WColor.FromArgb(255, r, g, b));

        //Assert
        hsv.H.Should().BeApproximately(hue, 0.5);
        hsv.S.Should().BeApproximately(saturation, 0.005);
        hsv.V.Should().BeApproximately(value, 0.005);
    }

    [Fact]
    public void Hsv_round_trips_to_the_same_rgb()
    {
        //Arrange
        var color = WColor.FromArgb(200, 30, 144, 255);

        //Act
        var back = ColorPickerMath.ToColor(ColorPickerMath.ToHsv(color), color.A);

        //Assert
        back.Should().Be(color);
    }

    [Fact]
    public void Hex_text_has_alpha_only_when_alpha_is_enabled()
    {
        //Arrange
        var color = WColor.FromArgb(0x80, 0x12, 0xAB, 0xEF);

        //Assert
        ColorPickerMath.ToHex(color, alphaEnabled: true).Should().Be("#8012ABEF");
        ColorPickerMath.ToHex(color, alphaEnabled: false).Should().Be("#12ABEF");
    }

    [Theory]
    [InlineData("#FF0000", true, 255, 255, 0, 0)]
    [InlineData("80FF0000", true, 128, 255, 0, 0)]
    [InlineData("80FF0000", false, 255, 255, 0, 0)]
    [InlineData("#0F0", true, 255, 0, 255, 0)]
    public void Hex_text_parses_with_or_without_the_hash(string text, bool alpha, byte a, byte r, byte g, byte b)
    {
        //Act
        var ok = ColorPickerMath.TryParseHex(text, alpha, out var color);

        //Assert
        ok.Should().BeTrue();
        color.Should().Be(WColor.FromArgb(a, r, g, b));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("red")]
    public void Text_that_is_not_hex_is_refused(string text)
    {
        //Assert
        ColorPickerMath.TryParseHex(text, true, out _).Should().BeFalse();
    }

    [Fact]
    public void Hue_saturation_spectrum_has_hue_across_and_full_saturation_at_the_top()
    {
        //Act
        var position = ColorPickerMath.SpectrumPosition(new HsvColor(180, 1, 0.5), ColorSpectrumComponents.HueSaturation);

        //Assert
        position.X.Should().BeApproximately(0.5, 1e-9);
        position.Y.Should().BeApproximately(0, 1e-9);
        ColorPickerMath.SliderChannel(ColorSpectrumComponents.HueSaturation).Should().Be(ColorChannel.Value);
    }

    [Fact]
    public void A_spectrum_position_sets_the_two_axis_channels_and_keeps_the_third()
    {
        //Act
        var hsv = ColorPickerMath.AtSpectrumPosition(0.25, 0.75, ColorSpectrumComponents.HueSaturation, new HsvColor(0, 0, 0.8));

        //Assert
        hsv.H.Should().BeApproximately(90, 1e-9);
        hsv.S.Should().BeApproximately(0.25, 1e-9);
        hsv.V.Should().Be(0.8);
    }

    [Fact]
    public void Value_runs_from_its_maximum_at_the_top_when_paired_with_hue()
    {
        //Act
        var (x, y) = ColorPickerMath.SpectrumPosition(new HsvColor(90, 0.4, 1), ColorSpectrumComponents.HueValue);

        //Assert
        x.Should().BeApproximately(0.25, 1e-9);
        y.Should().BeApproximately(0, 1e-9);
        ColorPickerMath.SliderChannel(ColorSpectrumComponents.HueValue).Should().Be(ColorChannel.Saturation);
        ColorPickerMath.SliderChannel(ColorSpectrumComponents.SaturationValue).Should().Be(ColorChannel.Hue);
    }
}
