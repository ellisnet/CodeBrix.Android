// Technique from .NET MAUI, src/Core/src/Platform/Android/Material3Controls/MauiMaterialEditText.cs and
// src/Core/src/Platform/Android/EditTextExtensions.cs @ 828569a864 (a TextInputEditText subclass that
// reports selection changes; the text written with the cursor kept in range). Copyright (c) .NET
// Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using AContext = global::Android.Content.Context;
using ATextInputEditText = Google.Android.Material.TextField.TextInputEditText;

namespace CodeBrix.Android.UI.Platform.Text;

/// <summary>
/// The native editor of a TextBox / PasswordBox (plan 2.14): a Material TextInputEditText that reports
/// selection changes (Android has no selection listener) and context-menu edits (paste, undo, redo) to
/// its handler.
/// </summary>
internal sealed class CodeBrixEditText : ATextInputEditText
{
    /// <summary>Creates the editor.</summary>
    /// <param name="context">A Material 3 context (the TextInputLayout's).</param>
    internal CodeBrixEditText(AContext context)
        : base(context)
    {
    }

    /// <summary>Called when the selection changed (start, end).</summary>
    internal Action<int, int> SelectionChangedCallback { get; set; }

    /// <summary>
    /// Called before a context-menu action (android.R.id.paste, ...) runs; returning true cancels it
    /// (Core's Paste event was handled).
    /// </summary>
    internal Func<int, bool> ContextMenuItemCallback { get; set; }

    /// <summary>Called when the window of the editor gains or loses the focus.</summary>
    internal Action<bool> WindowFocusChangedCallback { get; set; }

    /// <inheritdoc />
    public override void OnWindowFocusChanged(bool hasWindowFocus)
    {
        base.OnWindowFocusChanged(hasWindowFocus);
        WindowFocusChangedCallback?.Invoke(hasWindowFocus);
    }

    /// <inheritdoc />
    public override bool OnTextContextMenuItem(int id)
    {
        if (ContextMenuItemCallback?.Invoke(id) == true)
        {
            return true;
        }

        return base.OnTextContextMenuItem(id);
    }

    /// <inheritdoc />
    protected override void OnSelectionChanged(int selStart, int selEnd)
    {
        base.OnSelectionChanged(selStart, selEnd);
        SelectionChangedCallback?.Invoke(selStart, selEnd);
    }
}
