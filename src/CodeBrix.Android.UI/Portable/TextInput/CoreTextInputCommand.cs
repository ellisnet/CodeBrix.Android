namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>The commands an input method's toolbar asks a text-entry control for (performContextMenuAction).</summary>
internal enum CoreTextInputCommand
{
    /// <summary>Select the whole text.</summary>
    SelectAll,

    /// <summary>Cut the selection to the clipboard.</summary>
    Cut,

    /// <summary>Copy the selection to the clipboard.</summary>
    Copy,

    /// <summary>Paste the clipboard at the selection.</summary>
    Paste,
}
