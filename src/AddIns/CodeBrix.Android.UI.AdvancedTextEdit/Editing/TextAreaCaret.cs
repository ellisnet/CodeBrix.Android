using System;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using CodeBrix.Platform.UI.AdvancedTextEdit.Rendering;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Editing;

/// <summary>
/// [AP8-S item L] The caret of an AdvancedTextEdit TextArea for CodeBrix.Android.UI's soft-keyboard session
/// (<see cref="ICoreTextInputCaret"/>): the Core's own caret rectangle (<see cref="Caret.CalculateCaretRectangle"/>, in the
/// document's visual coordinates) moved by the TextView's scroll offset onto the TextView - the element the caret is
/// drawn on. It reports a move when the caret moves, the view scrolls or its visual lines are rebuilt (typing, a re-wrap,
/// a resize). With it the soft keyboard's focus view sits on the caret, and Android's pan brings the caret above the
/// keyboard. Everything is the Core's; this class only reads it. UI thread.
/// </summary>
internal sealed class TextAreaCaret : ICoreTextInputCaret
{
    private readonly TextArea _area;
    private readonly TextView _view;
    private bool _disposed;

    /// <summary>Creates the caret of <paramref name="area"/> (it listens to the area until disposed).</summary>
    /// <param name="area">The text area.</param>
    /// <exception cref="ArgumentNullException"><paramref name="area"/> is null.</exception>
    internal TextAreaCaret(TextArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _view = area.TextView;
        _area.Caret.PositionChanged += OnMoved;
        _view.ScrollOffsetChanged += OnMoved;
        _view.VisualLinesChanged += OnMoved;
    }

    /// <inheritdoc />
    public event EventHandler Moved;

    /// <inheritdoc />
    public FrameworkElement Element => _view;

    /// <inheritdoc />
    public bool TryGetBounds(out Rect bounds)
    {
        bounds = default;
        if (_disposed || _area.Document == null || !_view.VisualLinesValid)
        {
            return false;
        }

        var caret = _area.Caret.CalculateCaretRectangle();
        if (caret.IsEmpty)
        {
            return false;
        }

        bounds = new Rect(caret.X - _view.HorizontalOffset, caret.Y - _view.VerticalOffset, caret.Width, caret.Height);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _area.Caret.PositionChanged -= OnMoved;
        _view.ScrollOffsetChanged -= OnMoved;
        _view.VisualLinesChanged -= OnMoved;
    }

    private void OnMoved(object sender, EventArgs e) => Moved?.Invoke(this, EventArgs.Empty);
}
