using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.UI.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventButtonState = Android.Views.MotionEventButtonState;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using ASystemClock = Android.OS.SystemClock;
// Aliased under its own name (see CommandBarSteps: "GlobalStaticResources" would be shadowed by the framework's class).
using ToolkitResources = global::CodeBrix.Platform.UI.Toolkit.GlobalStaticResources;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The Toolkit TriPaneView on Android" feature (AndroidFeatures/AndroidToolkit): the
/// TriPaneView's Android handler (Handlers/Toolkit/TriPaneViewHandler) - its adaptive form by window size class and its
/// native finger drags on the dividers - driven with REAL MotionEvents dispatched to the activity, at window pixels
/// computed from the element's bounds and the window's real density (a real resize changes the density).
/// </summary>
[Binding]
public sealed class TriPaneViewSteps
{
    private static readonly Dictionary<string, int> Completed = new();

    /// <summary>Puts the adaptive form back on after a scenario that switched it off.</summary>
    [AfterScenario]
    public async Task Put_the_adaptive_form_back() =>
        await TestTargetFixture.RunOnUIThreadAsync(() => AdaptivePolicy.AdaptiveTriPaneView = true).ConfigureAwait(false);

    /// <summary>Turns the adaptive form off (AdaptivePolicy.AdaptiveTriPaneView).</summary>
    [Given("the adaptive TriPaneView form is switched off")]
    public async Task Given_the_adaptive_form_is_off() =>
        await TestTargetFixture.RunOnUIThreadAsync(() => AdaptivePolicy.AdaptiveTriPaneView = false).ConfigureAwait(false);

    /// <summary>Shows a TriPaneView filling the panel, each pane a solid colour (registered as "name.side" / ".upper" / ".lower").</summary>
    [Given("the application shows a TriPaneView named {string} with its panes painted {string}, {string} and {string}")]
    public async Task Given_a_TriPaneView(string name, string side, string upper, string lower)
    {
        ElementRegistry.Clear();
        Completed.Clear();
        TriPaneView view = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            // The scenario app has no App.xaml, so nothing generated registers the Toolkit's default styles (an app's XAML
            // generator does): register them as the CommandBar group registers its add-in's.
            ToolkitResources.Initialize();
            ToolkitResources.RegisterDefaultStyles();
            ToolkitResources.RegisterResourceDictionariesBySource();

            Border Pane(string part, string color)
            {
                var border = new Border { Name = $"{name}.{part}", Background = new SolidColorBrush(Colors.Parse(color)) };
                ElementRegistry.Register(border.Name, border);
                return border;
            }

            view = new TriPaneView
            {
                Name = name,
                SidePane = Pane("side", side),
                UpperPane = Pane("upper", upper),
                LowerPane = Pane("lower", lower),
                SidePaneVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                UpperPaneVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                LowerPaneVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            Completed[name] = 0;
            view.DividerDragCompleted += (_, _) => Completed[name]++;
            ElementRegistry.Register(name, view);
        }).ConfigureAwait(false);

        await TestTargetFixture.SetContentAsync(view).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>The control is shown by the Android TriPaneView handler (not the templated fallback).</summary>
    [Then("the TriPaneView {string} is shown by its Android handler")]
    public async Task Then_shown_by_its_handler(string name)
    {
        var handler = await OnUIThreadAsync(() => PolicyDiagnostics.HandlerOf(Tri(name))?.GetType().Name).ConfigureAwait(false);
        handler.Should().Be(nameof(TriPaneViewHandler));
    }

    /// <summary>Exactly these panes have room on screen (a pane is shown when its scroll viewer part has a size).</summary>
    [Then("the TriPaneView {string} shows the panes {string}")]
    public async Task Then_shows_the_panes(string name, string panes)
    {
        var wanted = panes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).OrderBy(p => p).ToArray();
        string[] shown = Array.Empty<string>();
        string detail = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            (shown, detail) = await OnUIThreadAsync(() =>
            {
                var view = Tri(name);
                var list = new List<string>();
                var sizes = new List<string>();
                foreach (var (pane, part) in new[] { ("side", "PART_SidePaneScrollViewer"), ("upper", "PART_UpperPaneScrollViewer"), ("lower", "PART_LowerPaneScrollViewer") })
                {
                    var viewer = Descendants(view).OfType<ScrollViewer>().First(s => s.Name == part);
                    sizes.Add($"{pane}={viewer.ActualWidth:0}x{viewer.ActualHeight:0}");
                    if (viewer.ActualWidth >= 1 && viewer.ActualHeight >= 1)
                    {
                        list.Add(pane);
                    }
                }

                return (list.OrderBy(p => p).ToArray(), $"{string.Join(" ", sizes)}; weights {Weights(view)}");
            }).ConfigureAwait(false);
            if (shown.SequenceEqual(wanted))
            {
                break;
            }

