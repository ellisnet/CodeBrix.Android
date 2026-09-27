using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Hosting;
using AEditText = global::Android.Widget.EditText;
using AKeyEvent = global::Android.Views.KeyEvent;
using AKeyEventActions = global::Android.Views.KeyEventActions;
using AKeycode = global::Android.Views.Keycode;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The input hook of a <see cref="CodeBrixActivity"/> (plan 2.15): key events go to the window's keyboard
/// source before the focused widget - except while a native text editor (a TextBox, PasswordBox, NumberBox or
/// AutoSuggestBox field) has the Android focus: then the editor gets the key first and Core only what the editor
/// did not use (the native-head order: caret keys move the caret natively, Enter reaches the editor's action,
/// and a key the editor leaves - Tab, Escape, an accelerator, an arrow at the text's end - still reaches Core);
/// touch, mouse, stylus, hover and wheel events go to the window's
/// pointer source AFTER Android dispatched them to the native views, so a native widget can mark an event
/// handled (<see cref="NativeInput.MarkHandled"/>) and a native scroll view can take a pointer over
/// (<see cref="NativeInput.CancelPointer"/>) first.
/// </summary>
internal sealed class ActivityInputRouter : IActivityInputHook
{
    private readonly CodeBrixActivity _activity;
    private readonly int[] _location = new int[2];
    private readonly int[] _decorLocation = new int[2];
    private readonly HashSet<uint> _cancelled = new();
    private readonly HashSet<AKeycode> _editorKeys = new();

    /// <summary>Creates the router of an activity.</summary>
    internal ActivityInputRouter(CodeBrixActivity activity) => _activity = activity;

    /// <inheritdoc />
    public bool OnKeyEvent(AKeyEvent e, out bool viewsSawKey)
    {
        viewsSawKey = false;
        if (FocusedNativeEditor() != null && !e.IsSystem)
        {
            viewsSawKey = true;
            if (_activity.DispatchKeyEventToViews(e))
            {
                if (e.Action == AKeyEventActions.Down)
                {
                    _editorKeys.Add(e.KeyCode);
                }
                else if (e.Action == AKeyEventActions.Up)
                {
                    _editorKeys.Remove(e.KeyCode);
                }

                return true;
            }

            // The key-up of a key whose key-down the editor used is the editor's too (Core never saw the down).
            if (e.Action == AKeyEventActions.Up && _editorKeys.Remove(e.KeyCode))
            {
                return true;
            }
        }
        else if (e.Action == AKeyEventActions.Up && _editorKeys.Remove(e.KeyCode))
        {
            // The focus left the editor between the key's down and up: the up goes where the down went.
            viewsSawKey = true;
            return _activity.DispatchKeyEventToViews(e);
        }

        return Host()?.KeyboardSource?.OnNativeKeyEvent(e) == true;
    }

