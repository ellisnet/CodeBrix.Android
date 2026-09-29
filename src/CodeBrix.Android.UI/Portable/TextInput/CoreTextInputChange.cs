namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>What changed in a custom text-entry control's text (<see cref="ICoreTextInputTarget.Changed"/>).</summary>
internal enum CoreTextInputChange
{
    /// <summary>The selection or the caret moved.</summary>
    Selection,

    /// <summary>Part of the text changed.</summary>
    Text,

    /// <summary>The whole text was replaced (a new document): an input method must read it again.</summary>
    Reset,
}
