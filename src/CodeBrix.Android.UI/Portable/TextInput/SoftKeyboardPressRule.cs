using Microsoft.UI.Input;

namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// When a CUSTOM text-entry control (a Core control that reports its focus through CodeBrix.Platform's
/// SoftwareKeyboardFocus seam: the TerminalView and AdvancedTextEdit add-ins) summons the soft keyboard - the rule a
/// TextBox follows, and WinUI's: the keyboard comes up for a FINGER or PEN press on the control, never for focus alone
/// (programmatic focus, keyboard focus, the first focus Core gives a page) and never for a mouse click. A control that
/// takes the focus any other way still takes it (key presses reach it at once); the user's tap, or the app's
/// InputPane.TryShow, brings the keyboard.
/// <para>
/// The press can land on a control that already has the focus (a tap after the keyboard was dismissed): the keyboard is
/// asked for on the press. Or it gives the control its focus (the first tap): a TerminalControl takes the focus on its
/// Tapped event, right AFTER the release is routed, with FocusState.Pointer - the same state its GrabFocus() gives, so
/// the focus state cannot tell a tap from an application's call. The rule therefore remembers the last finger or pen
/// release and shows the keyboard for a focus that follows it within <see cref="FocusAfterReleaseMilliseconds"/> when
/// that release landed on the control now focused (once per release). The rule follows one pointer from its press to
/// its release and answers "show" at most once for it. Pure state: the
/// Android controller feeds it the routed Pointer events it sees (on the control and at the window's root - the same
/// event can arrive twice) and asks the input method for the keyboard when it answers true.
/// </para>
/// </summary>
internal sealed class SoftKeyboardPressRule
{
    private uint? _pointerId;
    private ulong _frameId;
    private bool _shown;
    private uint? _releasedPointerId;
    private ulong _releasedFrameId;

    /// <summary>True for the pointers whose press summons the soft keyboard: a finger (touch) or a pen.</summary>
    /// <param name="type">The pointer's device type.</param>
    /// <returns>True for touch and pen; false for a mouse.</returns>
    internal static bool SummonsKeyboard(PointerDeviceType type) => type is PointerDeviceType.Touch or PointerDeviceType.Pen;

    /// <summary>How soon after a finger or pen release on a control its focus counts as given by that tap (Tapped is
    /// raised right after the release, in the same input dispatch).</summary>
    internal const long FocusAfterReleaseMilliseconds = 500;

    private bool _releaseArmed;
    private long _releaseTime;

    /// <summary>
    /// A custom text control gained the focus: the keyboard is asked for only when that focus follows (within
    /// <see cref="FocusAfterReleaseMilliseconds"/>) a finger or pen release that landed on the control, and that release
    /// has not been used yet. Programmatic, keyboard or first-of-page focus never summons it.
    /// </summary>
    /// <param name="now">The current time, in milliseconds (any monotonic clock the releases use too).</param>
    /// <param name="lastReleaseOnControl">True when the last finger or pen release landed on the control now focused.</param>
    /// <returns>True when the keyboard must be asked for now.</returns>
    internal bool OnFocused(long now, bool lastReleaseOnControl)
    {
        var show = _releaseArmed && lastReleaseOnControl && now >= _releaseTime && now - _releaseTime <= FocusAfterReleaseMilliseconds;
        if (show)
        {
            _releaseArmed = false;
        }

        return show;
    }

    /// <summary>A pointer pressed (routed by Core).</summary>
    /// <param name="pointerId">The pointer's id.</param>
    /// <param name="frameId">The press's frame id (the same event seen twice has the same one).</param>
    /// <param name="type">The pointer's device type.</param>
    /// <param name="onFocusedControl">True when the press landed on the custom text control that has the focus now.</param>
    /// <returns>True when the keyboard must be asked for now.</returns>
    internal bool OnPressed(uint pointerId, ulong frameId, PointerDeviceType type, bool onFocusedControl)
    {
        if (_pointerId == pointerId && _frameId == frameId)
        {
            // The same press seen again (on the control, then at the root): the control may have taken the focus in between.
            if (_shown || !onFocusedControl || !SummonsKeyboard(type))
            {
                return false;
            }

            _shown = true;
            return true;
        }

        if (!SummonsKeyboard(type))
        {
            _pointerId = null;
            return false;
        }

        _pointerId = pointerId;
        _frameId = frameId;
        _shown = onFocusedControl;
        return _shown;
    }

    /// <summary>A pointer released (routed by Core).</summary>
    /// <param name="pointerId">The pointer's id.</param>
    /// <param name="frameId">The release's frame id (the same event seen twice has the same one).</param>
    /// <param name="type">The pointer's device type.</param>
    /// <param name="onFocusedControl">True when the release is on the custom text control that has the focus now.</param>
    /// <param name="now">The current time, in milliseconds (the clock <see cref="OnFocused"/> is given).</param>
    /// <returns>True when the keyboard must be asked for now (the press gave the control its focus while it was routed).</returns>
    internal bool OnReleased(uint pointerId, ulong frameId, PointerDeviceType type, bool onFocusedControl, long now)
    {
        if (_releasedPointerId == pointerId && _releasedFrameId == frameId)
        {
            // The same release seen again (on the control, then at the root).
            return false;
        }

        _releasedPointerId = pointerId;
        _releasedFrameId = frameId;
        var shownForPress = _pointerId == pointerId && _shown;

        // A focus that follows this release (Tapped) may still summon the keyboard - unless this press already did.
        _releaseArmed = SummonsKeyboard(type) && !shownForPress;
        _releaseTime = now;
        if (_pointerId != pointerId)
        {
            return false;
        }

        var show = !_shown && onFocusedControl;
        if (show)
        {
            _releaseArmed = false;
        }

        _pointerId = null;
        _shown = false;
        return show;
    }

    /// <summary>A pointer's gesture was cancelled (a native view took it over, capture lost): no keyboard for it.</summary>
    /// <param name="pointerId">The pointer's id.</param>
    internal void OnCanceled(uint pointerId)
    {
        if (_pointerId == pointerId)
        {
            _pointerId = null;
            _shown = false;
        }
    }
}
