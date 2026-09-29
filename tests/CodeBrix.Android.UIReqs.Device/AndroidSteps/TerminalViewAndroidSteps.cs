#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.TerminalView.Android;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.TerminalView;
using CodeBrix.Platform.UI.TerminalView.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using SkiaSharp;
using Xunit;
using AContext = Android.Content.Context;
using AEditorInfo = Android.Views.InputMethods.EditorInfo;
using AHideSoftInputFlags = Android.Views.InputMethods.HideSoftInputFlags;
using AImeAction = Android.Views.InputMethods.ImeAction;
using AImeFlags = Android.Views.InputMethods.ImeFlags;
using AInputMethodManager = Android.Views.InputMethods.InputMethodManager;
using AInputSourceType = Android.Views.InputSourceType;
using AInputTypes = Android.Text.InputTypes;
using AKeyEvent = Android.Views.KeyEvent;
using AKeyEventActions = Android.Views.KeyEventActions;
using AKeycode = Android.Views.Keycode;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventButtonState = Android.Views.MotionEventButtonState;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using ASystemClock = Android.OS.SystemClock;
using AWindowInsets = Android.Views.WindowInsets;
using JavaString = Java.Lang.String;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The TerminalView on Android" feature (AndroidFeatures/AndroidTerminal): the TerminalView
/// add-in's Android canvas supply, and the soft-keyboard session of a custom text-entry control (CodeBrix.Android.UI's
/// Input/TextInput: the controller behind SoftwareKeyboardFocus, the text-input view, its input connection) - the
/// connection is driven as an input method drives it; fingers and the mouse are REAL MotionEvents dispatched to the
/// activity.
/// </summary>
[Binding]
public sealed class TerminalViewAndroidSteps
{
    private const string TerminalFont = "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf";
    private static readonly Dictionary<string, StringBuilder> Hosts = new();
    private static int _paintsBefore;

    /// <summary>Shows an 800 x 600 terminal (Navy ground, Yellow text, 24 DIPs) that records what it sends its host.</summary>
    [Given("the application shows a TerminalView named {string} that records its host traffic")]
    public Task Given_a_terminal(string name) => ShowAsync(name, null);

    /// <summary>The same terminal beside a Button.</summary>
    [Given("the application shows a TerminalView named {string} that records its host traffic, beside a Button named {string}")]
    public Task Given_a_terminal_and_a_button(string name, string button) => ShowAsync(name, button);

