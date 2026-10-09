using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>What a text box handler does about the Android focus of its editor after Core focused the element.</summary>
internal enum NativeFocusClaimAction
{
    /// <summary>Nothing (Core's focus is gone, or the editor already has the Android focus).</summary>
    None,

    /// <summary>Ask Android for the focus now.</summary>
    RequestNow,

    /// <summary>Android refused it (an editor of a page that has just arrived is not laid out at its size yet): ask again after its next layout.</summary>
    RetryAfterLayout,
}

/// <summary>
/// [AP10-G] The rule that keeps a focused TextBox's editor holding the Android focus, so that a hardware keyboard's keys
/// go into it as text. Core can focus a box (a page's first focus after Frame.Navigate, FocusState Pointer or
/// Programmatic) while its editor cannot take the Android focus yet - the page that has just arrived is laid out but its
/// views are still 0 x 0, and Android refuses the focus of a view without a size: the request is repeated after the
/// editor's next layout, for as long as Core's focus stays on the box. The soft keyboard is not part of this rule (the
/// AP9-4 rule: it is asked for by a finger on the box only).
/// </summary>
internal static class NativeFocusClaim
{
    /// <summary>The first decision, when Core's focus state of the box changes.</summary>
    /// <param name="coreState">The box's Core focus state.</param>
    /// <param name="editorHasFocus">True when the editor already holds the Android focus.</param>
    /// <returns><see cref="NativeFocusClaimAction.RequestNow"/> or <see cref="NativeFocusClaimAction.None"/>.</returns>
    internal static NativeFocusClaimAction OnCoreFocus(FocusState coreState, bool editorHasFocus) =>
        coreState == FocusState.Unfocused || editorHasFocus ? NativeFocusClaimAction.None : NativeFocusClaimAction.RequestNow;

    /// <summary>The decision after a request for the Android focus.</summary>
    /// <param name="granted">What the request answered (and the editor then holds the focus).</param>
    /// <returns><see cref="NativeFocusClaimAction.RetryAfterLayout"/> when it was refused.</returns>
    internal static NativeFocusClaimAction AfterRequest(bool granted) =>
        granted ? NativeFocusClaimAction.None : NativeFocusClaimAction.RetryAfterLayout;

    /// <summary>The decision after a layout of the editor while a retry is pending.</summary>
    /// <param name="pending">True when an earlier request was refused.</param>
    /// <param name="coreState">The box's Core focus state now.</param>
    /// <param name="editorHasFocus">True when the editor holds the Android focus now.</param>
    /// <returns><see cref="NativeFocusClaimAction.RequestNow"/> to ask again; <see cref="NativeFocusClaimAction.None"/> to drop the retry.</returns>
    internal static NativeFocusClaimAction OnLayout(bool pending, FocusState coreState, bool editorHasFocus) =>
        pending ? OnCoreFocus(coreState, editorHasFocus) : NativeFocusClaimAction.None;
}
