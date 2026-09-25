using System;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml.Documents;
using Windows.Foundation;
using ALayout = global::Android.Text.Layout;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="ITextLayout"/>: queries over an Android text
/// <see cref="ALayout"/> (the StaticLayout the measure produced), converted to DIPs.
/// </summary>
internal sealed class AndroidTextLayout : ITextLayout
{
    private readonly string _text;
    private readonly double _density;

    internal AndroidTextLayout(ALayout layout, string text, double density)
    {
        Layout = layout;
        _text = text ?? string.Empty;
        _density = density;
    }

    /// <summary>The Android layout (pixels).</summary>
    internal ALayout Layout { get; }

    /// <summary>The laid-out text.</summary>
    internal string Text => _text;

    /// <inheritdoc />
    public bool IsBaseDirectionRightToLeft =>
        Layout.LineCount > 0 && Layout.GetParagraphDirection(0) == global::Android.Text.TextLayoutDirection.RightToLeft;

    /// <inheritdoc />
    public Rect GetRectForIndex(int adjustedIndex)
    {
        var index = Math.Clamp(adjustedIndex, 0, _text.Length);
        var line = Layout.GetLineForOffset(index);
        var x = Layout.GetPrimaryHorizontal(index);
        var width = 0f;
        if (index < _text.Length && Layout.GetLineForOffset(index + 1) == line)
        {
            width = Math.Abs(Layout.GetPrimaryHorizontal(index + 1) - x);
        }

        var top = Layout.GetLineTop(line);
        var bottom = Layout.GetLineBottom(line);
        return new Rect(Dip(x), Dip(top), Dip(width), Dip(bottom - top));
    }

    /// <inheritdoc />
    public int GetIndexAt(Point p, bool ignoreEndingNewLine, bool extendedSelection)
    {
        if (_text.Length == 0)
        {
            return 0;
        }

        var line = Layout.GetLineForVertical((int)Math.Round(p.Y * _density));
        var offset = Layout.GetOffsetForHorizontal(line, (float)(p.X * _density));
        if (ignoreEndingNewLine && offset > 0 && offset == Layout.GetLineEnd(line) && _text[offset - 1] == '\n')
        {
            offset--;
        }

        return Math.Clamp(offset, 0, _text.Length);
    }

    /// <inheritdoc />
    public Hyperlink GetHyperlinkAt(Point point) => null;

    /// <inheritdoc />
    public (int, int) GetWordAt(int index, bool right) => WordBoundaries.GetWordAt(_text, index, right);

    /// <inheritdoc />
    public (int, int, bool, bool, int) GetLineAt(int index)
    {
        if (Layout.LineCount == 0)
        {
            return (0, 0, true, true, 0);
        }

        var line = Layout.GetLineForOffset(Math.Clamp(index, 0, _text.Length));
        var start = Layout.GetLineStart(line);
        var end = Layout.GetLineEnd(line);
        return (start, end - start, line == 0, line == Layout.LineCount - 1, line);
    }

    private double Dip(double px) => TextMeasureMath.PxToDip(px, _density);
}

/// <summary>The layout of a TextBlock that has not been measured, or that has no text.</summary>
internal sealed class EmptyTextLayout : ITextLayout
{
    internal static readonly EmptyTextLayout Instance = new();

    /// <inheritdoc />
    public bool IsBaseDirectionRightToLeft => false;

    /// <inheritdoc />
    public Rect GetRectForIndex(int adjustedIndex) => default;

    /// <inheritdoc />
    public int GetIndexAt(Point p, bool ignoreEndingNewLine, bool extendedSelection) => 0;

    /// <inheritdoc />
    public Hyperlink GetHyperlinkAt(Point point) => null;

    /// <inheritdoc />
    public (int, int) GetWordAt(int index, bool right) => (0, 0);

    /// <inheritdoc />
    public (int, int, bool, bool, int) GetLineAt(int index) => (0, 0, true, true, 0);
}
