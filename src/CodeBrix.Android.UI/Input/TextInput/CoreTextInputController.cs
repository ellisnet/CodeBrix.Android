using CodeBrix.Android.UI.Hosting;
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
/// <item>A control gains focus: the activity's <see cref="CoreTextInputView"/> opens a session with the control's
/// profile and, when its add-in registered one, its text target (<see cref="CoreTextInput"/>), takes the Android focus
/// (the input method re-reads the editor) and the keyboard is shown.</item>
/// <item>A control loses focus (or leaves the tree while it has it): the keyboard is hidden and the session closed on the next looper turn - unless another
/// custom text control took the focus in between (no flicker between two of them).</item>
/// <item>A finger or pen pressed on the focused control shows the keyboard again (the user dismissed it and tapped
/// back in); a mouse press does not (a docked or desktop pointer has a hardware keyboard).</item>
/// </list>
/// With a hardware keyboard attached, Android itself keeps the soft keyboard hidden (the system setting decides).
/// </summary>
internal sealed class CoreTextInputController : ITextInputFocusNotificationsSingleton
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.TextInput");
    private readonly PointerEventHandler _pressed;
    private readonly RoutedEventHandler _unloaded;
    private Control _focused;
    private CoreTextInputView _view;
    private int _version;

    /// <summary>Creates the controller.</summary>
    internal CoreTextInputController()
    {
        _pressed = OnFocusedControlPressed;
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
            // keyboard is not summoned again - a dismissal stands until a finger or pen presses the control
            // (OnFocusedControlPressed), as the Platform's software keyboard honours it.
            return;
        }

        if (!ReferenceEquals(_focused, control))
        {
            Detach(_focused);
            _focused = control;
            control.AddHandler(UIElement.PointerPressedEvent, _pressed, handledEventsToo: true);
            control.Unloaded += _unloaded;
        }

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

        if (Ime(view) is { } ime)
        {
            ime.RestartInput(view);
            ime.ShowSoftInput(view, AShowFlags.Implicit);
            ShowCount++;
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

    private void OnFocusedControlPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e?.Pointer?.PointerDeviceType == PointerDeviceType.Mouse || _view is not { Profile: not null } view)
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

    private static AInputMethodManager Ime(CoreTextInputView view) =>
        view.Context?.GetSystemService(AContext.InputMethodService) as AInputMethodManager;
}
