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
