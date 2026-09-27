using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using System.Collections.Generic;

namespace WebcamPainter.Painting;

/// <summary>One selectable highlighter color: a layer name and its ink color.</summary>
public sealed class HighlighterColor
{
    /// <summary>Creates a highlighter color entry.</summary>
    public HighlighterColor(string name, Color color)
    {
        Name = name;
        Color = color;
        TextColor = GetReadableTextColor(color);
    }

    /// <summary>The color's display name; also the name of its drawing layer.</summary>
    public string Name { get; }

    /// <summary>The ink color.</summary>
    public Color Color { get; }

    /// <summary>
    /// The caption color that reads on <see cref="Color"/>: white on the darker inks, black on
    /// the lighter ones. Deciding it here, beside the ink, is what lets a button showing this
    /// color take both values from the palette instead of repeating either of them.
    /// </summary>
    public Color TextColor { get; }

    //BT.709 luminance is the perceptual measure, so yellow and green come out light at the
    //  same byte values that leave blue and indigo dark; the midpoint is the switch.
    private static Color GetReadableTextColor(Color color)
    {
        Rgba32 rgba = color.ToPixel<Rgba32>();
        float luminance = (rgba.R * 0.2126f) + (rgba.G * 0.7152f) + (rgba.B * 0.0722f);
        return luminance < 128f
            ? Color.FromRgb(255, 255, 255)
            : Color.FromRgb(0, 0, 0);
    }
}

/// <summary>
/// The set of highlighter colors a <see cref="PaintingSession"/> offers - one drawing layer
/// per color. Tweak the color values here; the Paint Mode buttons are templated over this
/// list, so what the user sees follows.
/// </summary>
public static class HighlighterPalette
{
    /// <summary>The ROYGBIV highlighter colors, in rainbow order.</summary>
    public static IReadOnlyList<HighlighterColor> Colors { get; } = new[]
    {
        new HighlighterColor("Red", Color.FromRgb(230, 30, 30)),
        new HighlighterColor("Orange", Color.FromRgb(255, 140, 20)),
        new HighlighterColor("Yellow", Color.FromRgb(240, 220, 20)),
        new HighlighterColor("Green", Color.FromRgb(40, 200, 60)),
        new HighlighterColor("Blue", Color.FromRgb(40, 90, 235)),
        new HighlighterColor("Indigo", Color.FromRgb(85, 45, 180)),
        new HighlighterColor("Violet", Color.FromRgb(190, 60, 220)),
    };
}
