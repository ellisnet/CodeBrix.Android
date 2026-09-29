using System;

namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// The text a CUSTOM text-entry control (a Core control that reports its focus through CodeBrix.Platform's
/// SoftwareKeyboardFocus seam) lets a soft keyboard SEE and EDIT: its document, its selection, and the edits an input
/// method makes (surrounding text, commit, composition, delete around the cursor, set selection). An add-in registers
/// a factory of targets for its control (Input/TextInput/CoreTextInput.RegisterTarget); the Android input connection
/// then works on the control's own text through <see cref="TextInputTargetEditor"/> instead of turning the keyboard's
/// text into key presses. A control without a target keeps the key-press path (the TerminalView add-in).
/// </summary>
/// <remarks>
/// Offsets are UTF-16 code units from the start of the control's text (the unit an Android input method counts in).
/// Every member is called on the UI thread. A target never throws for an offset out of range: the editor clamps first.
/// A target is made when a soft-keyboard session opens on its control and disposed when the session ends (it stops
/// listening to the control).
/// </remarks>
internal interface ICoreTextInputTarget : IDisposable
{
    /// <summary>The length of the control's text.</summary>
    int TextLength { get; }

    /// <summary>The lower end of the selection (the caret when nothing is selected).</summary>
    int SelectionStart { get; }

    /// <summary>The upper end of the selection (the caret when nothing is selected).</summary>
    int SelectionEnd { get; }

    /// <summary>Raised when the control's text or selection changed, whatever changed it (a hardware key, a finger,
    /// the application, or this target's own edits).</summary>
    event EventHandler<CoreTextInputChange> Changed;

    /// <summary>A part of the control's text.</summary>
    /// <param name="start">The first offset (0 .. <see cref="TextLength"/>).</param>
    /// <param name="length">The number of code units (the range lies inside the text).</param>
    /// <returns>The text.</returns>
    string GetText(int start, int length);

    /// <summary>True when the range may be replaced (false: part of it is read-only).</summary>
    /// <param name="start">The first offset of the range.</param>
    /// <param name="end">The offset after the range (equal to <paramref name="start"/>: an insertion point).</param>
    /// <returns>Whether an edit of the range is allowed.</returns>
    bool CanEdit(int start, int end);

    /// <summary>
    /// Types <paramref name="text"/> at the selection the way the control types a key's character (the control's own
    /// typing path: its text-input events, its line-break handling, its completion), replacing the selection.
    /// </summary>
    /// <param name="text">The text (not empty).</param>
    /// <returns>The caret offset after the typing, or -1 when the control refused it.</returns>
    int Type(string text);

    /// <summary>Replaces a range of the text as it is (no typing behaviour), leaving the selection to the caller.</summary>
    /// <param name="start">The first offset of the range.</param>
    /// <param name="end">The offset after the range.</param>
    /// <param name="text">The replacement (empty: a deletion).</param>
    /// <returns>The offset after the inserted text, or -1 when the control refused the edit.</returns>
    int Replace(int start, int end, string text);

    /// <summary>Selects a range (<paramref name="start"/> = <paramref name="end"/>: puts the caret there).</summary>
    /// <param name="start">The anchor.</param>
    /// <param name="end">The active end (where the caret goes).</param>
    void Select(int start, int end);

    /// <summary>Shows (or, with -1, -1, stops showing) the range an input method is composing - an underline.</summary>
    /// <param name="start">The first offset of the composition, or -1.</param>
    /// <param name="end">The offset after it, or -1.</param>
    void ShowComposition(int start, int end);

    /// <summary>Runs a command an input method's toolbar asks for.</summary>
    /// <param name="command">The command.</param>
    /// <returns>True when the control ran it.</returns>
    bool Perform(CoreTextInputCommand command);

    /// <summary>
    /// Starts a group of edits the control keeps together (one undo step): one call of an input method, begun and ended
    /// synchronously by <see cref="TextInputTargetEditor"/>; nests.
    /// </summary>
    void BeginBatch();

    /// <summary>Ends the group <see cref="BeginBatch"/> started.</summary>
    void EndBatch();
}
