namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// How the soft keyboard behaves for one kind of CUSTOM text-entry control (a Core control that reports its focus
/// through CodeBrix.Platform's SoftwareKeyboardFocus seam). An add-in registers the profile of its control
/// (Input/TextInput/CoreTextInput.RegisterProfile); a control with no registered profile gets <see cref="Default"/>.
/// Pure data: the Android input connection maps it onto the EditorInfo it gives the input method.
/// </summary>
internal sealed class CoreTextInputProfile
{
    /// <summary>A plain text editor: suggestions and composition on, Enter types a line break.</summary>
    internal static readonly CoreTextInputProfile Default = new("text", suggestions: true, multiLine: true);

    /// <summary>
    /// A document editor (a code or text editor control): suggestions and composition on, Enter types a line break. The
    /// same keyboard as <see cref="Default"/>, named for the editors that register it together with a text target
    /// (Input/TextInput/CoreTextInput.RegisterTarget), so the keyboard sees and edits the document itself.
    /// </summary>
    internal static readonly CoreTextInputProfile Editor = new("editor", suggestions: true, multiLine: true);

    /// <summary>
    /// A terminal: every key the user presses reaches the control at once (no suggestions, no autocorrection, no
    /// composition held back in the keyboard; the keyboard shows its "visible password" layout, which has the digits
    /// and symbols a shell needs), Enter is a key, and the keyboard never goes full screen.
    /// </summary>
    internal static readonly CoreTextInputProfile Terminal = new("terminal", suggestions: false, multiLine: false);

    /// <summary>Creates a profile.</summary>
    /// <param name="name">A name for diagnostics.</param>
    /// <param name="suggestions">True: the keyboard may suggest, correct and compose words.</param>
    /// <param name="multiLine">True: Enter types a line break (a multi-line editor); false: Enter is a key.</param>
    internal CoreTextInputProfile(string name, bool suggestions, bool multiLine)
    {
        Name = name;
        Suggestions = suggestions;
        MultiLine = multiLine;
    }

    /// <summary>A name for diagnostics.</summary>
    internal string Name { get; }

    /// <summary>True: the keyboard may suggest, correct and compose words (text reaches the control when committed).</summary>
    internal bool Suggestions { get; }

    /// <summary>True: Enter types a line break; false: Enter is a key press.</summary>
    internal bool MultiLine { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
