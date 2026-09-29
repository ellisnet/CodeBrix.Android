using System;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// [AP8-S item L] The CARET of a custom text-entry control (a Core control reporting its focus through CodeBrix.Platform's
/// SoftwareKeyboardFocus seam), as the soft-keyboard session needs it: where the caret is on the control, and when it
/// moved. While the session is open the activity's <see cref="CoreTextInputView"/> - the view that holds the Android
/// focus for the control - is laid out on the caret, so Android's adjustPan (the default soft-input mode) brings the caret
/// above the keyboard as it does a native text field's caret line. An add-in registers a factory of carets for its control
/// (<see cref="CoreTextInput.RegisterCaret"/>) next to its profile; a control without one keeps the parked 1x1 view (the
/// window does not pan for it; SoftInputAdjust.Resize lays such a control out above the keyboard instead).
/// </summary>
/// <remarks>UI thread. A caret is made when a session opens on its control and disposed when the session ends.</remarks>
internal interface ICoreTextInputCaret : IDisposable
{
    /// <summary>The element the caret bounds are measured on (their coordinate space; its box also clips the caret).</summary>
    FrameworkElement Element { get; }

    /// <summary>Raised when the caret moved on the element: typed text, a caret move, a scroll, a re-layout.</summary>
    event EventHandler Moved;

    /// <summary>The caret rectangle in <see cref="Element"/>'s coordinates (DIPs), when the control shows one.</summary>
    /// <param name="bounds">The caret (a thin caret may be 0 wide).</param>
    /// <returns>False when the control has no caret to show now (no document, not laid out).</returns>
    bool TryGetBounds(out Rect bounds);
}
