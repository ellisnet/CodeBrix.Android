using System;
using CodeBrix.Android.UI.Platform.Text;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="ITextBoxPlatform"/> (plan 3, ITextBoxPlatform row): a thin
/// adapter over the native editor (<see cref="INativeTextEditor"/>) the TextBox / PasswordBox handler
/// connects - <see cref="IsManagedEditing"/> is false, so text, caret, selection handles, the IME and
/// clipboard gestures belong to the native TextInputEditText. Core's selection reads come from the
/// editor, Core's text and selection writes go to it, Core's focus drives the editor's focus, and the
/// managed pointer/key editing hooks are no-ops (the editor does its own). While no handler is connected
/// (a TextBox outside a live tree) the adapter keeps the text and selection itself.
/// </summary>
internal sealed class TextBoxAndroidPlatform : ITextBoxPlatform
{
    private readonly WeakReference<TextBox> _owner;
    private int _selectionStart;
    private int _selectionLength;
    private int _selectionStartBeforeKeyDown;

    internal TextBoxAndroidPlatform(TextBox owner) => _owner = new WeakReference<TextBox>(owner);

    /// <summary>The native editor of the connected handler (null while none is connected).</summary>
    internal INativeTextEditor Editor { get; private set; }

    /// <summary>The last text Core pushed to the native editor.</summary>
    internal string NativeText { get; private set; } = string.Empty;

    /// <inheritdoc />
    public bool IsManagedEditing => false;

    /// <inheritdoc />
    public int SelectionStart => Editor?.SelectionStart ?? _selectionStart;

    /// <inheritdoc />
    public int SelectionLength => Editor?.SelectionLength ?? _selectionLength;

    /// <inheritdoc />
    public bool IsBackwardSelection => Editor?.IsBackwardSelection ?? false;

    /// <inheritdoc />
    public TextBox.CaretDisplayMode CaretMode => default;

    /// <inheritdoc />
    public int SelectionStartBeforeKeyDown => _selectionStartBeforeKeyDown;

    /// <summary>The adapter of a TextBox (null when the TextBox was created before this platform was registered).</summary>
    /// <param name="textBox">The TextBox.</param>
    /// <returns>The adapter, or null.</returns>
    internal static TextBoxAndroidPlatform Of(TextBox textBox) => textBox?.TextBoxPlatform as TextBoxAndroidPlatform;

