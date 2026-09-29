using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.TextInput;
using Microsoft.Extensions.Logging;
using ABaseInputConnection = global::Android.Views.InputMethods.BaseInputConnection;
using ICharSequence = Java.Lang.ICharSequence;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using ACapitalizationMode = global::Android.Text.CapitalizationMode;
using AGetTextFlags = global::Android.Views.InputMethods.GetTextFlags;
using ATextUtils = global::Android.Text.TextUtils;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The input connection a soft keyboard types into while a custom text-entry control has the focus. Two paths:
/// <list type="bullet">
/// <item><b>Text target</b> (the control's add-in registered one - <see cref="CoreTextInput.RegisterTarget"/>): every
/// call the input method makes works on the control's OWN text through <see cref="CoreTextInputView.TargetEditor"/>
/// (Portable/TextInput/TextInputTargetEditor): it reads the text around the cursor and the selection, commits and
/// composes in place (the composition shown underlined by the control), deletes around the cursor, moves the
/// selection, runs select-all / cut / copy / paste, and batches; the view tells the input method when the selection
/// moves.</item>
/// <item><b>Key presses</b> (no target: the terminal): the keyboard's text reaches the control as KEY PRESSES (the
/// control reads KeyDown; it has no text the keyboard could edit). Committed text and a finished composition are turned
/// into key presses and the connection's own editable is emptied again, so the keyboard always sees an empty field: a
/// delete it asks for then arrives as <see cref="DeleteSurroundingText"/> with nothing to delete, which becomes
/// Backspace presses. A composition in progress stays in the keyboard (in the editable) until it is committed.</item>
/// </list>
/// Keys the keyboard sends as key events (Enter, Backspace, the arrows of some layouts) take the ordinary key path
/// (the activity, then Core) on both paths.
/// </summary>
internal sealed class CoreTextInputConnection : ABaseInputConnection
{
    private readonly CoreTextInputView _view;
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.TextInput");

    /// <summary>Creates the connection of <paramref name="view"/> (a full editor: it keeps an editable).</summary>
    /// <param name="view">The text-input view.</param>
    internal CoreTextInputConnection(CoreTextInputView view)
        : base(view, true)
    {
        _view = view;
    }

    /// <inheritdoc />
    public override global::Android.Text.IEditable Editable => _view.Editable;

    private TextInputTargetEditor Target => _view.TargetEditor;

    /// <inheritdoc />
    public override ICharSequence GetTextBeforeCursorFormatted(int n, AGetTextFlags flags) =>
        Target is { } target ? new Java.Lang.String(target.GetTextBeforeCursor(n)) : base.GetTextBeforeCursorFormatted(n, flags);

    /// <inheritdoc />
    public override ICharSequence GetTextAfterCursorFormatted(int n, AGetTextFlags flags) =>
        Target is { } target ? new Java.Lang.String(target.GetTextAfterCursor(n)) : base.GetTextAfterCursorFormatted(n, flags);

    /// <inheritdoc />
    public override ICharSequence GetSelectedTextFormatted(AGetTextFlags flags)
    {
        if (Target is not { } target)
        {
            return base.GetSelectedTextFormatted(flags);
        }

        return target.GetSelectedText() is { } selected ? new Java.Lang.String(selected) : null;
    }

    /// <inheritdoc />
    public override ACapitalizationMode GetCursorCapsMode(ACapitalizationMode reqModes)
    {
        if (Target is not { } target)
        {
            return base.GetCursorCapsMode(reqModes);
        }

        var before = target.GetTextBeforeCursor(256);
        using var text = new Java.Lang.String(before);
        return ATextUtils.GetCapsMode(text, before.Length, reqModes);
    }

    /// <inheritdoc />
    public override bool CommitText(ICharSequence text, int newCursorPosition)
    {
        if (Target is { } target)
        {
            Trace("commit", text?.ToString(), newCursorPosition);
            return target.Commit(text?.ToString(), newCursorPosition);
        }

        base.CommitText(text, newCursorPosition);
        SendEditable();
        return true;
    }

    /// <inheritdoc />
    public override bool SetComposingText(ICharSequence text, int newCursorPosition) =>
        Target is { } target ? Trace("compose", text?.ToString(), newCursorPosition) && target.SetComposingText(text?.ToString(), newCursorPosition)
            : base.SetComposingText(text, newCursorPosition);

    /// <inheritdoc />
    public override bool SetComposingRegion(int start, int end) =>
        Target is { } target ? Trace("region", null, start, end) && target.SetComposingRegion(start, end) : base.SetComposingRegion(start, end);

    /// <inheritdoc />
    public override bool FinishComposingText()
    {
        if (Target is { } target)
        {
            Trace("finish", null);
            return target.FinishComposingText();
        }

        base.FinishComposingText();
        SendEditable();
        return true;
    }

    /// <inheritdoc />
    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (Target is { } target)
        {
            Trace("delete", null, beforeLength, afterLength);
            return target.DeleteSurroundingText(beforeLength, afterLength);
        }

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
        if (Target is { } target)
        {
            return target.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
        }

        if (Editable is not { } editable || editable.Length() == 0)
        {
            _view.Deliver(TextInputKeystrokes.ForDeletion(beforeLength, afterLength));
            return true;
        }

        return base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
    }

    /// <inheritdoc />
    public override bool SetSelection(int start, int end) =>
        Target is { } target ? Trace("select", null, start, end) && target.SetSelection(start, end) : base.SetSelection(start, end);

    /// <inheritdoc />
    public override bool PerformContextMenuAction(int id)
    {
        if (Target is not { } target)
        {
            return base.PerformContextMenuAction(id);
        }

        return id switch
        {
            global::Android.Resource.Id.SelectAll => target.Perform(CoreTextInputCommand.SelectAll),
            global::Android.Resource.Id.Cut => target.Perform(CoreTextInputCommand.Cut),
            global::Android.Resource.Id.Copy => target.Perform(CoreTextInputCommand.Copy),
            global::Android.Resource.Id.Paste => target.Perform(CoreTextInputCommand.Paste),
            _ => false,
        };
    }

    /// <inheritdoc />
    public override bool BeginBatchEdit() => Target is { } target ? target.BeginBatchEdit() : base.BeginBatchEdit();

    /// <inheritdoc />
    public override bool EndBatchEdit() => Target is { } target ? target.EndBatchEdit() : base.EndBatchEdit();

    /// <inheritdoc />
    public override bool PerformEditorAction(AImeAction actionCode)
    {
        if (Target is { } target)
        {
            // An editor's action key types a line break where the caret is (the profile is multi-line).
            return target.Commit("\n", 1);
        }

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

    // One debug line per input-method call on the text-target path (logcat, category CodeBrix.Android.UI.Input.TextInput).
    private bool Trace(string call, string text, int a = 0, int b = 0)
    {
        _log.LogDebug("IME {Call} '{Text}' {A} {B}", call, text, a, b);
        return true;
    }
}