    /// <summary>The terminal's drawing surface is the add-in's Canvas, shown by its handler as a native Skia view.</summary>
    [Then("the drawing surface of the TerminalView {string} is the Android terminal canvas")]
    public async Task Then_the_surface_is_the_Android_canvas(string name)
    {
        (string surface, string handler, string view) = await OnUIThreadAsync(() =>
        {
            var canvas = Surface(name);
            var handler = canvas == null ? null : PolicyDiagnostics.HandlerOf(canvas);
            return ((string)canvas?.GetType().Name, (string)handler?.GetType().Name, (handler as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView?.GetType().Name);
        }).ConfigureAwait(false);
        surface.Should().Be(nameof(TerminalCanvasElement));
        handler.Should().Be(nameof(TerminalCanvasHandler));
        view.Should().Be("SkiaCanvasView");
    }

    /// <summary>Feeds VT output to the terminal.</summary>
    [When("the TerminalView {string} is fed {string}")]
    public async Task When_fed(string name, string text)
    {
        _paintsBefore = await OnUIThreadAsync(() => Surface(name).PaintCount).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Terminal(name).Feed(text)).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>The surface painted after the output was fed (the canvas supply's invalidation reached the native view).</summary>
    [Then("the drawing surface of the TerminalView {string} paints again")]
    public async Task Then_paints_again(string name)
    {
        var paints = 0;
        for (var attempt = 0; attempt < 30 && paints <= _paintsBefore; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            paints = await OnUIThreadAsync(() => Surface(name).PaintCount).ConfigureAwait(false);
        }

        paints.Should().BeGreaterThan(_paintsBefore, "the terminal's surface must paint again after output is fed");
    }

    /// <summary>Gives the terminal the focus the way an application does (TerminalControl.GrabFocus).</summary>
    [When("the TerminalView {string} takes the focus")]
    public async Task When_takes_focus(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Terminal(name).GrabFocus()).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Moves the focus to a Button.</summary>
    [When("the Button {string} takes the focus")]
    public async Task When_button_takes_focus(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((Button)ElementRegistry.Resolve(name)).Focus(FocusState.Programmatic)).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The controller's session is open for the terminal: its text-input view holds the Android focus and is an editor.</summary>
    [Then("the soft-keyboard session is open for {string} with the {string} profile")]
    public async Task Then_session_open(string name, string profile)
    {
        var state = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            state = await OnUIThreadAsync(() =>
            {
                var controller = CoreTextInputController.Current;
                var view = controller?.View;
                return $"focused={ReferenceEquals(controller?.FocusedControl, Terminal(name))} profile={view?.Profile} editor={view?.OnCheckIsTextEditor()} androidFocus={view?.IsFocused}";
            }).ConfigureAwait(false);
            if (state == $"focused=True profile={profile} editor=True androidFocus=True")
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        state.Should().Be($"focused=True profile={profile} editor=True androidFocus=True");
    }

    /// <summary>The editor the text-input view describes to an input method: a terminal's.</summary>
    [Then("the soft keyboard is asked for no suggestions and no full-screen editor")]
    public async Task Then_editor_info()
    {
        var (type, options) = await OnUIThreadAsync(() =>
        {
            var info = new AEditorInfo();
            using var connection = CoreTextInputController.Current.View.OnCreateInputConnection(info);
            return (info.InputType, info.ImeOptions);
        }).ConfigureAwait(false);
        (type & AInputTypes.TextFlagNoSuggestions).Should().Be(AInputTypes.TextFlagNoSuggestions);
        (type & AInputTypes.TextVariationVisiblePassword).Should().Be(AInputTypes.TextVariationVisiblePassword);
        (type & AInputTypes.TextFlagMultiLine).Should().Be((AInputTypes)0);
        (options & AImeFlags.NoFullscreen).Should().Be(AImeFlags.NoFullscreen);
        (options & AImeFlags.NoExtractUi).Should().Be(AImeFlags.NoExtractUi);
    }

    /// <summary>The input method commits text.</summary>
    [When("the soft keyboard commits {string}")]
    public Task When_commits(string text) => WithConnectionAsync(c => c.CommitText(new JavaString(text), 1));

    /// <summary>The input method sets a composing text.</summary>
    [When("the soft keyboard composes {string}")]
    public Task When_composes(string text) => WithConnectionAsync(c => c.SetComposingText(new JavaString(text), 1));

    /// <summary>The input method finishes its composition.</summary>
    [When("the soft keyboard finishes its composition")]
    public Task When_finishes() => WithConnectionAsync(c => c.FinishComposingText());

    /// <summary>The input method asks to delete before the cursor.</summary>
    [When("the soft keyboard deletes {int} characters before the cursor")]
    public Task When_deletes(int count) => WithConnectionAsync(c => c.DeleteSurroundingText(count, 0));

    /// <summary>The input method performs its editor action (the Enter key of a single-line editor).</summary>
    [When("the soft keyboard performs its editor action")]
    public Task When_editor_action() => WithConnectionAsync(c => c.PerformEditorAction(AImeAction.None));

    /// <summary>The input method sends a key as key events (down, up) through its connection.</summary>
    [When("the soft keyboard sends the key {string}")]
    public async Task When_sends_key(string key)
    {
        var code = Enum.Parse<AKeycode>(key);
        await WithConnectionAsync(c =>
        {
            var now = ASystemClock.UptimeMillis();
            c.SendKeyEvent(new AKeyEvent(now, now, AKeyEventActions.Down, code, 0));
            c.SendKeyEvent(new AKeyEvent(now, now, AKeyEventActions.Up, code, 0));
            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>What the terminal sent its host (InputEmitted), polled for up to two seconds.</summary>
    [Then("the host of {string} received {string}")]
    public async Task Then_host_received(string name, string expected)
    {
        var wanted = expected.Replace("<CR>", "\r").Replace("<DEL>", "\x7f").Replace("<ESC>", "\u001b");
        var got = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            got = await OnUIThreadAsync(() => Hosts[name].ToString()).ConfigureAwait(false);
            if (got == wanted && attempt >= 2)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        Visible(got).Should().Be(Visible(wanted));
    }

    /// <summary>The session is closed after the focus left the terminal.</summary>
    [Then("the soft-keyboard session is closed")]
    public async Task Then_session_closed()
    {
        var state = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            state = await OnUIThreadAsync(() =>
            {
                var controller = CoreTextInputController.Current;
                var view = CoreTextInputView.For(AppHost.Activity as CodeBrix.Android.UI.Hosting.CodeBrixActivity);
                return $"focused={controller?.FocusedControl?.GetType().Name ?? "none"} editor={view?.OnCheckIsTextEditor()} androidFocus={view?.IsFocused}";
            }).ConfigureAwait(false);
            if (state == "focused=none editor=False androidFocus=False")
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        state.Should().Be("focused=none editor=False androidFocus=False");
    }

    /// <summary>The soft keyboard is on screen (the window's IME insets), within three seconds.</summary>
    [Then("the soft keyboard is showing")]
    public async Task Then_keyboard_showing()
    {
        var shown = false;
        for (var attempt = 0; attempt < 30 && !shown; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            shown = await OnUIThreadAsync(IsKeyboardShown).ConfigureAwait(false);
        }

        shown.Should().BeTrue("the soft keyboard must be on screen");
    }

    /// <summary>The soft keyboard is not on screen (checked after a second).</summary>
    [Then("the soft keyboard is hidden")]
    public async Task Then_keyboard_hidden()
    {
        await Task.Delay(1000).ConfigureAwait(false);
        var shown = await OnUIThreadAsync(IsKeyboardShown).ConfigureAwait(false);
        var asked = await OnUIThreadAsync(() => CoreTextInputController.Current.ShowCount).ConfigureAwait(false);
        shown.Should().BeFalse("the soft keyboard must stay hidden (the controller asked for it {0} times)", asked);
    }

    /// <summary>The user dismisses the keyboard (as its own hide key does), and it is gone.</summary>
    [When("the soft keyboard is dismissed")]
    public async Task When_dismissed()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var decor = AppHost.Activity?.Window?.DecorView;
            if (decor?.WindowToken is { } token && AppHost.Activity.GetSystemService(AContext.InputMethodService) is AInputMethodManager ime)
            {
                ime.HideSoftInputFromWindow(token, AHideSoftInputFlags.None);
            }
        }).ConfigureAwait(false);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            if (!await OnUIThreadAsync(IsKeyboardShown).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    /// <summary>A real finger tap in the middle of the terminal.</summary>
    [When("a real finger taps the TerminalView {string}")]
    public Task When_finger_taps(string name) => TapAsync(name, AMotionEventToolType.Finger);

    /// <summary>A real mouse click in the middle of the terminal.</summary>
    [When("a real mouse clicks the TerminalView {string}")]
    public Task When_mouse_clicks(string name) => TapAsync(name, AMotionEventToolType.Mouse);

    /// <summary>
    /// A real finger presses (and lifts) at a point of the terminal given in DIPs from its top left corner. The event's
    /// coordinates are worked out from the SCREEN, independently of the input router: the point's screen pixel (the root
    /// layout's content layer on screen + the element's XamlRoot position x density) minus where the window's decor view
    /// is on screen - Activity.DispatchTouchEvent receives decor-view coordinates.
    /// </summary>
    [When("a real finger presses the TerminalView {string} {int} DIPs right and {int} DIPs down from its top left corner")]
    public async Task When_finger_presses_at(string name, int dx, int dy)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var terminal = Terminal(name);
            Pressed.Clear();
            terminal.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, e) => Pressed.Add(e.GetCurrentPoint(terminal).Position)), true);
            var activity = (CodeBrix.Android.UI.Hosting.CodeBrixActivity)AppHost.Activity;
            var scale = terminal.XamlRoot?.RasterizationScale ?? 1d;
            var point = terminal.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(dx, dy));
            var content = new int[2];
            activity.RootLayout.ContentLayer.GetLocationOnScreen(content);
            var decor = new int[2];
            activity.Window.DecorView.GetLocationOnScreen(decor);
            var x = (int)Math.Round(point.X * scale) + content[0] - decor[0];
            var y = (int)Math.Round(point.Y * scale) + content[1] - decor[1];
            global::Android.Util.Log.Info("UIReqs.Terminal", $"finger at event ({x},{y}): content layer on screen {content[0]},{content[1]}, decor on screen {decor[0]},{decor[1]}");
            var down = ASystemClock.UptimeMillis();
            Touch(Obtain(down, down, AMotionEventActions.Down, x, y, AMotionEventToolType.Finger));
            Touch(Obtain(down, down + 60, AMotionEventActions.Up, x, y, AMotionEventToolType.Finger));
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Where Core reported the finger's press, relative to the terminal.</summary>
    [Then("the TerminalView {string} saw the finger {int} DIPs right and {int} DIPs down from its top left corner")]
    public async Task Then_saw_the_finger_at(string name, int dx, int dy)
    {
        var seen = await OnUIThreadAsync(() => Pressed.Count == 0 ? "none" : $"{Math.Round(Pressed[0].X)},{Math.Round(Pressed[0].Y)}").ConfigureAwait(false);
        seen.Should().Be($"{dx},{dy}", "Core must see the finger where it touched the screen (the keyboard is up: the window's decor view may sit below the status bar)");
    }