    /// <summary>Connects the native editor of a handler (null disconnects it, keeping its last selection).</summary>
    /// <param name="editor">The editor.</param>
    internal void Attach(INativeTextEditor editor)
    {
        if (editor == null && Editor != null)
        {
            _selectionStart = Editor.SelectionStart;
            _selectionLength = Editor.SelectionLength;
        }

        Editor = editor;
        if (editor != null)
        {
            editor.SetTextFromCore(NativeText);
            editor.Select(_selectionStart, _selectionLength);
        }
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_owner.TryGetTarget(out var owner))
        {
            NativeText = owner.Text ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public void OnUnloaded()
    {
    }

    /// <inheritdoc />
    public void ResetTextView()
    {
    }

    /// <inheritdoc />
    public void UpdateTextView() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void SetTextNative(string text)
    {
        NativeText = text ?? string.Empty;
        Editor?.SetTextFromCore(NativeText);
    }

    /// <inheritdoc />
    public void OnTextChanged()
    {
        if (_owner.TryGetTarget(out var owner))
        {
            NativeText = owner.Text ?? string.Empty;
            Editor?.SetTextFromCore(NativeText);
        }

        ClampPendingSelection(NativeText.Length);
    }

    /// <inheritdoc />
    public void OnTextInputProcessed(string oldText)
    {
    }

    /// <inheritdoc />
    public string CoerceMultilineText(string text)
    {
        if (string.IsNullOrEmpty(text) || !_owner.TryGetTarget(out var owner) || owner.AcceptsReturn)
        {
            return text;
        }

        // A single-line TextBox keeps the first line only (what the Fluent TextBox does with pasted lines).
        var end = text.IndexOfAny(new[] { '\r', '\n' });
        return end < 0 ? text : text[..end];
    }

    /// <inheritdoc />
    public void ClampPendingSelection(int textLength)
    {
        _selectionStart = Math.Clamp(_selectionStart, 0, Math.Max(0, textLength));
        _selectionLength = Math.Clamp(_selectionLength, 0, Math.Max(0, textLength - _selectionStart));
    }

    /// <inheritdoc />
    public void ClearPendingSelection()
    {
    }

    /// <inheritdoc />
    public void OnBeforeTextChangingCanceled()
    {
    }

    /// <inheritdoc />
    public void Select(int start, int length)
    {
        _selectionStart = Math.Max(0, start);
        _selectionLength = Math.Max(0, length);
        ClampPendingSelection(NativeText.Length);
        Editor?.Select(_selectionStart, _selectionLength);
    }

    /// <inheritdoc />
    public void OnPasteFromClipboard(string adjustedClipboardText, int selectionStart, string newText)
    {
    }

    /// <inheritdoc />
    public void OnPasteStarting()
    {
    }

    /// <inheritdoc />
    public void OnPasteFinished()
    {
    }

    /// <inheritdoc />
    public void OnCutSelectionToClipboard()
    {
    }

    /// <inheritdoc />
    public void OnCutStarting()
    {
    }

    /// <inheritdoc />
    public void OnCutFinished()
    {
    }

    /// <inheritdoc />
    public void Undo() => Editor?.Undo();

    /// <inheritdoc />
    public void Redo() => Editor?.Redo();

    /// <summary>The framework editor's undo history cannot be cleared from outside: no-op.</summary>
    public void ClearUndoRedoHistory()
    {
    }

    /// <inheritdoc />
    public void OnKeyDown(KeyRoutedEventArgs args)
    {
        // A focused native editor gets its keys before Core (Input/ActivityInputRouter): a key reaching Core here
        // is one the editor did not use, so the selection is the editor's own and unchanged by the key.
        _selectionStartBeforeKeyDown = SelectionStart;
    }

    /// <inheritdoc />
    public void OnPostKeyDown(KeyRoutedEventArgs args)
    {
    }

    /// <inheritdoc />
    public void OnPointerPressed(PointerRoutedEventArgs args)
    {
    }

    /// <inheritdoc />
    public void OnPointerReleased(PointerRoutedEventArgs args, bool wasFocused)
    {
    }

    /// <inheritdoc />
    public void OnPointerCaptureLost(PointerRoutedEventArgs args)
    {
    }

    /// <inheritdoc />
    public void OnPointerMoved(PointerRoutedEventArgs args)
    {
    }

    /// <inheritdoc />
    public void OnRightTapped(RightTappedRoutedEventArgs args)
    {
    }

    /// <inheritdoc />
    public void OnFocusedByPointer()
    {
    }

    /// <inheritdoc />
    public void OnFocusStateChanged(FocusState focusState, bool initial) => Editor?.OnCoreFocusStateChanged(focusState);

    /// <inheritdoc />
    public void DispatchUpdateScrolling()
    {
    }

    /// <inheritdoc />
    public void OnForegroundColorChanged(Brush newValue) => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void OnSelectionHighlightColorChanged(SolidColorBrush brush) => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void UpdateFont() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void UpdateTextViewProperties() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void OnMaxLengthChanged() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void OnFlowDirectionChanged() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void OnTextWrappingChanged() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void OnTextAlignmentChanged() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void InvalidateOverlayLayout()
    {
    }

    /// <inheritdoc />
    public void OnPasswordCharChanged() => Editor?.RefreshFromCore();

    /// <inheritdoc />
    public void SetPasswordRevealState(PasswordRevealState state)
    {
    }
}
