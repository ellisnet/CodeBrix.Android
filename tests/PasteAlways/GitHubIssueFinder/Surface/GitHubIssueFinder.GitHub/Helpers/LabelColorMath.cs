using System;
using System.Globalization;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The colour arithmetic behind a label pill. GitHub gives each label a colour of its own,
/// and the application draws the same pill the website draws: the label colour laid faintly
/// over the page ground for the fill, more strongly for the border, and lightened or
/// darkened just enough for the text to stay readable on the ground it sits on.
/// Everything here is plain numbers, so it carries no dependency on any drawing type.
/// </summary>
public static class LabelColorMath
{
    /// <summary>How much of the label colour shows through in the pill fill.</summary>
    public const double BackgroundBlend = 0.18d;

    /// <summary>How much of the label colour shows through in the pill border.</summary>
    public const double BorderBlend = 0.45d;

    /// <summary>The lightest the pill text may be on a light page.</summary>
    public const double MaximumTextLightnessOnLight = 0.42d;

    /// <summary>The darkest the pill text may be on a dark page.</summary>
    public const double MinimumTextLightnessOnDark = 0.72d;

    /// <summary>
    /// How far the pill border must sit from the page ground on the lightness axis. A label
    /// whose colour is close to the ground, black on a dark page for instance, would otherwise
    /// blend into it and the pill would read as bare text.
    /// </summary>
    public const double MinimumBorderSeparation = 0.10d;

    /// <summary>
    /// Reads a label colour written as six hexadecimal digits, with or without a leading hash
    /// and in either case, for example "d73a4a", "#d73a4a" or "D73A4A".
    /// </summary>
    /// <param name="hex">The text to read.</param>
    /// <param name="argb">
    /// Receives the colour with an opaque alpha, for example 0xFFD73A4A; zero when the text
    /// could not be read.
    /// </param>
    /// <returns>True when the text was a colour.</returns>
    public static bool TryParseHex(string hex, out uint argb)
    {
        argb = 0u;
        if (string.IsNullOrWhiteSpace(hex)) { return false; }

        var text = hex.Trim();
        if (text.Length > 0 && text[0] == '#') { text = text.Substring(1); }
        if (text.Length != 6) { return false; }

        foreach (var character in text)
        {
            if (Uri.IsHexDigit(character)) { continue; }
            return false;
        }

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        argb = 0xFF000000u | rgb;
        return true;
    }

    /// <summary>
    /// Works out the three colours a label pill is drawn with.
    /// </summary>
    /// <param name="labelArgb">The label's own colour.</param>
    /// <param name="canvasArgb">The colour of the ground the pill sits on.</param>
    /// <param name="darkBase">True when the colour scheme's base theme is dark.</param>
    /// <returns>
    /// The pill fill, the pill border and the pill text, each with an opaque alpha.
    /// </returns>
    public static (uint Background, uint Border, uint Text) PillColors(uint labelArgb, uint canvasArgb,
        bool darkBase)
    {
        var background = Blend(labelArgb, canvasArgb, BackgroundBlend);
        var border = SeparateFromGround(Blend(labelArgb, canvasArgb, BorderBlend), canvasArgb);
        var text = ClampTextLightness(labelArgb, darkBase);
        return (background, border, text);
    }

    //Lays one colour over another. The sums stay in doubles and the result is rounded once,
    //on the way back to bytes.
    private static uint Blend(uint foreground, uint background, double amount)
    {
        var red = (Red(foreground) * amount) + (Red(background) * (1d - amount));
        var green = (Green(foreground) * amount) + (Green(background) * (1d - amount));
        var blue = (Blue(foreground) * amount) + (Blue(background) * (1d - amount));
        return Pack(red, green, blue);
    }

    //Pushes a border away from the ground it is drawn on until the two are far enough apart on
    //the lightness axis to be told apart. A label colour close to the page ground would otherwise
    //blend into it and the pill would lose its outline; the push is along lightness only, so the
    //label keeps its hue.
    private static uint SeparateFromGround(uint borderArgb, uint canvasArgb)
    {
        ToHsl(canvasArgb, out _, out _, out var groundLightness);
        ToHsl(borderArgb, out var hue, out var saturation, out var lightness);

        if (Math.Abs(lightness - groundLightness) >= MinimumBorderSeparation) { return borderArgb; }

        //A dark ground is pushed away from by going lighter, a light ground by going darker,
        //which is the direction that always has room.
        var wanted = groundLightness < 0.5d
            ? groundLightness + MinimumBorderSeparation
            : groundLightness - MinimumBorderSeparation;

        return FromHsl(hue, saturation, Math.Clamp(wanted, 0d, 1d));
    }

    //Moves the colour along the lightness axis only, so the label stays recognisable.
    private static uint ClampTextLightness(uint argb, bool darkBase)
    {
        ToHsl(argb, out var hue, out var saturation, out var lightness);
        lightness = darkBase
            ? Math.Max(lightness, MinimumTextLightnessOnDark)
            : Math.Min(lightness, MaximumTextLightnessOnLight);
        return FromHsl(hue, saturation, lightness);
    }

    //Splits a colour into hue in degrees, saturation and lightness, all as doubles.
    private static void ToHsl(uint argb, out double hue, out double saturation, out double lightness)
    {
        var red = Red(argb) / 255d;
        var green = Green(argb) / 255d;
        var blue = Blue(argb) / 255d;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var span = max - min;

        lightness = (max + min) / 2d;

        if (span <= 0d)
        {
            hue = 0d;
            saturation = 0d;
            return;
        }

        saturation = lightness > 0.5d
            ? span / (2d - max - min)
            : span / (max + min);

        double sixths;
        if (max == red)
        {
            sixths = ((green - blue) / span) % 6d;
            if (sixths < 0d) { sixths += 6d; }
        }
        else if (max == green)
        {
            sixths = ((blue - red) / span) + 2d;
        }
        else
        {
            sixths = ((red - green) / span) + 4d;
        }

        hue = sixths * 60d;
    }

    //Puts a colour back together from hue, saturation and lightness.
    private static uint FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1d - Math.Abs((2d * lightness) - 1d)) * saturation;
        var sixths = hue / 60d;
        var second = chroma * (1d - Math.Abs((sixths % 2d) - 1d));
        var lift = lightness - (chroma / 2d);

        double red;
        double green;
        double blue;

        if (sixths < 1d) { red = chroma; green = second; blue = 0d; }
        else if (sixths < 2d) { red = second; green = chroma; blue = 0d; }
        else if (sixths < 3d) { red = 0d; green = chroma; blue = second; }
        else if (sixths < 4d) { red = 0d; green = second; blue = chroma; }
        else if (sixths < 5d) { red = second; green = 0d; blue = chroma; }
        else { red = chroma; green = 0d; blue = second; }

        return Pack((red + lift) * 255d, (green + lift) * 255d, (blue + lift) * 255d);
    }

    private static double Red(uint argb) => (argb >> 16) & 0xFFu;

    private static double Green(uint argb) => (argb >> 8) & 0xFFu;

    private static double Blue(uint argb) => argb & 0xFFu;

    private static uint Pack(double red, double green, double blue) =>
        0xFF000000u | ((uint)ToByte(red) << 16) | ((uint)ToByte(green) << 8) | ToByte(blue);

    private static byte ToByte(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded <= 0d) { return 0; }
        if (rounded >= 255d) { return 255; }
        return (byte)rounded;
    }
}
