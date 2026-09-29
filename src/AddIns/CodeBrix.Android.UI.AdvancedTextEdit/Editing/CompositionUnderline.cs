using System;
using CodeBrix.Platform.UI.AdvancedTextEdit.Document;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using CodeBrix.Platform.UI.AdvancedTextEdit.Rendering;
using SkiaSharp;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Editing;

/// <summary>
/// The underline under the text an input method is composing (the platform convention: the word the keyboard is still
/// working on is underlined until it is committed). It is a background renderer of the editor's own TextView (the Core's
/// extension point, IBackgroundRenderer on the selection layer): the rectangles are the TextView's own geometry for the
/// range (BackgroundGeometryBuilder.GetRectsForSegment), the colour the text area's foreground.
/// </summary>
internal sealed class CompositionUnderline : IBackgroundRenderer
{
    private const float Thickness = 1.5f;

    private readonly TextView _view;
    private readonly TextArea _area;
    private int _start = -1;
    private int _end = -1;
    private bool _attached;

    /// <summary>Creates the underline of <paramref name="view"/> (drawn only while shown).</summary>
    /// <param name="view">The text area's text view.</param>
    /// <param name="area">The text area (its foreground is the underline's colour).</param>
    internal CompositionUnderline(TextView view, TextArea area)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _area = area ?? throw new ArgumentNullException(nameof(area));
    }

    /// <summary>The first offset of the underlined range, or -1.</summary>
    internal int Start => _start;

    /// <summary>The offset after the underlined range, or -1.</summary>
    internal int End => _end;

    /// <summary>How many times the underline drew a range (diagnostics and device fences).</summary>
    internal int DrawCount { get; private set; }

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Underlines [<paramref name="start"/>, <paramref name="end"/>).</summary>
    /// <param name="start">The first offset.</param>
    /// <param name="end">The offset after the range.</param>
    internal void Show(int start, int end)
    {
        _start = start;
        _end = end;
        if (!_attached)
        {
            _attached = true;
            _view.BackgroundRenderers.Add(this);
        }

        _view.InvalidateLayer(Layer);
    }

    /// <summary>Takes the underline away.</summary>
    internal void Hide()
    {
        if (_start < 0)
        {
            return;
        }

        _start = -1;
        _end = -1;
        _view.InvalidateLayer(Layer);
    }

    /// <summary>Takes the underline away and leaves the text view (the soft-keyboard session ended).</summary>
    internal void Detach()
    {
        Hide();
        if (_attached)
        {
            _attached = false;
            _view.BackgroundRenderers.Remove(this);
        }
    }

    /// <inheritdoc />
    public void Draw(TextView textView, SKCanvas canvas)
    {
        if (_start < 0 || textView?.Document == null || canvas == null || !textView.VisualLinesValid)
        {
            return;
        }

        var length = Math.Min(_end, textView.Document.TextLength) - _start;
        if (length <= 0)
        {
            return;
        }

        var color = VisualLineElementTextRunProperties.GetSolidColor(_area.Foreground) ?? SKColors.Black;
        using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new SimpleSegment(_start, length)))
        {
            canvas.DrawRect(SKRect.Create((float)rect.X, (float)(rect.Y + rect.Height - Thickness), (float)rect.Width, Thickness), paint);
        }

        DrawCount++;
    }
}
