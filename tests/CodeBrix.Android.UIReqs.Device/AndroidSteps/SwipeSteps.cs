#nullable disable

using System;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AMotionEventActions = Android.Views.MotionEventActions;
using ASystemClock = Android.OS.SystemClock;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP1.9: the SwipeControl fences of AndroidElements10B - what a real finger's swipe leaves behind (Reveal stays open
/// at its items' width after a short or a long swipe, a flick back closes it, Execute invokes once and closes), and the
/// measurements behind it (logcat "AP19-SWIPE": the release velocity and cumulative translation Core's
/// ManipulationCompleted carries, the width of the template's SwipeContentStackPanel Core snaps to, the resting
/// position of the content). Core decides all of it (SwipeControl.Platform.cs SimulateInertia); these steps only
/// drive a real finger and read Core's state.
/// </summary>
[Binding]
public sealed class SwipeSteps
{
    private static int _invoked;
    private static double _releaseVelocity = double.NaN;
    private static double _releaseCumulative = double.NaN;
    private static double _stackWidthAtRelease = double.NaN;
    private static string _lastTransform;
    private static double _lastTranslateX = double.NaN;

    /// <summary>Counts an Execute item's Invoked (the "execute swipe control" sample).</summary>
    internal static void CountInvoked() => _invoked++;

    /// <summary>Forgets the counters after every scenario.</summary>
    [AfterScenario]
    public static void Forget_the_swipe_state()
    {
        _invoked = 0;
        _releaseVelocity = double.NaN;
        _releaseCumulative = double.NaN;
        _stackWidthAtRelease = double.NaN;
    }

