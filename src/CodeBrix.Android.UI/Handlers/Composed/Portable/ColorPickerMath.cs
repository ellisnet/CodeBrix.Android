using System;
using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using WColor = Windows.UI.Color;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>A colour in HSV: hue in degrees [0, 360), saturation and value in [0, 1].</summary>
/// <param name="H">Hue in degrees.</param>
/// <param name="S">Saturation, 0-1.</param>
/// <param name="V">Value (brightness), 0-1.</param>
internal readonly record struct HsvColor(double H, double S, double V);

/// <summary>The HSV channel a ColorPicker's third slider sets (the one its spectrum's two axes leave out).</summary>
internal enum ColorChannel
{
    /// <summary>Hue.</summary>
    Hue,

    /// <summary>Saturation.</summary>
    Saturation,

    /// <summary>Value.</summary>
    Value,
}

/// <summary>
/// The native ColorPicker's colour math (host-testable): RGB and HSV, the WinUI hex text ("#AARRGGBB" with alpha,
/// "#RRGGBB" without), and the spectrum's axes for each ColorSpectrumComponents value exactly as WinUI lays them out
/// (the first-named component on X; saturation, or else value, runs from its maximum at the top).
/// </summary>
internal static class ColorPickerMath
{
    /// <summary>HSV of an RGB colour (alpha ignored).</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The HSV colour (hue 0 for greys).</returns>
    internal static HsvColor ToHsv(WColor color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var chroma = max - min;
        double hue;
        if (chroma <= 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / chroma) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / chroma) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / chroma) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return new HsvColor(hue, max <= 0 ? 0 : chroma / max, max);
    }

    /// <summary>The RGB colour of an HSV colour, with <paramref name="alpha"/>.</summary>
    /// <param name="hsv">The HSV colour.</param>
    /// <param name="alpha">The alpha byte.</param>
    /// <returns>The colour.</returns>
    internal static WColor ToColor(HsvColor hsv, byte alpha = 255)
    {
        var h = ((hsv.H % 360) + 360) % 360;
        var s = Math.Clamp(hsv.S, 0, 1);
        var v = Math.Clamp(hsv.V, 0, 1);
        var chroma = v * s;
        var x = chroma * (1 - Math.Abs(((h / 60) % 2) - 1));
        var m = v - chroma;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return WColor.FromArgb(alpha, Byte(r + m), Byte(g + m), Byte(b + m));
    }

    /// <summary>The WinUI hex text of a colour: "#AARRGGBB" when alpha is enabled, else "#RRGGBB".</summary>
    /// <param name="color">The colour.</param>
    /// <param name="alphaEnabled">ColorPicker.IsAlphaEnabled.</param>
    /// <returns>The text.</returns>
    internal static string ToHex(WColor color, bool alphaEnabled) => alphaEnabled
        ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B)
        : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);

    /// <summary>
    /// Parses hex text typed into the picker: an optional '#', then RRGGBB or AARRGGBB (RGB / ARGB short forms
    /// accepted). Without alpha enabled, the colour is opaque.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="alphaEnabled">ColorPicker.IsAlphaEnabled.</param>
    /// <param name="color">The colour parsed.</param>
    /// <returns>True when the text is a colour.</returns>
    internal static bool TryParseHex(string text, bool alphaEnabled, out WColor color)
    {
        color = default;
        var digits = (text ?? string.Empty).Trim();
        if (digits.StartsWith('#'))
        {
            digits = digits.Substring(1);
        }

        if (digits.Length is 3 or 4)
        {
            var expanded = new char[digits.Length * 2];
            for (var i = 0; i < digits.Length; i++)
            {
                expanded[i * 2] = digits[i];
                expanded[(i * 2) + 1] = digits[i];
            }

            digits = new string(expanded);
        }

        if (digits.Length is not (6 or 8) || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        var a = digits.Length == 8 ? (byte)(value >> 24) : (byte)255;
        color = WColor.FromArgb(alphaEnabled ? a : (byte)255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    /// <summary>The channel the third (colour) slider sets for a spectrum's components.</summary>
    /// <param name="components">ColorPicker.ColorSpectrumComponents.</param>
    /// <returns>The channel.</returns>
    internal static ColorChannel SliderChannel(ColorSpectrumComponents components) => components switch
    {
        ColorSpectrumComponents.HueValue or ColorSpectrumComponents.ValueHue => ColorChannel.Saturation,
        ColorSpectrumComponents.SaturationValue or ColorSpectrumComponents.ValueSaturation => ColorChannel.Hue,
        _ => ColorChannel.Value,
    };

    /// <summary>A channel's value as a fraction 0-1 (hue / 360).</summary>
    /// <param name="hsv">The colour.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The fraction.</returns>
    internal static double Fraction(HsvColor hsv, ColorChannel channel) => channel switch
    {
        ColorChannel.Hue => hsv.H / 360.0,
        ColorChannel.Saturation => hsv.S,
        _ => hsv.V,
    };

    /// <summary>The colour with one channel set from a fraction 0-1.</summary>
    /// <param name="hsv">The colour.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="fraction">The fraction (hue: of 360 degrees).</param>
    /// <returns>The new colour.</returns>
    internal static HsvColor WithFraction(HsvColor hsv, ColorChannel channel, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        return channel switch
        {
            ColorChannel.Hue => hsv with { H = Math.Min(fraction * 360.0, 359.999) },
            ColorChannel.Saturation => hsv with { S = fraction },
            _ => hsv with { V = fraction },
        };
    }

    /// <summary>Where a colour sits on the spectrum, as fractions of its width and height (0,0 = top left).</summary>
    /// <param name="hsv">The colour.</param>
    /// <param name="components">The spectrum's components.</param>
    /// <returns>(x, y) fractions.</returns>
    internal static (double X, double Y) SpectrumPosition(HsvColor hsv, ColorSpectrumComponents components)
    {
        var (xChannel, yChannel) = Axes(components);
        return (AxisFraction(hsv, xChannel, components), AxisFraction(hsv, yChannel, components));
    }

    /// <summary>The colour at a spectrum position (the slider's channel is kept from <paramref name="current"/>).</summary>
    /// <param name="x">X as a fraction of the width.</param>
    /// <param name="y">Y as a fraction of the height.</param>
    /// <param name="components">The spectrum's components.</param>
    /// <param name="current">The current colour.</param>
    /// <returns>The colour at that point.</returns>
    internal static HsvColor AtSpectrumPosition(double x, double y, ColorSpectrumComponents components, HsvColor current)
    {
        var (xChannel, yChannel) = Axes(components);
        var result = WithFraction(current, xChannel, Inverted(xChannel, components) ? 1 - Math.Clamp(x, 0, 1) : Math.Clamp(x, 0, 1));
        return WithFraction(result, yChannel, Inverted(yChannel, components) ? 1 - Math.Clamp(y, 0, 1) : Math.Clamp(y, 0, 1));
    }

    /// <summary>The spectrum's (X, Y) channels for its components.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The channel on each axis.</returns>
    internal static (ColorChannel X, ColorChannel Y) Axes(ColorSpectrumComponents components) => components switch
    {
        ColorSpectrumComponents.HueValue => (ColorChannel.Hue, ColorChannel.Value),
        ColorSpectrumComponents.ValueHue => (ColorChannel.Value, ColorChannel.Hue),
        ColorSpectrumComponents.ValueSaturation => (ColorChannel.Value, ColorChannel.Saturation),
        ColorSpectrumComponents.SaturationHue => (ColorChannel.Saturation, ColorChannel.Hue),
        ColorSpectrumComponents.SaturationValue => (ColorChannel.Saturation, ColorChannel.Value),
        _ => (ColorChannel.Hue, ColorChannel.Saturation),
    };

    private static double AxisFraction(HsvColor hsv, ColorChannel channel, ColorSpectrumComponents components)
    {
        var fraction = Fraction(hsv, channel);
        return Inverted(channel, components) ? 1 - fraction : fraction;
    }

    // WinUI puts saturation's maximum at the start of its axis when the spectrum pairs it with hue, and value's
    // maximum at the start of its axis otherwise (ColorSpectrum: "more hue on the outside").
    private static bool Inverted(ColorChannel channel, ColorSpectrumComponents components) =>
        components is ColorSpectrumComponents.HueSaturation or ColorSpectrumComponents.SaturationHue
            ? channel == ColorChannel.Saturation
            : channel == ColorChannel.Value;

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
}