    private static readonly List<Windows.Foundation.Point> Pressed = new();

    /// <summary>
    /// The window left touch mode (a navigation key came through the system - here the soft keyboard's key event), the
    /// state in which Android draws a focused view's default focus highlight.
    /// </summary>
    [Then("the window is out of touch mode")]
    public async Task Then_out_of_touch_mode()
    {
        var inTouchMode = true;
        for (var attempt = 0; attempt < 20 && inTouchMode; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            inTouchMode = await OnUIThreadAsync(() => AppHost.Activity.Window.DecorView.IsInTouchMode).ConfigureAwait(false);
        }

        inTouchMode.Should().BeFalse("a key event from the input method must take the window out of touch mode for this check");
    }

    /// <summary>Shows a solid Grid that fills the panel.</summary>
    [When("the application shows a Grid named {string} painted {string} that fills the panel")]
    public async Task When_a_blank_grid(string name, string color)
    {
        ElementRegistry.Clear();
        Grid grid = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            grid = new Grid { Name = name, Background = new SolidColorBrush(CodeBrix.Platform.UI.Core.UIReqs.Support.Colors.Parse(color)) };
            ElementRegistry.Register(name, grid);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>A TextBox and a Button in a row above a solid Border that fills the rest of the panel.</summary>
    [When("the application shows a TextBox named {string} and a Button named {string} above a Border named {string} painted {string}")]
    public async Task When_textbox_button_ground(string box, string button, string ground, string color)
    {
        ElementRegistry.Clear();
        Grid grid = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            var entry = new TextBox { Name = box, Width = 300 };
            var other = new Button { Name = button, Content = "Other" };
            row.Children.Add(entry);
            row.Children.Add(other);
            var border = new Border { Name = ground, Background = new SolidColorBrush(CodeBrix.Platform.UI.Core.UIReqs.Support.Colors.Parse(color)) };
            Grid.SetRow(border, 1);
            grid.Children.Add(row);
            grid.Children.Add(border);
            ElementRegistry.Register(box, entry);
            ElementRegistry.Register(button, other);
            ElementRegistry.Register(ground, border);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Gives an element Core's (programmatic) focus.</summary>
    [When("the element {string} takes the focus")]
    public async Task When_element_takes_focus(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((Control)ElementRegistry.Resolve(name)).Focus(FocusState.Programmatic)).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The root layout takes the Android focus (where FocusAndroidPlatform keeps it for Core-focused elements).</summary>
    [When("the root layout takes the Android focus")]
    public async Task When_root_takes_focus()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((CodeBrix.Android.UI.Hosting.CodeBrixActivity)AppHost.Activity).RootLayout.RequestFocus()).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static bool _statusBarShown;

    private static int _showsSeen;

    /// <summary>
    /// Shows the status bar the scenario app hides (an application's normal window: the soft keyboard then moves the
    /// window's decor view below the status bar - the case the input router must measure the content from).
    /// </summary>
    [Given("the system status bar is showing")]
    public async Task Given_the_status_bar_is_showing()
    {
        _statusBarShown = true;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var window = AppHost.Activity.Window;
            AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView)?.Show(AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars());
        }).ConfigureAwait(false);
        await Task.Delay(500).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the input method after a scenario that used a custom control's soft-keyboard session, and puts the display
    /// back into touch mode after a scenario that took it out (a navigation key sent through the system
    /// by the soft keyboard's connection): touch mode is system-wide and survives the app, and the groups that follow
    /// must start in touch mode as on a fresh device. Only a real system touch does it: the host runs
    /// `adb shell input tap 1 1` (the empty panel's corner).
    /// </summary>
    [AfterScenario(Order = -40)]
    public async Task Back_into_touch_mode_with_a_fresh_input_method()
    {
        if (AppHost.PreserveDeviceConfiguration)
        {
            // Do not reset the selected input method or inject a system-wide corner tap in this mode.
            // The normal soft-keyboard hook still hides our app's keyboard. Cross-scenario IME isolation
            // must be reported as unverified by this run, rather than claiming the reset was performed.
            return;
        }

        // A scenario that opened a soft-keyboard session on a custom text control (this group, the copied TerminalView
        // group) leaves state in the input method (Gboard then shows itself for a later PasswordBox it did not show for
        // on a fresh device): the host resets the input method.
        var shows = await OnUIThreadAsync(() => CoreTextInputController.Current?.ShowCount ?? 0).ConfigureAwait(false);
        if (shows != _showsSeen)
        {
            _showsSeen = shows;
            var (resetExit, resetOutput) = await HostChannel.ShellAsync("ime reset", TestContext.Current.CancellationToken).ConfigureAwait(false);
            resetExit.Should().Be(0, "the host must reset the input method ({0})", resetOutput);
            await Task.Delay(500).ConfigureAwait(false);
            await WarmTheResetKeyboard().ConfigureAwait(false);
        }

        var inTouchMode = await OnUIThreadAsync(() => AppHost.Activity.Window.DecorView.IsInTouchMode).ConfigureAwait(false);
        if (inTouchMode)
        {
            return;
        }

        var (exit, output) = await HostChannel.ShellAsync("input tap 1 1", TestContext.Current.CancellationToken).ConfigureAwait(false);
        exit.Should().Be(0, "the host must tap the device to restore touch mode ({0})", output);
        for (var attempt = 0; attempt < 30 && !inTouchMode; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            inTouchMode = await OnUIThreadAsync(() => AppHost.Activity.Window.DecorView.IsInTouchMode).ConfigureAwait(false);
        }

        inTouchMode.Should().BeTrue("a real tap must put the display back into touch mode");
        await Settle().ConfigureAwait(false);
    }

    /// <summary>
    /// [AP7-B TerminalView RE-GATE] After an "ime reset" the input method cold-starts: its next appearance is slow and it
    /// briefly shows a different bottom row (the spacebar language label). A later group's native editor would capture
    /// that state (re-gate full pass: Text/PasswordBox and Text/TextBox keyboard frames). So the reset never leaks: a
    /// scratch 1x1 editor on the decor view shows the keyboard, the harness waits until it is visible AND two captures
    /// 300 ms apart show the same keyboard (bounded 4 s), then hides it, waits until hidden and settled, and removes the editor.
    /// </summary>
    private static async Task WarmTheResetKeyboard()
    {
        global::Android.Widget.EditText editor = null;
        global::Android.Views.View previousFocus = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var activity = AppHost.Activity;
            if (activity?.Window?.DecorView is not global::Android.Views.ViewGroup decor)
            {
                return;
            }

            previousFocus = activity.CurrentFocus;
            editor = new global::Android.Widget.EditText(activity) { Alpha = 0f };
            decor.AddView(editor, new global::Android.Widget.FrameLayout.LayoutParams(1, 1));
            editor.RequestFocus();
            ((AInputMethodManager)activity.GetSystemService(AContext.InputMethodService))?.ShowSoftInput(editor, global::Android.Views.InputMethods.ShowFlags.Implicit);
        }).ConfigureAwait(false);
        if (editor == null)
        {
            return;
        }

        try
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            byte[] last = null;
            while (started.ElapsedMilliseconds < 4000)
            {
                var (visible, top) = await OnUIThreadAsync(() => ImeState()).ConfigureAwait(false);
                if (visible && top > 0)
                {
                    var frame = await HostChannel.CaptureAsync(-(++_warmCaptures), 0, TestContext.Current.CancellationToken).ConfigureAwait(false);
                    var rows = KeyboardRows(frame, top);
                    if (last != null && rows != null && rows.AsSpan().SequenceEqual(last))
                    {
                        break;
                    }

                    last = rows;
                }

                await Task.Delay(300).ConfigureAwait(false);
            }
        }
        finally
        {
            await TestTargetFixture.RunOnUIThreadAsync(() =>
            {
                var activity = AppHost.Activity;
                ((AInputMethodManager)activity?.GetSystemService(AContext.InputMethodService))?.HideSoftInputFromWindow(editor.WindowToken, AHideSoftInputFlags.None);
                editor.ClearFocus();
                (editor.Parent as global::Android.Views.ViewGroup)?.RemoveView(editor);
                previousFocus?.RequestFocus();
            }).ConfigureAwait(false);
            var hidden = System.Diagnostics.Stopwatch.StartNew();
            while (hidden.ElapsedMilliseconds < 2000)
            {
                var (visible, _) = await OnUIThreadAsync(() => ImeState()).ConfigureAwait(false);
                var animating = await OnUIThreadAsync(() => AppHost.Activity?.InsetsListener?.IsImeAnimating == true).ConfigureAwait(false);
                if (!visible && !animating)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            await Settle().ConfigureAwait(false);
        }
    }

    private static int _warmCaptures;

    /// <summary>The IME's visibility and its top edge in window pixels (0 when not shown).</summary>
    private static (bool Visible, int Top) ImeState()
    {
        var decor = AppHost.Activity?.Window?.DecorView;
        var insets = decor?.RootWindowInsets;
        if (insets == null || !insets.IsVisible(AWindowInsets.Type.Ime()))
        {
            return (false, 0);
        }

        var bottom = insets.GetInsets(AWindowInsets.Type.Ime()).Bottom;
        return (true, bottom > 0 ? decor.Height - bottom : 0);
    }

    /// <summary>The frame's pixel rows from <paramref name="top"/> to the bottom (the keyboard only: the content above
    /// may blink a caret).</summary>
    private static byte[] KeyboardRows(CodeBrix.Android.UIReqs.Device.TestTarget.TestFrame frame, int top)
    {
        if (frame == null || top <= 0 || top >= frame.Height)
        {
            return null;
        }

        var rows = new byte[(frame.Height - top) * frame.Width * 4];
        var i = 0;
        for (var y = top; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var c = frame.GetPixel(x, y);
                rows[i++] = c.Red;
                rows[i++] = c.Green;
                rows[i++] = c.Blue;
                rows[i++] = c.Alpha;
            }
        }

        return rows;
    }

    /// <summary>Hides the status bar again after a scenario that showed it.</summary>
    [AfterScenario]
    public async Task Hide_the_status_bar_again()
    {
        if (!_statusBarShown)
        {
            return;
        }

        _statusBarShown = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var window = AppHost.Activity.Window;
            AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView)?.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
        }).ConfigureAwait(false);
        await Task.Delay(500).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task ShowAsync(string name, string button)
    {
        ElementRegistry.Clear();
        Hosts.Clear();
        UIElement content = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            // One real measurement in the terminal face before the grid is fitted (the TerminalView group's warm-up).
            CellMetrics.Measure(TerminalFont, 24f);
            var host = new StringBuilder();
            Hosts[name] = host;
            var terminal = new TerminalControl
            {
                Name = name,
                Width = 800,
                Height = 600,
                BackgroundColor = SKColors.Navy,
                ForegroundColor = SKColors.Yellow,
                TerminalFontSize = 24,
            };
            terminal.InputEmitted += data => host.Append(data);
            ElementRegistry.Register(name, terminal);
            if (button == null)
            {
                terminal.HorizontalAlignment = HorizontalAlignment.Center;
                terminal.VerticalAlignment = VerticalAlignment.Center;
                content = terminal;
                return;
            }

            var other = new Button { Name = button, Content = "Other" };
            ElementRegistry.Register(button, other);
            var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(other);
            panel.Children.Add(terminal);
            content = panel;
        }).ConfigureAwait(false);

        await TestTargetFixture.SetContentAsync(content).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static async Task WithConnectionAsync(Func<CoreTextInputConnection, bool> call)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = CoreTextInputController.Current?.View ?? throw new InvalidOperationException("No soft-keyboard session is open.");
            var connection = (CoreTextInputConnection)view.OnCreateInputConnection(new AEditorInfo());
            call(connection).Should().BeTrue();
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static async Task TapAsync(string name, AMotionEventToolType tool)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var terminal = Terminal(name);
            var scale = terminal.XamlRoot?.RasterizationScale ?? 1d;
            var bounds = terminal.TransformToVisual(null).TransformBounds(new Windows.Foundation.Rect(0, 0, terminal.ActualWidth, terminal.ActualHeight));
            var location = new int[2];
            AppHost.Activity.FindViewById(global::Android.Resource.Id.Content)?.GetLocationInWindow(location);
            var x = (int)Math.Round((bounds.X + (bounds.Width / 2)) * scale) + location[0];
            var y = (int)Math.Round((bounds.Y + (bounds.Height / 2)) * scale) + location[1];
            var down = ASystemClock.UptimeMillis();
            Touch(Obtain(down, down, AMotionEventActions.Down, x, y, tool));
            Touch(Obtain(down, down + 60, AMotionEventActions.Up, x, y, tool));
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static void Touch(AMotionEvent e)
    {
        AppHost.Activity.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static AMotionEvent Obtain(long downTime, long eventTime, AMotionEventActions action, int x, int y, AMotionEventToolType tool)
    {
        var mouse = tool == AMotionEventToolType.Mouse;
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = tool };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        var buttons = mouse && action != AMotionEventActions.Up ? AMotionEventButtonState.Primary : 0;
        var source = mouse ? AInputSourceType.Mouse : AInputSourceType.Touchscreen;
        return AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, buttons, 1f, 1f, 1, 0, source, 0);
    }

    private static bool IsKeyboardShown() =>
        AppHost.Activity?.Window?.DecorView?.RootWindowInsets is { } insets && insets.IsVisible(AWindowInsets.Type.Ime());

    private static string Visible(string text) => text.Replace("\r", "<CR>").Replace("\x7f", "<DEL>").Replace("\u001b", "<ESC>");

    private static TerminalControl Terminal(string name) => (TerminalControl)ElementRegistry.Resolve(name);

    private static TerminalCanvasElement Surface(string name) => Find<TerminalCanvasElement>(Terminal(name));

    private static T Find<T>(DependencyObject root)
        where T : class
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found)
            {
                return found;
            }

            if (Find<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static async Task Settle()
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(150).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
