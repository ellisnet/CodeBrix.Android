// STUB (paste always): CodeBrix.Platform.UI.TextLayout (package CodeBrix.Platform.TextLayout.ApacheLicenseForever,
// a CodeBrix.Platform repo add-in); it has no Android flavor yet. Same namespace and type names; the members
// Pinta.Brix.Engine's TextLayout uses (member names from the add-in's public API).
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace CodeBrix.Platform.UI.TextLayout;

/// <summary>Horizontal alignment of laid-out text.</summary>
public enum TextAlign
{
    /// <summary>Left.</summary>
    Left,
    /// <summary>Center.</summary>
    Center,
    /// <summary>Right.</summary>
    Right,
}

/// <summary>Font weight (100..900).</summary>
public enum TextFontWeight
{
    /// <summary>100.</summary>
    Thin = 100,
    /// <summary>200.</summary>
    ExtraLight = 200,
    /// <summary>300.</summary>
    Light = 300,
    /// <summary>400.</summary>
    Normal = 400,
    /// <summary>500.</summary>
    Medium = 500,
    /// <summary>600.</summary>
    SemiBold = 600,
    /// <summary>700.</summary>
    Bold = 700,
    /// <summary>800.</summary>
    ExtraBold = 800,
    /// <summary>900.</summary>
    Black = 900,
}

/// <summary>Font style.</summary>
public enum TextFontStyle
{
    /// <summary>Upright.</summary>
    Normal,
    /// <summary>Oblique.</summary>
    Oblique,
    /// <summary>Italic.</summary>
    Italic,
}

/// <summary>Font stretch.</summary>
public enum TextFontStretch
{
    /// <summary>Normal width.</summary>
    Normal = 5,
}

/// <summary>Base text direction.</summary>
public enum TextDirection
{
    /// <summary>From the text.</summary>
    Auto,
    /// <summary>Left to right.</summary>
    LeftToRight,
    /// <summary>Right to left.</summary>
    RightToLeft,
}

/// <summary>One run of text in one font.</summary>
public sealed class TextRunDescriptor
{
    /// <summary>Creates a run.</summary>
    public TextRunDescriptor(string text, string fontFamily, float fontSize, TextFontWeight weight = TextFontWeight.Normal,
        TextFontStyle style = TextFontStyle.Normal, TextFontStretch stretch = TextFontStretch.Normal, TextDirection direction = TextDirection.Auto)
    {
        Text = text;
        FontFamily = fontFamily;
        FontSize = fontSize;
        Weight = weight;
        Style = style;
        Stretch = stretch;
        Direction = direction;
    }

    /// <summary>The text.</summary>
    public string Text { get; }
    /// <summary>The font family.</summary>
    public string FontFamily { get; }
    /// <summary>The font size.</summary>
    public float FontSize { get; }
    /// <summary>The weight.</summary>
    public TextFontWeight Weight { get; }
    /// <summary>The style.</summary>
    public TextFontStyle Style { get; }
    /// <summary>The stretch.</summary>
    public TextFontStretch Stretch { get; }
    /// <summary>The direction.</summary>
    public TextDirection Direction { get; }
}

/// <summary>Layout options.</summary>
public sealed class TextLayoutOptions
{
    /// <summary>The wrapping / alignment width.</summary>
    public float? MaxWidth { get; set; }
    /// <summary>The alignment.</summary>
    public TextAlign Alignment { get; set; }
}

/// <summary>A finished layout.</summary>
public sealed class TextLayoutResult : IDisposable
{
    /// <summary>The laid-out text.</summary>
    public string Text { get; } = string.Empty;
    /// <summary>The layout size.</summary>
    public SKSize Size { get; }
    /// <summary>The number of lines.</summary>
    public int LineCount { get; }
    /// <summary>The line height.</summary>
    public float LineHeight { get; }
    /// <summary>The caret rectangle at a character index.</summary>
    public SKRect GetCaretRect(int index, float width) => SKRect.Empty;
    /// <summary>The nearest character index to a point.</summary>
    public int GetNearestIndexAt(SKPoint point) => 0;
    /// <summary>The selection rectangles of a range.</summary>
    public IReadOnlyList<SKRect> GetSelectionRects(int start, int length) => Array.Empty<SKRect>();
    /// <summary>The outline of the laid-out glyphs.</summary>
    public SKPath GetOutlinePath() => new SKPath();
    /// <summary>Draws the layout.</summary>
    public void Draw(SKCanvas canvas, SKPoint origin, SKPaint paint) { }
    /// <summary>Draws the layout at the origin.</summary>
    public void Draw(SKCanvas canvas, SKPaint paint) { }
    /// <summary>Releases the layout.</summary>
    public void Dispose() { }
}

/// <summary>The layout entry point.</summary>
public static class TextLayoutEngine
{
    /// <summary>Lays out runs.</summary>
    public static TextLayoutResult Layout(IReadOnlyList<TextRunDescriptor> runs, TextLayoutOptions? options) =>
        throw new NotSupportedException("Text layout is not part of the paste-always compile head.");

    /// <summary>Lays out one string in one font.</summary>
    public static TextLayoutResult Layout(string text, string fontFamily, float fontSize, TextLayoutOptions? options = null) =>
        throw new NotSupportedException("Text layout is not part of the paste-always compile head.");
}
