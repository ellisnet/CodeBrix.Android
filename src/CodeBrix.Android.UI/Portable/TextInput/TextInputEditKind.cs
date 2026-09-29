namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>How <see cref="TextInputTargetEditor"/> applies an input method's replacement of a range.</summary>
internal enum TextInputEditKind
{
    /// <summary>Nothing changes (the range already holds the text, or an empty range gets empty text).</summary>
    None,

    /// <summary>The range IS the selection: the text is typed there, through the control's typing path.</summary>
    Typed,

    /// <summary>The range ends at the caret and the new text only adds to what it held (a composition that grows letter
    /// by letter): the added part is typed at the caret, through the control's typing path.</summary>
    Appended,

    /// <summary>Anything else (an autocorrection, a deletion, a composition that changed a letter): the range is
    /// replaced as it is.</summary>
    Replaced,
}
