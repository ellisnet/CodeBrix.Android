using System;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Platform.UI.TerminalView;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.TerminalView.Input;

/// <summary>
/// [AP8-S batch 4] The caret of a TerminalControl for CodeBrix.Android.UI's soft-keyboard session
/// (<see cref="ICoreTextInputCaret"/>): the terminal's cursor cell, read from the Core's platform caret seam
/// (TerminalControl.GetCaretRectForPlatform - the cell in the control's own coordinates, DIPs; Rect.Empty while the
/// hosted application hides the cursor or its line is scrolled out of view) and followed through
/// TerminalControl.CaretRectChangedForPlatform (output moved the cursor, the view scrolled, the grid was refitted, the
/// font changed). With it the soft keyboard's focus view sits on the cursor cell, and Android's pan (the default
/// soft-input mode) brings the cursor row - the prompt - above the keyboard. A move of the whole control is not a caret
/// change: the text-input view follows the element's layout itself. Everything is the Core's; this class only reads it.
/// UI thread.
/// </summary>
internal sealed class TerminalCaret : ICoreTextInputCaret
{
    private readonly TerminalControl _control;
    private bool _disposed;

    /// <summary>Creates the caret of <paramref name="control"/> (it listens to the control until disposed).</summary>
    /// <param name="control">The terminal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    internal TerminalCaret(TerminalControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.CaretRectChangedForPlatform += OnMoved;
    }

    /// <inheritdoc />
    public event EventHandler Moved;

    /// <inheritdoc />
    public FrameworkElement Element => _control;

    /// <inheritdoc />
    public bool TryGetBounds(out Rect bounds)
    {
        bounds = default;
        if (_disposed)
        {
            return false;
        }

        var caret = _control.GetCaretRectForPlatform();
        if (caret.IsEmpty)
        {
            return false;
        }

        bounds = caret;
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
        _control.CaretRectChangedForPlatform -= OnMoved;
    }

    private void OnMoved(object sender, EventArgs e) => Moved?.Invoke(this, EventArgs.Empty);
}
