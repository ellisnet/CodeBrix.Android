using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Platform.Text;

/// <summary>
/// What the TextBox platform adapter (<c>TextBoxAndroidPlatform</c>, Core's ITextBoxPlatform with
/// IsManagedEditing = false) asks of the native editor a TextBox / PasswordBox handler connected:
/// selection, text written by Core, focus and undo/redo (keys reach the focused editor before Core:
/// Input/ActivityInputRouter).
/// </summary>
internal interface INativeTextEditor
{
    /// <summary>The native selection start (UTF-16 index).</summary>
    int SelectionStart { get; }

    /// <summary>The native selection length.</summary>
    int SelectionLength { get; }

    /// <summary>True when the native selection runs backwards (the caret at its start).</summary>
    bool IsBackwardSelection { get; }

    /// <summary>Writes Core's text into the editor (no echo back to Core; the selection is kept inside the text).</summary>
    /// <param name="text">The text.</param>
    void SetTextFromCore(string text);

    /// <summary>Selects a range (Core's Select / SelectAll).</summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    void Select(int start, int length);

    /// <summary>Follows Core's focus: native focus (and the soft keyboard for pointer focus) or none.</summary>
    /// <param name="state">The TextBox's new focus state.</param>
    void OnCoreFocusStateChanged(FocusState state);

    /// <summary>Undoes the last edit (the framework editor's own undo stack).</summary>
    void Undo();

    /// <summary>Redoes the last undone edit.</summary>
    void Redo();

    /// <summary>Re-reads the formatting properties (font, colours, alignment, wrapping, MaxLength, password char).</summary>
    void RefreshFromCore();
}
