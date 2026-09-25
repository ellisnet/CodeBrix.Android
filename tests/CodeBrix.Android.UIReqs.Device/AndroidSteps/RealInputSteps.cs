using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.System;
using AAxis = Android.Views.Axis;
using AInputSourceType = Android.Views.InputSourceType;
using AKeyEvent = Android.Views.KeyEvent;
using AKeyEventActions = Android.Views.KeyEventActions;
using AMetaKeyStates = Android.Views.MetaKeyStates;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventButtonState = Android.Views.MotionEventButtonState;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using AKeycode = Android.Views.Keycode;
using ASystemClock = Android.OS.SystemClock;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "Real Android input" feature (AndroidFeatures/AndroidInput): real
/// MotionEvents and KeyEvents dispatched to the activity (DispatchTouchEvent / DispatchGenericMotionEvent /
/// DispatchKeyEvent), so they take the path device input takes into Core's pointer and keyboard sources.
/// </summary>
[Binding]
public sealed class RealInputSteps
{
    /// <summary>KeyCharacterMap.VIRTUAL_KEYBOARD: the device id of key events no physical keyboard sent.</summary>
    private const int VirtualKeyboardDeviceId = -1;

    private static readonly Dictionary<string, Recorder> Recorders = new();

    /// <summary>Shows a Grid with a solid background that records its pointer and gesture events.</summary>
    [Given("the application shows a Grid named {string} {int} by {int} painted {string} that records its pointer events")]
    public async Task Given_a_recording_grid(string name, int width, int height, string color)
    {
        ElementRegistry.Clear();
        Recorders.Clear();
        var recorder = new Recorder();
        Recorders[name] = recorder;
        Grid grid = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            grid = new Grid
            {
                Name = name,
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Colors.Parse(color)),
            };
            grid.PointerPressed += (_, _) => recorder.Add("PointerPressed");
            grid.PointerReleased += (_, _) => recorder.Add("PointerReleased");
            grid.PointerEntered += (_, _) => recorder.Add("PointerEntered");
            grid.PointerExited += (_, _) => recorder.Add("PointerExited");
            grid.PointerCanceled += (_, _) => recorder.Add("PointerCanceled");
            grid.PointerWheelChanged += (_, e) =>
            {
                recorder.Add("PointerWheelChanged");
                recorder.WheelDelta = e.GetCurrentPoint(grid).Properties.MouseWheelDelta;
            };
            grid.Tapped += (_, _) => recorder.Add("Tapped");
            grid.RightTapped += (_, _) => recorder.Add("RightTapped");
            ElementRegistry.Register(name, grid);
        }).ConfigureAwait(false);

        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await TestTargetFixture.NextFrameAsync().ConfigureAwait(false);
    }

    /// <summary>Shows a Page (with a painted panel) that records the keys it receives.</summary>
    [Given("the application shows a Page named {string} that records its keys")]
    public async Task Given_a_recording_page(string name)
    {
        ElementRegistry.Clear();
        Recorders.Clear();
        var recorder = new Recorder();
        Recorders[name] = recorder;
        Page page = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            page = new Page
            {
                Name = name,
                Content = new Grid { Background = new SolidColorBrush(Colors.Parse("Blue")) },
            };
            page.KeyDown += (_, e) =>
            {
                recorder.Add("KeyDown");
                recorder.Keys.Add(e.Key);
            };
            ElementRegistry.Register(name, page);
        }).ConfigureAwait(false);

        await TestTargetFixture.SetContentAsync(page).ConfigureAwait(false);
        await TestTargetFixture.NextFrameAsync().ConfigureAwait(false);
    }

    /// <summary>Adds a keyboard accelerator to a page that records its invocation.</summary>
    [Given("the page {string} has a keyboard accelerator {string} + {string}")]
    public async Task Given_the_page_has_an_accelerator(string name, string modifiers, VirtualKey key)
    {
        var recorder = Recorders[name];
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = Enum.Parse<VirtualKeyModifiers>(modifiers) };
            accelerator.Invoked += (_, e) =>
            {
                recorder.Add("AcceleratorInvoked");
                e.Handled = true;
            };
            ElementRegistry.Resolve(name).KeyboardAccelerators.Add(accelerator);
        }).ConfigureAwait(false);

        // Core collects the live accelerators when an element enters the tree: show the page again.
        var page = (Page)ElementRegistry.Resolve(name);
        await TestTargetFixture.ClearContentAsync().ConfigureAwait(false);
        ElementRegistry.Register(name, page);
        await TestTargetFixture.SetContentAsync(page).ConfigureAwait(false);
        await TestTargetFixture.NextFrameAsync().ConfigureAwait(false);
    }

    /// <summary>Gives a page the keyboard focus (programmatic focus).</summary>
    [Given("the page {string} has the keyboard focus")]
    public async Task Given_the_page_has_focus(string name)
    {
        var focused = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var page = (Page)ElementRegistry.Resolve(name);
            page.IsTabStop = true;
            focused = page.Focus(FocusState.Programmatic);
        }).ConfigureAwait(false);
        focused.Should().BeTrue("the page \"{0}\" must take the keyboard focus", name);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A finger taps the middle of an element (DOWN then UP, 50 ms apart).</summary>
    [When("a real finger taps {string}")]
    public async Task When_a_real_finger_taps(string name)
    {
        var (x, y) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        await TapAsync(x, y).ConfigureAwait(false);
    }

    /// <summary>A finger taps a corner of the panel, far from an element.</summary>
    [When("a real finger taps the panel far from {string}")]
    public async Task When_a_real_finger_taps_far(string name)
    {
        var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
        var x = bounds.X > 40 ? 20 : TestTargetFixture.PanelWidth - 20;
        var y = bounds.Y > 40 ? 20 : TestTargetFixture.PanelHeight - 20;
        await TapAsync(x, y).ConfigureAwait(false);
    }

    /// <summary>A mouse clicks an element with its right (secondary) button.</summary>
    [When("a real mouse right-clicks {string}")]
    public async Task When_a_real_mouse_right_clicks(string name)
    {
        var (x, y) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Touch(Mouse(down, down, AMotionEventActions.Down, x, y, AMotionEventButtonState.Secondary));
            Touch(Mouse(down, down + 60, AMotionEventActions.Up, x, y, 0));
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>The mouse wheel turns one notch away from the user over an element.</summary>
    [When("the real mouse wheel turns one notch up over {string}")]
    public async Task When_the_real_mouse_wheel_turns(string name)
    {
        var (x, y) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var now = ASystemClock.UptimeMillis();
            Generic(Mouse(now, now, AMotionEventActions.HoverEnter, x, y, 0));
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var now = ASystemClock.UptimeMillis();
            Generic(Mouse(now, now, AMotionEventActions.Scroll, x, y, 0, vscroll: 1f));
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A mouse (no button) moves from the left of an element, across it, to its right.</summary>
    [When("a real mouse hovers across {string}")]
    public async Task When_a_real_mouse_hovers_across(string name)
    {
        var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), 0).ConfigureAwait(false);
        var y = bounds.Y + (bounds.Height / 2);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var now = ASystemClock.UptimeMillis();
            Generic(Mouse(now, now, AMotionEventActions.HoverEnter, Math.Max(1, bounds.X - 20), y, 0));
            for (var step = 0; step <= 10; step++)
            {
                var x = bounds.X - 20 + ((bounds.Width + 40) * step / 10);
                Generic(Mouse(now, now + (step * 16), AMotionEventActions.HoverMove, x, y, 0));
            }

            Generic(Mouse(now, now + 200, AMotionEventActions.HoverExit, bounds.X + bounds.Width + 20, y, 0));
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A hardware-keyboard key is pressed and released.</summary>
    [When("the real key {string} is pressed")]
    public async Task When_the_real_key_is_pressed(VirtualKey key) => await PressAsync(key, alt: false).ConfigureAwait(false);

    /// <summary>A hardware-keyboard key is pressed and released while Alt is held.</summary>
    [When("the real key {string} is pressed with Alt held")]
    public async Task When_the_real_key_is_pressed_with_alt(VirtualKey key) => await PressAsync(key, alt: true).ConfigureAwait(false);

    /// <summary>Asserts how many times an element recorded an event.</summary>
    [Then("{string} recorded {int} {string}")]
    public void Then_recorded(string name, int count, string eventName) =>
        Recorders[name].Count(eventName).Should().Be(count, "\"{0}\" recorded [{1}]", name, string.Join(", ", Recorders[name].Events));

    /// <summary>Asserts the last wheel delta an element recorded.</summary>
    [Then("the last wheel delta {string} recorded is {int}")]
    public void Then_the_wheel_delta(string name, int delta) => Recorders[name].WheelDelta.Should().Be(delta);

    /// <summary>Asserts a key a page's KeyDown handler received.</summary>
    [Then("{string} recorded the key {string}")]
    public void Then_recorded_the_key(string name, VirtualKey key)
    {
        var keys = Recorders[name].Keys;
        keys.Should().Contain(key, "the page recorded [{0}]", string.Join(", ", keys));
    }

    private static async Task TapAsync(int x, int y)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Touch(Finger(down, down, AMotionEventActions.Down, x, y));
            Touch(Finger(down, down + 50, AMotionEventActions.Up, x, y));
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task PressAsync(VirtualKey key, bool alt)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var code = CodeBrix.Android.UI.Input.VirtualKeyHelper.ToKeyCode(key);
            var meta = alt ? AMetaKeyStates.AltOn | AMetaKeyStates.AltLeftOn : 0;
            var now = ASystemClock.UptimeMillis();
            var activity = AppHost.Activity!;
            if (alt)
            {
                activity.DispatchKeyEvent(new AKeyEvent(now, now, AKeyEventActions.Down, AKeycode.AltLeft, 0, meta, VirtualKeyboardDeviceId, 0, 0, AInputSourceType.Keyboard));
            }

            activity.DispatchKeyEvent(new AKeyEvent(now, now, AKeyEventActions.Down, code, 0, meta, VirtualKeyboardDeviceId, 0, 0, AInputSourceType.Keyboard));
            activity.DispatchKeyEvent(new AKeyEvent(now, now + 30, AKeyEventActions.Up, code, 0, meta, VirtualKeyboardDeviceId, 0, 0, AInputSourceType.Keyboard));
            if (alt)
            {
                activity.DispatchKeyEvent(new AKeyEvent(now, now + 40, AKeyEventActions.Up, AKeycode.AltLeft, 0, 0, VirtualKeyboardDeviceId, 0, 0, AInputSourceType.Keyboard));
            }
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static void Touch(AMotionEvent e)
    {
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static void Generic(AMotionEvent e)
    {
        AppHost.Activity!.DispatchGenericMotionEvent(e);
        e.Recycle();
    }

    private static AMotionEvent Finger(long downTime, long eventTime, AMotionEventActions action, int x, int y) =>
        Obtain(downTime, eventTime, action, x, y, AMotionEventToolType.Finger, AInputSourceType.Touchscreen, 0, 0f);

    private static AMotionEvent Mouse(long downTime, long eventTime, AMotionEventActions action, int x, int y, AMotionEventButtonState buttons, float vscroll = 0f) =>
        Obtain(downTime, eventTime, action, x, y, AMotionEventToolType.Mouse, AInputSourceType.Mouse, buttons, vscroll);

    private static AMotionEvent Obtain(long downTime, long eventTime, AMotionEventActions action, int x, int y,
        AMotionEventToolType tool, AInputSourceType source, AMotionEventButtonState buttons, float vscroll)
    {
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = tool };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        if (vscroll != 0)
        {
            coords.SetAxisValue(AAxis.Vscroll, vscroll);
        }

        return AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, buttons, 1f, 1f, 1, 0, source, 0)!;
    }

    private sealed class Recorder
    {
        internal List<string> Events { get; } = new();

        internal List<VirtualKey> Keys { get; } = new();

        internal int WheelDelta { get; set; }

        internal void Add(string eventName) => Events.Add(eventName);

        internal int Count(string eventName) => Events.FindAll(e => e == eventName).Count;
    }
}
