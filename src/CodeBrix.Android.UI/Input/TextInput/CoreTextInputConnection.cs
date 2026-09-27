using CodeBrix.Android.UI.Portable.TextInput;
using ABaseInputConnection = global::Android.Views.InputMethods.BaseInputConnection;
using ICharSequence = Java.Lang.ICharSequence;
using AImeAction = global::Android.Views.InputMethods.ImeAction;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The input connection a soft keyboard types into while a custom text-entry control has the focus: the keyboard's
/// text reaches the control as KEY PRESSES (the control reads KeyDown; it has no text the keyboard could edit).
/// Committed text and a finished composition are turned into key presses and the connection's own editable is
/// emptied again, so the keyboard always sees an empty field: a delete it asks for then arrives as
/// <see cref="DeleteSurroundingText"/> with nothing to delete, which becomes Backspace presses. A composition in
/// progress stays in the keyboard (in the editable) until it is committed. Keys the keyboard sends as key events
/// (Enter, Backspace, the arrows of some layouts) take the ordinary key path (the activity, then Core).
/// </summary>
internal sealed class CoreTextInputConnection : ABaseInputConnection
{
    private readonly CoreTextInputView _view;

    /// <summary>Creates the connection of <paramref name="view"/> (a full editor: it keeps an editable).</summary>
    /// <param name="view">The text-input view.</param>
    internal CoreTextInputConnection(CoreTextInputView view)
        : base(view, true)
    {
        _view = view;
    }

    /// <inheritdoc />
    public override global::Android.Text.IEditable Editable => _view.Editable;

    /// <inheritdoc />
    public override bool CommitText(ICharSequence text, int newCursorPosition)
    {
        base.CommitText(text, newCursorPosition);
        SendEditable();
        return true;
    }

    /// <inheritdoc />
    public override bool FinishComposingText()
    {
        base.FinishComposingText();
        SendEditable();
        return true;
    }

    /// <inheritdoc />
    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        // The editable is empty unless a composition is in progress: the delete is for text the control holds.
        if (Editable is not { } editable || editable.Length() == 0)
        {
            _view.Deliver(TextInputKeystrokes.ForDeletion(beforeLength, afterLength));
            return true;
        }

        return base.DeleteSurroundingText(beforeLength, afterLength);
    }

    /// <inheritdoc />
    public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
    {
        if (Editable is not { } editable || editable.Length() == 0)
        {
            _view.Deliver(TextInputKeystrokes.ForDeletion(beforeLength, afterLength));
            return true;
        }

        return base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
    }

    /// <inheritdoc />
    public override bool PerformEditorAction(AImeAction actionCode)
    {
        // The keyboard's action key (shown as Enter for a terminal: IME_ACTION_NONE): an Enter press.
        SendEditable();
        _view.Deliver(TextInputKeystrokes.ForText("\n"));
        return true;
    }

    private void SendEditable()
    {
        if (Editable is not { } editable || editable.Length() == 0)
        {
            return;
        }

        var text = editable.ToString();
        editable.Clear();
        _view.Deliver(TextInputKeystrokes.ForText(text));
    }
}