            await Task.Delay(150).ConfigureAwait(false);
        }

        string.Join(", ", shown).Should().Be(string.Join(", ", wanted), "the panes on screen ({0})", detail);
    }

    /// <summary>The divider is the restore grip of a minimized pane (the engine's layout).</summary>
    [Then("the {word} divider of {string} is a restore grip")]
    public async Task Then_the_divider_is_a_grip(string kind, string name)
    {
        var grip = await OnUIThreadAsync(() => Divider(name, kind).IsRestoreGrip).ConfigureAwait(false);
        grip.Should().BeTrue("the {0} divider of \"{1}\" must be a restore grip", kind, name);
    }

    /// <summary>The control's four weights.</summary>
    [Then("the weights of {string} are {double}, {double}, {double} and {double}")]
    public async Task Then_the_weights_are(string name, double side, double stack, double upper, double lower)
    {
        var weights = await OnUIThreadAsync(() => Tri(name)).ConfigureAwait(false);
        var actual = await OnUIThreadAsync(() => (weights.SidePanePercent, weights.StackPercent, weights.UpperPanePercent, weights.LowerPanePercent)).ConfigureAwait(false);
        actual.Should().Be((side, stack, upper, lower));
    }

    /// <summary>The side pane's share of the width, from the weights.</summary>
    [Then("the side pane of {string} is wider than {int} percent")]
    public async Task Then_the_side_pane_is_wider(string name, int percent)
    {
        var side = await OnUIThreadAsync(() => Tri(name).SidePanePercent / (Tri(name).SidePanePercent + Tri(name).StackPercent) * 100).ConfigureAwait(false);
        side.Should().BeGreaterThan(percent);
    }

    /// <summary>The upper pane's share of the stack's height, from the weights.</summary>
    [Then("the upper pane of {string} is narrower than {int} percent")]
    public async Task Then_the_upper_pane_is_narrower(string name, int percent)
    {
        var upper = await OnUIThreadAsync(() => Tri(name).UpperPanePercent / (Tri(name).UpperPanePercent + Tri(name).LowerPanePercent) * 100).ConfigureAwait(false);
        upper.Should().BeLessThan(percent);
    }

    /// <summary>How many divider drags the handler took natively.</summary>
    [Then("the TriPaneView {string} took {int} divider drag natively")]
    [Then("the TriPaneView {string} took {int} divider drags natively")]
    public async Task Then_native_drags(string name, int count)
    {
        var taken = await OnUIThreadAsync(() => (PolicyDiagnostics.HandlerOf(Tri(name)) as TriPaneViewHandler)?.NativeDragCount ?? -1).ConfigureAwait(false);
        taken.Should().Be(count);
    }

    /// <summary>How many times the control raised DividerDragCompleted.</summary>
    [Then("the TriPaneView {string} raised DividerDragCompleted {int} time")]
    [Then("the TriPaneView {string} raised DividerDragCompleted {int} times")]
    public void Then_completed(string name, int count) => Completed[name].Should().Be(count);

    /// <summary>A finger presses beside a divider (across its axis), drags along it and lifts.</summary>
    [When("a real finger drags the {word} divider of {string} by {int} dp, starting {int} dp beside it")]
    public async Task When_a_finger_drags(string kind, string name, int distance, int beside)
    {
        var isSide = kind == "side";
        var (x, y, scale) = await CentreAsync(name, kind).ConfigureAwait(false);
        var off = (int)Math.Round(beside * scale);
        var (x0, y0) = isSide ? (x + off, y - (int)(100 * scale)) : (x + (int)(100 * scale), y - off);
        var d = (int)Math.Round(distance * scale);
        await DragAsync(x0, y0, isSide ? x0 + d : x0, isSide ? y0 : y0 + d, AMotionEventToolType.Finger).ConfigureAwait(false);
    }

    /// <summary>A finger drags the stack divider down to the control's bottom edge.</summary>
    [When("a real finger drags the stack divider of {string} to the bottom of {string}")]
    public async Task When_a_finger_drags_to_the_bottom(string name, string container)
    {
        var (x, y, _) = await CentreAsync(name, "stack").ConfigureAwait(false);
        var bottom = await OnUIThreadAsync(() => PixelBounds(Tri(container)).Bottom - 1).ConfigureAwait(false);
        await DragAsync(x, y, x, bottom, AMotionEventToolType.Finger).ConfigureAwait(false);
    }

    /// <summary>A finger taps the middle of a divider (no movement).</summary>
    [When("a real finger taps the {word} divider of {string}")]
    public async Task When_a_finger_taps(string kind, string name)
    {
        var (x, y, _) = await CentreAsync(name, kind).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Touch(Obtain(down, down, AMotionEventActions.Down, x, y, AMotionEventToolType.Finger));
            Touch(Obtain(down, down + 60, AMotionEventActions.Up, x, y, AMotionEventToolType.Finger));
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>A mouse presses its primary button exactly on a divider, drags and releases.</summary>
    [When("a real mouse drags the {word} divider of {string} by {int} dp")]
    public async Task When_a_mouse_drags(string kind, string name, int distance)
    {
        var (x, y, scale) = await CentreAsync(name, kind).ConfigureAwait(false);
        var d = (int)Math.Round(distance * scale);
        await DragAsync(x, y, kind == "side" ? x + d : x, kind == "side" ? y : y + d, AMotionEventToolType.Mouse).ConfigureAwait(false);
    }

    private static async Task DragAsync(int x0, int y0, int x1, int y1, AMotionEventToolType tool)
    {
        const int steps = 12;
        var down = 0L;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            down = ASystemClock.UptimeMillis();
            Touch(Obtain(down, down, AMotionEventActions.Down, x0, y0, tool));
        }).ConfigureAwait(false);
        for (var step = 1; step <= steps; step++)
        {
            var s = step;
            await TestTargetFixture.RunOnUIThreadAsync(() =>
                Touch(Obtain(down, down + (s * 16), AMotionEventActions.Move, x0 + ((x1 - x0) * s / steps), y0 + ((y1 - y0) * s / steps), tool))).ConfigureAwait(false);
        }

        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(down, down + ((steps + 1) * 16), AMotionEventActions.Up, x1, y1, tool))).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    private static async Task<(int X, int Y, double Scale)> CentreAsync(string name, string kind) =>
        await OnUIThreadAsync(() =>
        {
            var divider = Divider(name, kind);
            var bounds = PixelBounds(divider);
            return (bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2), divider.XamlRoot?.RasterizationScale ?? 1d);
        }).ConfigureAwait(false);

    /// <summary>An element's bounds in window pixels (the activity's content view origin + DIPs x the real density).</summary>
    private static (int Left, int Top, int Width, int Height, int Bottom) PixelBounds(FrameworkElement element)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1d;
        var bounds = element.TransformToVisual(null).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
        var location = new int[2];
        AppHost.Activity!.FindViewById(global::Android.Resource.Id.Content)?.GetLocationInWindow(location);
        var left = (int)Math.Round(bounds.X * scale) + location[0];
        var top = (int)Math.Round(bounds.Y * scale) + location[1];
        var height = (int)Math.Round(bounds.Height * scale);
        return (left, top, (int)Math.Round(bounds.Width * scale), height, top + height);
    }

    private static void Touch(AMotionEvent e)
    {
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static AMotionEvent Obtain(long downTime, long eventTime, AMotionEventActions action, int x, int y, AMotionEventToolType tool)
    {
        var mouse = tool == AMotionEventToolType.Mouse;
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = tool };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        var buttons = mouse && action != AMotionEventActions.Up ? AMotionEventButtonState.Primary : 0;
        var source = mouse ? AInputSourceType.Mouse : AInputSourceType.Touchscreen;
        return AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, buttons, 1f, 1f, 1, 0, source, 0)!;
    }

    private static TriPaneView Tri(string name) => (TriPaneView)ElementRegistry.Resolve(name);

    private static TriPaneViewDivider Divider(string name, string kind) =>
        Descendants(Tri(name)).OfType<TriPaneViewDivider>().First(d => d.Name == (kind == "side" ? "PART_SideDivider" : "PART_StackDivider"));

    private static string Weights(TriPaneView view) =>
        $"{view.SidePanePercent:0.##}/{view.StackPercent:0.##}/{view.UpperPanePercent:0.##}/{view.LowerPanePercent:0.##}";

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    private static async Task SettleAsync()
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(250).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
