using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.TextInput;
using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.Xaml.Controls.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using AContext = global::Android.Content.Context;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;
using AShowFlags = global::Android.Views.InputMethods.ShowFlags;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The Android side of CodeBrix.Platform's SoftwareKeyboardFocus seam (ITextInputFocusNotificationsSingleton): the
/// soft keyboard of the CUSTOM text-entry controls (Core controls that are typed into but are not a TextBox - the
/// TerminalView and AdvancedTextEdit add-ins). Registered by the CodeBrix.Android.UI bootstrap; main thread.
/// <list type="bullet">
/// <item>A control gains focus (any focus: programmatic, keyboard, a page's first focus, a press): the activity's
/// <see cref="CoreTextInputView"/> opens a session with the control's profile and, when its add-in registered one, its
/// text target (<see cref="CoreTextInput"/>), and takes the Android focus (the input method re-reads the editor), so
/// hardware keys and the input connection work at once. The soft keyboard is NOT shown for the focus itself.</item>
/// <item>A control loses focus (or leaves the tree while it has it): the keyboard is hidden and the session closed on the next looper turn - unless another
/// custom text control took the focus in between (no flicker between two of them).</item>
/// <item>[AP9-4] The soft keyboard comes up for a FINGER or PEN tap only - the tap that gives the control its focus
/// (the focus follows the release) or a press on the control that has it (the user dismissed the keyboard and tapped
/// back in); a mouse press does not (<see cref="SoftKeyboardPressRule"/>; the window's root is watched from its first
/// layout, <see cref="WatchRoot(UIElement)"/>). The rule the TextBox handler follows, and WinUI's: no touch keyboard for
/// programmatic or keyboard focus. An app that wants the keyboard without a tap calls InputPane.TryShow
/// (<see cref="TryShowForInputPane"/>).</item>
/// </list>
/// No hardware-keyboard detection of our own: the input method decides whether a requested keyboard appears while a
/// hardware keyboard is attached (the user's "show on-screen keyboard" setting).
/// </summary>
internal sealed class CoreTextInputController : ITextInputFocusNotificationsSingleton
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.TextInput");
    private readonly PointerEventHandler _pressed;
    private readonly PointerEventHandler _released;
    private readonly PointerEventHandler _canceled;
    private readonly RoutedEventHandler _unloaded;
    private readonly SoftKeyboardPressRule _pressRule = new();
    private readonly List<WeakReference<UIElement>> _roots = new();
    private WeakReference<DependencyObject> _lastReleaseSource;
    private Control _focused;
    private CoreTextInputView _view;
    private int _version;

    /// <summary>Creates the controller.</summary>
    internal CoreTextInputController()
    {
        _pressed = OnPointerPressed;
        _released = OnPointerReleased;
        _canceled = OnPointerCanceled;
        _unloaded = OnFocusedControlUnloaded;
    }

    /// <summary>The controller the bootstrap registered (null before; device fences read it).</summary>
    internal static CoreTextInputController Current { get; private set; }

    /// <summary>The control whose session is open (null when none).</summary>
    internal Control FocusedControl => _focused;

    /// <summary>The text-input view of the open session (null when none).</summary>
    internal CoreTextInputView View => _view;

    /// <summary>How many times the controller asked for the soft keyboard (diagnostics and device fences).</summary>
    internal int ShowCount { get; private set; }

    /// <summary>How many soft-keyboard sessions the controller opened (diagnostics and device fences).</summary>
    internal int OpenCount { get; private set; }

    /// <summary>Creates the controller the bootstrap registers.</summary>
    /// <returns>The controller.</returns>
    internal static CoreTextInputController Create() => Current = new CoreTextInputController();

    /// <inheritdoc />
    public void OnTextControlFocused(Control control)
    {
        if (control == null)
        {
            return;
        }

        // [AP8-S batch 3, item L; batch 4] A focused control that LEFT the tree can be handed the focus again by Core after
        // its session closed (an AdvancedTextEdit's text area whose page was replaced: Core's focused element is still the
        // removed text area, and it reports its focus once more, unloaded). No session for a control that is not on the
        // page: it would hold the Android focus and the keyboard for nothing (a later text box's tap then found the window
        // panned to an editor that is gone). "On the page" = loaded, or (not loaded yet) its parents reach the window's
        // content - a control added and focused in the same turn is served. The rule reads the tree itself, so it also
        // holds for a control whose Unloaded this controller never saw (it had no session when it left).
        if (!control.IsLoaded && !IsInLiveTree(control))
        {
            _log.LogDebug("{Control} reported its focus while it is not on the page: no soft-keyboard session.", control.GetType().Name);
            return;
        }

        _version++;
        if (ReferenceEquals(_focused, control) && _view is { Profile: not null, IsFocused: true })
        {
            // The same control focused again (a click on it, Focus() with another FocusState): the session is open; the
            // keyboard is not summoned - a dismissal stands until a finger or pen presses the control (OnPointerPressed /
            // OnPointerReleased), as the Platform's software keyboard honours it.
            return;
        }

        if (!ReferenceEquals(_focused, control))
        {
            Detach(_focused);
            _focused = control;
            control.AddHandler(UIElement.PointerPressedEvent, _pressed, handledEventsToo: true);
            control.AddHandler(UIElement.PointerReleasedEvent, _released, handledEventsToo: true);
            control.AddHandler(UIElement.PointerCanceledEvent, _canceled, handledEventsToo: true);
            control.Unloaded += _unloaded;
        }

        // The press that gives the control its focus may be routed to the control before Core moves the focus: the
        // window's root sees the rest of that press (and its release) after the control (bubbling, handled too).
        WatchRoot(control);

        var host = control.XamlRoot == null ? null : XamlRootMap.GetHostForRoot(control.XamlRoot) as AndroidXamlRootHost;
        var activity = host?.Wrapper?.Activity ?? ActivityRegistry.Current;
        var source = host?.KeyboardSource;
        var view = CoreTextInputView.For(activity);
        if (view == null || source == null)
        {
            _log.LogDebug("No text-input view or keyboard source for {Control}: the soft keyboard is not shown.", control.GetType().Name);
            return;
        }

        if (_view != null && !ReferenceEquals(_view, view))
        {
            _view.Close();
        }

        _view = view;
        var profile = CoreTextInput.ProfileOf(control);
        view.Open(profile, source, CoreTextInput.CreateTarget(control), CoreTextInput.CreateCaret(control));
        if (!view.IsFocused)
        {
            view.RequestFocus();
        }

        // [AP9-4] The session is open and holds the Android focus; the keyboard is NOT summoned for the focus (WinUI shows
        // no touch keyboard for programmatic or keyboard focus): a finger or pen press on the control summons it.
        Ime(view)?.RestartInput(view);
        OpenCount++;

        // The focus a finger or pen tap gives the control (a TerminalControl focuses itself on Tapped, right after the
        // release) summons the keyboard; any other focus does not.
        if (_pressRule.OnFocused(Environment.TickCount64, IsInside(_lastReleaseSource, control)))
        {
            ShowForPress();
        }

        _log.LogDebug("Text input opened for {Control} (profile {Profile}, {Path}).", control.GetType().Name, profile,
            view.TargetEditor != null ? "text target" : "key presses");
    }

    /// <inheritdoc />
    public void OnTextControlUnfocused(Control control)
    {
        if (control == null || !ReferenceEquals(_focused, control))
        {
            return;
        }

        Detach(control);
        _focused = null;
        var version = _version;
        var view = _view;
        if (view == null)
        {
            return;
        }

        // Next looper turn: a focus move to another custom text control arrives as unfocus + focus, and must not hide
        // and re-show the keyboard.
        view.Post(() =>
        {
            if (_version != version || _focused != null)
            {
                return;
            }

            Close(view);
        });
    }

    private void Close(CoreTextInputView view)
    {
        view.Close();
        if (ReferenceEquals(_view, view))
        {
            _view = null;
        }

        if (Ime(view) is { } ime)
        {
            if (view.WindowToken != null)
            {
                ime.HideSoftInputFromWindow(view.WindowToken, 0);
            }

            ime.RestartInput(view);
        }

        // The Android focus goes back to the root layout (where Core keeps it for every other element).
        if (view.IsFocused && view.Context is CodeBrixActivity activity)
        {
            activity.RootLayout?.RequestFocus();
        }

        _log.LogDebug("Text input closed.");
    }

    // The control's parents reach its window's content (the live visual tree).
    private static bool IsInLiveTree(Control control)
    {
        var content = control.XamlRoot?.Content;
        if (content == null)
        {
            return false;
        }

        DependencyObject current = control;
        while (current != null)
        {
            if (ReferenceEquals(current, content))
            {
                return true;
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Detach(Control control)
    {
        if (control != null)
        {
            control.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
            control.RemoveHandler(UIElement.PointerReleasedEvent, _released);
            control.RemoveHandler(UIElement.PointerCanceledEvent, _canceled);
            control.Unloaded -= _unloaded;
        }
    }

    // A focused control that leaves the tree (a page navigates, a console tab closes) does not always report losing the
    // focus: its session ends and the keyboard goes, as when it loses the focus.
    private void OnFocusedControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            OnTextControlUnfocused(control);
        }
    }

    // The window's root element (the top of the control's visual tree) gets the press and release handlers once.
    private void WatchRoot(Control control)
    {
        UIElement root = control;
        for (var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(root); parent != null; parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent))
        {
            if (parent is UIElement element)
            {
                root = element;
            }
        }

        if (!ReferenceEquals(root, control))
        {
            WatchRoot(root);
        }
    }

    /// <summary>
    /// Watches a window's root element for finger and pen presses and releases (handled events too) from the moment the
    /// window shows: the tap that gives a custom text control its FIRST focus is seen too. Called by the window's host
    /// whenever it attaches its root view; once per root.
    /// </summary>
    /// <param name="root">The window's root element.</param>
    internal void WatchRoot(UIElement root)
    {
        if (root == null)
        {
            return;
        }

        for (var index = _roots.Count - 1; index >= 0; index--)
        {
            if (!_roots[index].TryGetTarget(out var known))
            {
                _roots.RemoveAt(index);
            }
            else if (ReferenceEquals(known, root))
            {
                return;
            }
        }

        root.AddHandler(UIElement.PointerPressedEvent, _pressed, handledEventsToo: true);
        root.AddHandler(UIElement.PointerReleasedEvent, _released, handledEventsToo: true);
        root.AddHandler(UIElement.PointerCanceledEvent, _canceled, handledEventsToo: true);
        _roots.Add(new WeakReference<UIElement>(root));
    }

    // True when the element the reference holds is the control or inside it.
    private static bool IsInside(WeakReference<DependencyObject> reference, Control control)
    {
        if (reference == null || !reference.TryGetTarget(out var element))
        {
            return false;
        }

        for (var current = element; current != null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, control))
            {
                return true;
            }
        }

        return false;
    }

    // True when the routed event's source is the focused control or inside it.
    private bool OnFocusedControl(PointerRoutedEventArgs e)
    {
        var focused = _focused;
        if (focused == null)
        {
            return false;
        }

        for (var current = e?.OriginalSource as DependencyObject; current != null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, focused))
            {
                return true;
            }
        }

        return false;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e?.Pointer == null)
        {
            return;
        }

        var frame = e.GetCurrentPoint(null)?.FrameId ?? 0;
        if (_pressRule.OnPressed(e.Pointer.PointerId, frame, e.Pointer.PointerDeviceType, ReferenceEquals(sender, _focused) || OnFocusedControl(e)))
        {
            ShowForPress();
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e?.Pointer == null)
        {
            return;
        }

        var frame = e.GetCurrentPoint(null)?.FrameId ?? 0;
        if (e.OriginalSource is DependencyObject source)
        {
            _lastReleaseSource = new WeakReference<DependencyObject>(source);
        }

        if (_pressRule.OnReleased(e.Pointer.PointerId, frame, e.Pointer.PointerDeviceType, ReferenceEquals(sender, _focused) || OnFocusedControl(e), Environment.TickCount64))
        {
            ShowForPress();
        }
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (e?.Pointer != null)
        {
            _pressRule.OnCanceled(e.Pointer.PointerId);
        }
    }

    // A finger or pen pressed the focused control: the soft keyboard is asked for.
    private void ShowForPress()
    {
        if (_view is not { Profile: not null } view)
        {
            return;
        }

        if (!view.IsFocused)
        {
            view.RequestFocus();
        }

        Ime(view)?.ShowSoftInput(view, AShowFlags.Implicit);
        ShowCount++;
    }

    /// <summary>
    /// InputPane.TryShow while a custom text control has the focus: the soft keyboard is asked for on the session's
    /// text-input view (the app's explicit request: no press needed).
    /// </summary>
    /// <returns>Null when no custom text control has an open session (the caller falls back to the window's focused
    /// view); otherwise what the input method answered.</returns>
    internal bool? TryShowForInputPane()
    {
        if (_focused == null || _view is not { Profile: not null } view)
        {
            return null;
        }

        if (!view.IsFocused)
        {
            view.RequestFocus();
        }

        if (Ime(view) is not { } ime)
        {
            return false;
        }

        ShowCount++;
        return ime.ShowSoftInput(view, AShowFlags.Implicit);
    }

    private static AInputMethodManager Ime(CoreTextInputView view) =>
        view.Context?.GetSystemService(AContext.InputMethodService) as AInputMethodManager;
}