    /// <summary>Records what Core's manipulation of a SwipeControl's ContentRoot reports at the finger's release.</summary>
    [Given("the release of a swipe on {string} is measured")]
    public async Task Given_the_release_is_measured(string name) =>
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var swipe = ElementRegistry.Resolve(name);
            var content = FindNamed(swipe, "ContentRoot") ?? throw new InvalidOperationException($"\"{name}\" has no ContentRoot");
            content.ManipulationCompleted += (_, e) =>
            {
                _releaseVelocity = e.Velocities.Linear.X;
                _releaseCumulative = e.Cumulative.Translation.X;
                _stackWidthAtRelease = FindNamed(swipe, "SwipeContentStackPanel")?.ActualWidth ?? double.NaN;
            };
        }).ConfigureAwait(false);

    /// <summary>A real finger swipes an element over 400 ms, rests 300 ms without moving, then lifts.</summary>
    [When("a real finger swipes {string} {int} pixels {word} and rests before lifting")]
    public async Task When_a_real_finger_swipes_and_rests(string name, int distance, string direction)
    {
        var sign = direction == "left" ? -1 : 1;
        var (x, y) = await StartAsync(name).ConfigureAwait(false);
        var down = ASystemClock.UptimeMillis();
        await DispatchAsync(down, down, AMotionEventActions.Down, x, y).ConfigureAwait(false);
        const int Steps = 25;
        for (var i = 1; i <= Steps; i++)
        {
            await Task.Delay(16).ConfigureAwait(false);
            await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Move, x + (sign * distance * i / Steps), y).ConfigureAwait(false);
        }

        for (var i = 0; i < 18; i++)
        {
            await Task.Delay(16).ConfigureAwait(false);
            await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Move, x + (sign * distance), y).ConfigureAwait(false);
        }

        await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Up, x + (sign * distance), y).ConfigureAwait(false);
        await Elements10BSteps.SettleAsync(1500).ConfigureAwait(false);
    }

    /// <summary>A real finger swipes left, then flicks back to the right quickly and lifts while moving.</summary>
    [When("a real finger swipes {string} {int} pixels left and flicks {int} pixels back before lifting")]
    public async Task When_a_real_finger_swipes_and_flicks_back(string name, int distance, int back)
    {
        var (x, y) = await StartAsync(name).ConfigureAwait(false);
        var down = ASystemClock.UptimeMillis();
        await DispatchAsync(down, down, AMotionEventActions.Down, x, y).ConfigureAwait(false);
        const int Steps = 25;
        for (var i = 1; i <= Steps; i++)
        {
            await Task.Delay(16).ConfigureAwait(false);
            await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Move, x - (distance * i / Steps), y).ConfigureAwait(false);
        }

        for (var i = 1; i <= 3; i++)
        {
            await Task.Delay(16).ConfigureAwait(false);
            await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Move, x - distance + (back * i / 3), y).ConfigureAwait(false);
        }

        await DispatchAsync(down, ASystemClock.UptimeMillis(), AMotionEventActions.Up, x - distance + back, y).ConfigureAwait(false);
        await Elements10BSteps.SettleAsync(1500).ConfigureAwait(false);
    }

    /// <summary>Asserts the content rests open at the width of the revealed items (and logs the measurements).</summary>
    [Then("the SwipeControl {string} rests open showing its items on the {word}")]
    public async Task Then_it_rests_open(string name, string side)
    {
        var (x, width) = await RestingAsync(name).ConfigureAwait(false);
        width.Should().BeGreaterThan(0, "Core snaps the content to the width of the SwipeContentStackPanel of \"{0}\"", name);
        var expected = side == "right" ? -width : width;
        x.Should().BeApproximately(expected, 2, "the content of \"{0}\" rests open at its items' width", name);
    }

    /// <summary>Asserts the content rests shut (and logs the measurements).</summary>
    [Then("the SwipeControl {string} rests shut")]
    public async Task Then_it_rests_shut(string name)
    {
        var (x, _) = await RestingAsync(name).ConfigureAwait(false);
        x.Should().BeApproximately(0, 1, "the content of \"{0}\" rests shut", name);
    }

    /// <summary>Asserts how often the Execute item was invoked.</summary>
    [Then("the Execute item was invoked {int} times")]
    public void Then_the_Execute_item_was_invoked(int times) => _invoked.Should().Be(times);

    /// <summary>Asserts the sign of the release velocity Core received (the direction the finger was moving).</summary>
    [Then("the release velocity of the swipe points {word}")]
    public void Then_the_release_velocity_points(string direction)
    {
        double.IsNaN(_releaseVelocity).Should().BeFalse("ManipulationCompleted must have been raised");
        if (direction == "left")
        {
            _releaseVelocity.Should().BeLessThanOrEqualTo(0, "the finger moved left or rested");
        }
        else
        {
            _releaseVelocity.Should().BeGreaterThan(0, "the finger was flicking right");
        }
    }

    private static async Task<(double X, double Width)> RestingAsync(string name)
    {
        var (x, width) = await Elements10BSteps.OnUIThreadAsync(() =>
        {
            var swipe = ElementRegistry.Resolve(name);
            var content = FindNamed(swipe, "ContentRoot");
            var stack = FindNamed(swipe, "SwipeContentStackPanel");
            _lastTransform = content?.RenderTransform?.GetType().Name ?? "none";
            _lastTranslateX = (content?.RenderTransform as TranslateTransform)?.X ?? double.NaN;

            // Where the content IS, whatever transform Core uses to put it there.
            var offset = content == null ? double.NaN : content.TransformToVisual(swipe).TransformPoint(new Windows.Foundation.Point(0, 0)).X;
            return (offset, stack?.ActualWidth ?? double.NaN);
        }).ConfigureAwait(false);
        global::Android.Util.Log.Info("AP19-SWIPE", string.Create(CultureInfo.InvariantCulture,
            $"{name}: releaseVelocity={_releaseVelocity:0.###} DIP/ms cumulative={_releaseCumulative:0.#} stackWidthAtRelease={_stackWidthAtRelease:0.#} restingOffsetX={x:0.#} renderTransform={_lastTransform} translateX={_lastTranslateX:0.#} stackWidthNow={width:0.#} invoked={_invoked}"));
        return (x, width);
    }

    private static async Task<(int X, int Y)> StartAsync(string name) =>
        await Elements10BSteps.OnUIThreadAsync(() =>
        {
            var r = Elements10BSteps.WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name)));
            return (r.CenterX(), r.CenterY());
        }).ConfigureAwait(false);

    private static Task DispatchAsync(long down, long time, AMotionEventActions action, int x, int y) =>
        TestTargetFixture.RunOnUIThreadAsync(() => Elements10BSteps.Dispatch(down, time, action, x, y));

    /// <summary>
    /// A part of the SwipeControl's OWN template by name. The swipe items are AppBarButton-templated and have template
    /// parts of the same names (their own "ContentRoot"), so the items panel is not searched below its root.
    /// </summary>
    private static FrameworkElement FindNamed(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name)
            {
                return fe;
            }

            if (child is FrameworkElement { Name: "SwipeContentStackPanel" })
            {
                continue;
            }

            if (FindNamed(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
