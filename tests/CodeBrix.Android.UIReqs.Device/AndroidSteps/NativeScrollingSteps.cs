using System;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AAxis = Android.Views.Axis;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using ASystemClock = Android.OS.SystemClock;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "Native scrolling" feature (AndroidFeatures/AndroidNative): real finger
/// drags and mouse wheel notches dispatched to the activity, and the native view's scroll position.
/// </summary>
[Binding]
public sealed class NativeScrollingSteps
{
    /// <summary>A finger drags the middle of a ScrollViewer by a distance (slowly, so it does not fling).</summary>
    [When("a real finger drags the ScrollViewer {string} {int} pixels up")]
    public async Task When_a_real_finger_drags_up(string name, int distance) => await DragAsync(name, distance).ConfigureAwait(false);

    /// <summary>A finger drags the middle of a ScrollViewer downwards.</summary>
    [When("a real finger drags the ScrollViewer {string} {int} pixels down")]
    public async Task When_a_real_finger_drags_down(string name, int distance) => await DragAsync(name, -distance).ConfigureAwait(false);

    /// <summary>The mouse wheel turns one notch towards the user (content scrolls down) over an element.</summary>
    [When("the real mouse wheel turns one notch down over {string}")]
    public async Task When_the_wheel_turns_down(string name)
    {
        var (x, y) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            // A real mouse hovers into the view before its wheel turns.
            var now = ASystemClock.UptimeMillis();
            Mouse(now, AMotionEventActions.HoverEnter, x, y, 0f);
            Mouse(now + 16, AMotionEventActions.HoverMove, x, y, 0f);
        }).ConfigureAwait(false);

        // Core takes the hover in first (it drops a wheel from a mouse it has not seen), as a real
        // mouse's hover events arrive frames before its wheel does.
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            Mouse(ASystemClock.UptimeMillis(), AMotionEventActions.Scroll, x, y, -1f)).ConfigureAwait(false);
        await Task.Delay(400).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static readonly System.Collections.Generic.Dictionary<string, int[]> PadEvents = new();
    private const int DragSteps = 15;

    /// <summary>
    /// [AP8-S batch 3] A ScrollViewer (both axes scrollable by 40 DIPs) holding a pad that captures the pointer when pressed
    /// (a drawing surface: ManipulationMode All, WinUI's way to keep a touch drag from the ScrollViewer's panning) and counts its pressed / moved (while pressed) / released / canceled / lost events.
    /// </summary>
    [Given("the application shows a ScrollViewer named {string} {int} by {int} holding a pad named {string} {int} by {int} that captures the pointer and handles its own manipulations")]
    public Task Given_a_capturing_pad(string scroller, int width, int height, string pad, int padWidth, int padHeight)
        => ShowCapturingPadAsync(scroller, width, height, pad, padWidth, padHeight, Microsoft.UI.Xaml.Input.ManipulationModes.All);

    /// <summary>
    /// [AP8-S batch 4] The same pad WITHOUT opting out of panning (ManipulationMode System, the default): it captures the
    /// pointer when pressed, as a plain drawing surface does.
    /// </summary>
    [Given("the application shows a ScrollViewer named {string} {int} by {int} holding a pad named {string} {int} by {int} that captures the pointer")]
    public Task Given_a_plain_capturing_pad(string scroller, int width, int height, string pad, int padWidth, int padHeight)
        => ShowCapturingPadAsync(scroller, width, height, pad, padWidth, padHeight, Microsoft.UI.Xaml.Input.ManipulationModes.System);

    private static async Task ShowCapturingPadAsync(string scroller, int width, int height, string pad, int padWidth, int padHeight, Microsoft.UI.Xaml.Input.ManipulationModes mode)
    {
        ScrollViewer viewer = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var counts = new int[5];
            PadEvents[pad] = counts;
            var border = new Microsoft.UI.Xaml.Controls.Border
            {
                Name = pad,
                Width = padWidth,
                Height = padHeight,
                Background = new SolidColorBrush(CodeBrix.Platform.UI.Core.UIReqs.Support.Colors.Parse("Teal")),
                ManipulationMode = mode,
            };
            var pressed = false;
            border.PointerPressed += (_, e) => { counts[0]++; pressed = border.CapturePointer(e.Pointer); e.Handled = true; };
            border.PointerMoved += (_, _) => { if (pressed) { counts[1]++; } };
            border.PointerReleased += (_, e) => { counts[2]++; pressed = false; border.ReleasePointerCapture(e.Pointer); e.Handled = true; };
            border.PointerCanceled += (_, _) => { counts[3]++; pressed = false; };
            border.PointerCaptureLost += (_, _) => { if (pressed) { counts[4]++; } };
            viewer = new ScrollViewer
            {
                Name = scroller,
                Width = width,
                Height = height,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Enabled,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
                Content = border,
            };
            ElementRegistry.Register(scroller, viewer);
            ElementRegistry.Register(pad, border);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(viewer).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>[AP8-S batch 3] A finger presses the pad at a point (DIPs from its corner), drags to another in steps, lifts.</summary>
    [When("a real finger drags across the pad {string} from {int}, {int} to {int}, {int}")]
    public async Task When_finger_drags_across_pad(string pad, int x0, int y0, int x1, int y1)
    {
        var rect = await DeviceRect.OfAsync(ElementRegistry.Resolve(pad)).ConfigureAwait(false);
        var density = 1.0;
        await TestTargetFixture.RunOnUIThreadAsync(() => density = ((Microsoft.UI.Xaml.UIElement)ElementRegistry.Resolve(pad)).XamlRoot?.RasterizationScale ?? 1).ConfigureAwait(false);
        (int X, int Y) At(double t) => (rect.Center.X - (int)Math.Round(rect.Width / 2.0) + (int)Math.Round((x0 + ((x1 - x0) * t)) * density),
            rect.Center.Y - (int)Math.Round(rect.Height / 2.0) + (int)Math.Round((y0 + ((y1 - y0) * t)) * density));
        var down = ASystemClock.UptimeMillis();
        var start = At(0);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down, AMotionEventActions.Down, start.X, start.Y)).ConfigureAwait(false);
        for (var step = 1; step <= DragSteps; step++)
        {
            var point = At(step / (double)DragSteps);
            var time = down + (step * 30);
            await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, time, AMotionEventActions.Move, point.X, point.Y)).ConfigureAwait(false);
            await Task.Delay(16).ConfigureAwait(false);
        }

        var end = At(1);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down + (DragSteps * 30) + 50, AMotionEventActions.Up, end.X, end.Y)).ConfigureAwait(false);
        await Task.Delay(300).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>[AP8-S batch 3] The pad was pressed once, saw every move while pressed, one release, no cancel, no lost capture.</summary>
    [Then("the pad {string} saw every move of the finger and its release, and no cancel")]
    public async Task Then_pad_saw_everything(string pad)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        var counts = PadEvents[pad];
        var seen = $"pressed {counts[0]}, moved {counts[1]}, released {counts[2]}, canceled {counts[3]}, capture lost {counts[4]}";
        seen.Should().Be($"pressed 1, moved {DragSteps}, released 1, canceled 0, capture lost 0");
    }

    /// <summary>[AP8-S batch 3] Neither offset of the ScrollViewer moved.</summary>
    [Then("the ScrollViewer {string} is not scrolled")]
    public async Task Then_not_scrolled(string name)
    {
        var (h, v) = (-1.0, -1.0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var viewer = (ScrollViewer)ElementRegistry.Resolve(name);
            (h, v) = (viewer.HorizontalOffset, viewer.VerticalOffset);
        }).ConfigureAwait(false);
        $"{h},{v}".Should().Be("0,0", "the finger drew on the pad; the ScrollViewer must not scroll");
    }

    /// <summary>[AP8-S batch 4] The ScrollViewer panned: an offset moved (the drag went up and to the left).</summary>
    [Then("the ScrollViewer {string} is scrolled")]
    public async Task Then_scrolled(string name)
    {
        var (h, v) = (0.0, 0.0);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await TestTargetFixture.RunOnUIThreadAsync(() =>
            {
                var viewer = (ScrollViewer)ElementRegistry.Resolve(name);
                (h, v) = (viewer.HorizontalOffset, viewer.VerticalOffset);
            }).ConfigureAwait(false);
            if (h > 0 || v > 0)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        (h + v).Should().BeGreaterThan(20, "the finger's drag must pan the ScrollViewer \"{0}\" (offsets {1},{2})", name, h, v);
    }

    /// <summary>
    /// [AP8-S batch 4] The pad was pressed once and gave the pointer up when the ScrollViewer took the drag (it lost its capture
    /// or was canceled) - it saw no release.
    /// </summary>
    [Then("the pad {string} gave the pointer up to the ScrollViewer")]
    public async Task Then_pad_gave_up(string pad)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        var counts = PadEvents[pad];
        var seen = $"pressed {counts[0]}, moved {counts[1]}, released {counts[2]}, canceled {counts[3]}, capture lost {counts[4]}";
        counts[0].Should().Be(1, seen);
        counts[2].Should().Be(0, seen);
        (counts[3] + counts[4]).Should().BeGreaterThan(0, seen);
        counts[1].Should().BeLessThan(DragSteps, seen);
    }

    /// <summary>The native scroll view shows the offset Core reports (within a pixel).</summary>
    [Then("the native scroll position of {string} matches its VerticalOffset")]
    public async Task Then_the_native_position_matches(string name)
    {
        double offset = 0;
        int scrollY = -1;
        double density = 1;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var viewer = (ScrollViewer)ElementRegistry.Resolve(name);
            offset = viewer.VerticalOffset;
            density = viewer.XamlRoot?.RasterizationScale ?? 1;
            var presenter = FindPresenter(viewer);
            var view = presenter == null ? null : NativeViewLocator.ViewOf(presenter);
            scrollY = view?.ScrollY ?? -1;
        }).ConfigureAwait(false);
        scrollY.Should().BeGreaterThanOrEqualTo(0, "the ScrollViewer \"{0}\" must have a native scroll view", name);
        ((double)scrollY).Should().BeApproximately(offset * density, 1.0,
            "the native scroll view of \"{0}\" must be where Core's VerticalOffset {1} says", name, offset);
    }

    private static async Task DragAsync(string name, int distance)
    {
        var (x, startY) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        const int steps = 15;
        var down = ASystemClock.UptimeMillis();
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down, AMotionEventActions.Down, x, startY)).ConfigureAwait(false);
        for (var step = 1; step <= steps; step++)
        {
            var y = startY - (int)Math.Round(distance * (step / (double)steps));
            var time = down + (step * 30);
            await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, time, AMotionEventActions.Move, x, y)).ConfigureAwait(false);
            await Task.Delay(16).ConfigureAwait(false);
        }

        // Rest before lifting, so the release has no velocity (no fling).
        var end = down + (steps * 30) + 300;
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, end - 100, AMotionEventActions.Move, x, startY - distance)).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, end, AMotionEventActions.Up, x, startY - distance)).ConfigureAwait(false);
        await Task.Delay(300).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static void Mouse(long time, AMotionEventActions action, int x, int y, float vscroll)
    {
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = AMotionEventToolType.Mouse };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y };
        if (vscroll != 0)
        {
            coords.SetAxisValue(AAxis.Vscroll, vscroll);
        }

        var e = AMotionEvent.Obtain(time, time, action, 1, new[] { properties }, new[] { coords }, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Mouse, 0)!;
        AppHost.Activity!.DispatchGenericMotionEvent(e);
        e.Recycle();
    }

    private static void Touch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = AMotionEventToolType.Finger };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        var e = AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Touchscreen, 0)!;
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static ScrollContentPresenter? FindPresenter(Microsoft.UI.Xaml.DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollContentPresenter presenter)
            {
                return presenter;
            }

            if (FindPresenter(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
