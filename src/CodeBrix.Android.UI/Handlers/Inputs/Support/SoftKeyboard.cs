using AContext = global::Android.Content.Context;
using AHideSoftInputFlags = global::Android.Views.InputMethods.HideSoftInputFlags;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The soft-keyboard guard of the native text-entry handlers: a handler whose native view is leaving (its element was
/// removed from the tree, its page navigated away) hides the soft keyboard when one of its editors still has the focus.
/// Without it Android keeps the keyboard up for an editor that is no longer shown (the focus moves elsewhere, the
/// input method stays open). TextBoxHandler / PasswordBoxHandler do the same with their own editor.
/// </summary>
internal static class SoftKeyboard
{
    /// <summary>
    /// Hides the soft keyboard when the focused view inside <paramref name="root"/> (or <paramref name="root"/> itself)
    /// is a text editor. Call it from DisconnectHandler BEFORE the editors' listeners are removed.
    /// </summary>
    /// <param name="root">The handler's native view.</param>
    /// <returns>True when a focused editor was found and the keyboard was asked to hide.</returns>
    internal static bool HideIfEditorFocusedWithin(AView root)
    {
        if (root == null || !root.HasFocus || root.FindFocus() is not { } focused || !focused.OnCheckIsTextEditor())
        {
            return false;
        }

        var token = focused.WindowToken ?? root.WindowToken;
        if (token == null || focused.Context?.GetSystemService(AContext.InputMethodService) is not AInputMethodManager ime)
        {
            return false;
        }

        ime.HideSoftInputFromWindow(token, AHideSoftInputFlags.None);
        return true;
    }
}