    /// <summary>
    /// The native text editor that has the Android focus inside this activity's root layout, or null: an EditText, or a
    /// web page's view (android.webkit.WebView - the WebView add-in; the page's own inputs have the keys, as a focused
    /// WebView2 has them on every CodeBrix.Platform head).
    /// </summary>
    private global::Android.Views.View FocusedNativeEditor()
    {
        if (_activity.CurrentFocus is not { IsFocused: true, Enabled: true } editor
            || editor is not (AEditText or global::Android.Webkit.WebView))
        {
            return null;
        }

        for (var parent = editor.Parent; parent != null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, _activity.RootLayout))
            {
                return editor;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool OnGenericMotionEvent(AMotionEvent e)
    {
        // Hover and wheel: Core first (the native views do not use them; native scroll views follow
        // Core's offsets, so the wheel is Core's).
        if (Source() is not { } source)
        {
            return false;
        }

        var (x, y, density) = Origin();
        source.OnNativeMotionEvent(e, x, y, density, nativelyHandled: false, _cancelled);
        return (e.Action & AMotionEventActions.Mask) == AMotionEventActions.Scroll;
    }

    /// <inheritdoc />
    public void BeginTouchEvent(AMotionEvent e)
    {
        NativeInput.Begin(this, e);
        if ((e.Action & AMotionEventActions.Mask) == AMotionEventActions.Down)
        {
            _cancelled.Clear();
        }
    }

    /// <inheritdoc />
    public void EndTouchEvent(AMotionEvent e)
    {
        var handled = NativeInput.End(this);
        if (Source() is not { } source)
        {
            return;
        }

        var (x, y, density) = Origin();
        source.OnNativeMotionEvent(e, x, y, density, handled, _cancelled);
    }

    /// <summary>A native view took over pointer <paramref name="pointerIndex"/> of the event being dispatched.</summary>
    internal void Cancel(AMotionEvent e, int pointerIndex)
    {
        var id = PointerHelpers.GetPointerId(e, pointerIndex);
        if (!_cancelled.Add(id) || Source() is not { } source)
        {
            return;
        }

        var (x, y, density) = Origin();
        source.CancelPointer(e, pointerIndex, x, y, density);
    }

    /// <summary>True when Core captured the pointer of <paramref name="pointerIndex"/>.</summary>
    internal bool IsCaptured(AMotionEvent e, int pointerIndex) => Source()?.IsCaptured(PointerHelpers.GetPointerId(e, pointerIndex)) == true;

    private AndroidXamlRootHost Host()
    {
        var root = _activity.WindowWrapper?.XamlRoot;
        return root == null ? null : XamlRootMap.GetHostForRoot(root) as AndroidXamlRootHost;
    }

    private AndroidCorePointerInputSource Source() => Host()?.PointerSource;

    private (float X, float Y, double Density) Origin()
    {
        // The XamlRoot's content is the root layout's content layer. The activity receives MotionEvents in its DECOR
        // VIEW's coordinates, so the origin is the layer's position relative to the decor view (AP7-B TerminalView, FIXLIST
        // [AP7-B TerminalView]): with the soft keyboard up on a window that shows the status bar, Android lays the decor
        // view out BELOW the status bar inside the window, and a plain window location was off by that offset - every touch
        // landed a status bar's height above the finger while the keyboard was up.
        var layer = _activity.RootLayout?.ContentLayer;
        if (layer != null)
        {
            layer.GetLocationInWindow(_location);
            if (_activity.Window?.DecorView is { } decor)
            {
                decor.GetLocationInWindow(_decorLocation);
                _location[0] -= _decorLocation[0];
                _location[1] -= _decorLocation[1];
            }
        }
        else
        {
            _location[0] = _location[1] = 0;
        }

        var density = _activity.WindowWrapper?.XamlRoot?.RasterizationScale ?? _activity.Resources.DisplayMetrics.Density;
        return (_location[0], _location[1], density > 0 ? density : 1);
    }
}

/// <summary>
/// What native views tell the input router while Android dispatches a touch event to them (UI thread,
/// inside the activity's DispatchTouchEvent): the event was handled natively (a native widget acted on
/// it: Core still sees it, with Handled set), or a pointer was taken over (a native scroll view started
/// dragging: Core gets a cancel and no more of that pointer's events).
/// </summary>
internal static class NativeInput
{
    private static ActivityInputRouter _router;
    private static AMotionEvent _event;
    private static bool _handled;

    /// <summary>Marks the event being dispatched as handled by a native widget.</summary>
    internal static void MarkHandled() => _handled = true;

    /// <summary>A native view takes the gesture of <paramref name="pointerIndex"/> of the event being dispatched over.</summary>
    internal static void CancelPointer(AMotionEvent e, int pointerIndex) => _router?.Cancel(e ?? _event, pointerIndex);

    /// <summary>True when Core captured the pointer (native views must not intercept it).</summary>
    internal static bool IsCapturedByCore(AMotionEvent e, int pointerIndex) => _router?.IsCaptured(e, pointerIndex) == true;

    internal static void Begin(ActivityInputRouter router, AMotionEvent e)
    {
        _router = router;
        _event = e;
        _handled = false;
    }

    internal static bool End(ActivityInputRouter router)
    {
        var handled = _handled;
        _handled = false;
        _event = null;
        if (ReferenceEquals(_router, router))
        {
            _router = null;
        }

        return handled;
    }
}
