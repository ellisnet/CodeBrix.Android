namespace CodeBrix.Android.UI.Handlers;

/// <summary>What a text box handler does with an Android editor action (TextView.OnEditorActionListener).</summary>
internal enum EditorActionResponse
{
    /// <summary>Not the handler's: Android's default handling applies.</summary>
    Ignore,

    /// <summary>Raise Enter in Core (KeyDown / KeyUp Enter); the action counts as handled when Core handled the key.</summary>
    RaiseEnter,

    /// <summary>
    /// Raise Enter in Core and always consume the action: Android must not move the focus to the next field even when
    /// the app left the key unhandled (the desktop heads never move the focus on Enter).
    /// </summary>
    RaiseEnterAndConsume,

    /// <summary>Consume without raising anything (the key-up of an Enter whose key-down was raised).</summary>
    ConsumeOnly,
}

/// <summary>
/// The pure decision behind a text box's editor action. The soft keyboard's action key arrives with the editor's IME
/// action (Done, Search, ...) and is the Enter key of WinUI apps. A key-event Enter (a hardware keyboard, an IME that
/// sends key events) on a single-line editor arrives before API 36 as IME_NULL with the key event (API 36 passes the IME
/// action instead); left unhandled, TextView moves the focus to the next field on the key-up. On a single-line box it is
/// taken as the action and consumed, down and up, so Enter reaches Core as a key event and the focus stays - as on the
/// desktop heads. A multi-line box (AcceptsReturn) keeps Android's handling (the editor inserts the line break).
/// </summary>
internal static class EditorActionPolicy
{
    /// <summary>Decides what to do with an editor action.</summary>
    /// <param name="isImeNull">The action id is IME_NULL (EditorInfo.IME_NULL).</param>
    /// <param name="hasEnterKeyEvent">The action carries a key event for the Enter key.</param>
    /// <param name="isKeyDown">That key event is a key-down.</param>
    /// <param name="acceptsReturn">The TextBox accepts returns (multi-line).</param>
    /// <returns>The response.</returns>
    internal static EditorActionResponse Decide(bool isImeNull, bool hasEnterKeyEvent, bool isKeyDown, bool acceptsReturn)
    {
        if (!isImeNull)
        {
            return EditorActionResponse.RaiseEnter;
        }

        if (!hasEnterKeyEvent || acceptsReturn)
        {
            return EditorActionResponse.Ignore;
        }

        return isKeyDown ? EditorActionResponse.RaiseEnterAndConsume : EditorActionResponse.ConsumeOnly;
    }
}
