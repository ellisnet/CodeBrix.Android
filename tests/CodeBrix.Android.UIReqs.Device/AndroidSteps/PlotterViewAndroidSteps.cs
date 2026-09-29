#nullable disable

using System;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.PlotterView.Android;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.AddIn.PlotterView.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.PlotterView;
using Microsoft.UI.Xaml;
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
/// The steps of the Android-only "The PlotterView on Android" feature (AndroidFeatures/AndroidPlotterView): the PlotterView
/// add-in's Android canvas supply, and REAL MotionEvents (one finger, two fingers, the mouse wheel) dispatched to the
/// activity, reaching the chart through the framework's input router with CodeBrix.Plotter's default touch binding.
/// </summary>
[Binding]
public sealed class PlotterViewAndroidSteps
{
    private static (double Minimum, double Maximum) _remembered;

    /// <summary>Shows a 900 x 600 chart of one of the copied PlotterView group's models, with the engine's default controller.</summary>
    [Given("the application shows a Plotter named {string} with the {string} model and the default controller")]
    public async Task Given_a_plotter(string name, string model)
    {
        ElementRegistry.Clear();
        PlotterControl plot = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            plot = new PlotterControl
            {
                Name = name,
                Width = 900,
                Height = 600,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Model = PlotterModels.Build(model),
            };
            ElementRegistry.Register(name, plot);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(plot).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The chart's drawing surface is the add-in's Canvas, shown by its handler as a native Skia view.</summary>
    [Then("the drawing surface of the Plotter {string} is the Android plotter canvas")]
    public async Task Then_the_surface_is_the_Android_canvas(string name)
    {
        (string surface, string handler, string view, bool registered) = await OnUIThreadAsync(() =>
        {
            var canvas = Surface(name);
            var handler = canvas == null ? null : PolicyDiagnostics.HandlerOf(canvas);
            return ((string)canvas?.GetType().Name, (string)handler?.GetType().Name, (handler as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView?.GetType().Name,
                AndroidPlatformBootstrap.CanvasPlatform != null);
        }).ConfigureAwait(false);
        registered.Should().BeTrue("the Android add-in registered the chart's canvas supply");
        surface.Should().Be(nameof(PlotterCanvasElement));
        handler.Should().Be(nameof(PlotterCanvasHandler));
        view.Should().Be("SkiaCanvasView");
    }

    /// <summary>The native view ran the engine's paint.</summary>
    [Then("the drawing surface of the Plotter {string} has painted")]
    public async Task Then_the_surface_has_painted(string name)
    {
        var paints = 0;
        for (var attempt = 0; attempt < 30 && paints == 0; attempt++)
        {
            paints = await OnUIThreadAsync(() => Surface(name).PaintCount).ConfigureAwait(false);
            if (paints == 0)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        paints.Should().BeGreaterThan(0, "the chart's native surface must run the engine's paint");
    }

    /// <summary>Remembers the x axis's visible range.</summary>
    [When("the axes of the Plotter {string} are remembered")]
    public async Task When_axes_remembered(string name) => _remembered = await OnUIThreadAsync(() => XRange(name)).ConfigureAwait(false);

    /// <summary>One real finger drags horizontally through the middle of the plot area.</summary>
    [When("a real finger drags {int} pixels to the left across the Plotter {string}")]
    public async Task When_finger_drags(int distance, string name)
    {
        var (x, y) = await ScreenCentreAsync(name).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            var t = down;
            Touch(Obtain(down, t, AMotionEventActions.Down, (0, x + (distance / 2), y)));
            const int steps = 10;
            for (var step = 1; step <= steps; step++)
            {
                t += 16;
                Touch(Obtain(down, t, AMotionEventActions.Move, (0, x + (distance / 2) - (distance * step / steps), y)));
            }

            Touch(Obtain(down, t + 16, AMotionEventActions.Up, (0, x - (distance / 2), y)));
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Two real fingers go down either side of the plot's centre and spread apart (both move in each event).</summary>
    [When("two real fingers spread {int} pixels apart across the Plotter {string}")]
    public async Task When_two_fingers_spread(int spread, string name)
    {
        var (x, y) = await ScreenCentreAsync(name).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            const int start = 60;
            var down = ASystemClock.UptimeMillis();
            var t = down;
            Touch(Obtain(down, t, AMotionEventActions.Down, (0, x - start, y)));
            t += 16;
            Touch(Obtain(down, t, PointerAction(AMotionEventActions.PointerDown, 1), (0, x - start, y), (1, x + start, y)));
            const int steps = 10;
            var half = spread / 2;
            for (var step = 1; step <= steps; step++)
            {
                t += 16;
                var d = half * step / steps;
                Touch(Obtain(down, t, AMotionEventActions.Move, (0, x - start - d, y), (1, x + start + d, y)));
            }

            t += 16;
            Touch(Obtain(down, t, PointerAction(AMotionEventActions.PointerUp, 1), (0, x - start - half, y), (1, x + start + half, y)));
            Touch(Obtain(down, t + 16, AMotionEventActions.Up, (0, x - start - half, y)));
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>One real finger goes down on the centre of the plot area (the Line model's data point x = 5) and stays.</summary>
    [When("a real finger is held on the centre of the plot area of the Plotter {string}")]
    public async Task When_finger_held(string name)
    {
        var (x, y) = await ScreenCentreAsync(name).ConfigureAwait(false);
        _held = (x, y, ASystemClock.UptimeMillis());
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(_held.Down, _held.Down, AMotionEventActions.Down, (0, _held.X, _held.Y)))).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The held finger lifts where it went down.</summary>
    [When("the real finger on the Plotter {string} is lifted")]
    public async Task When_finger_lifted(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(_held.Down, ASystemClock.UptimeMillis(), AMotionEventActions.Up, (0, _held.X, _held.Y)))).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The chart's engine is showing its data-point tracker.</summary>
    [Then("the Plotter {string} shows its tracker")]
    public async Task Then_shows_tracker(string name) =>
        (await OnUIThreadAsync(() => Tracker(name) != null).ConfigureAwait(false)).Should().BeTrue("a finger held on a series shows the tracker (default touch binding)");

    /// <summary>The chart's engine shows no tracker.</summary>
    [Then("the Plotter {string} shows no tracker")]
    public async Task Then_shows_no_tracker(string name) =>
        (await OnUIThreadAsync(() => Tracker(name) == null).ConfigureAwait(false)).Should().BeTrue("lifting the finger hides the tracker");

    /// <summary>A real mouse wheel notch (away from the user) over the middle of the chart.</summary>
    [When("the real mouse wheel turns one notch up over the centre of the Plotter {string}")]
    public async Task When_mouse_wheel(string name)
    {
        var (x, y) = await ScreenCentreAsync(name).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var time = ASystemClock.UptimeMillis();
            var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = AMotionEventToolType.Mouse };
            var coords = new AMotionEvent.PointerCoords { X = x, Y = y };
            coords.SetAxisValue(AAxis.Vscroll, 1f);
            var e = AMotionEvent.Obtain(time, time, AMotionEventActions.Scroll, 1, new[] { properties }, new[] { coords }, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Mouse, 0);
            AppHost.Activity.DispatchGenericMotionEvent(e);
            e.Recycle();
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The x axis moved to later data (the content followed a finger moving left) and kept its width.</summary>
    [Then("the x-axis of the Plotter {string} has moved right, keeping its range")]
    public async Task Then_moved_right(string name)
    {
        var now = await OnUIThreadAsync(() => XRange(name)).ConfigureAwait(false);
        now.Minimum.Should().BeGreaterThan(_remembered.Minimum + 0.1, "a finger dragged to the left pans the chart to later data");
        (now.Maximum - now.Minimum).Should().BeApproximately(_remembered.Maximum - _remembered.Minimum, 0.01, "a pan keeps the range");
    }

    /// <summary>The x axis shows less than before.</summary>
    [Then("the x-axis range of the Plotter {string} is narrower than before")]
    public async Task Then_narrower(string name)
    {
        var now = await OnUIThreadAsync(() => XRange(name)).ConfigureAwait(false);
        (now.Maximum - now.Minimum).Should().BeLessThan((_remembered.Maximum - _remembered.Minimum) * 0.95, "the chart zoomed in");
    }

    private static (int X, int Y, long Down) _held;

    private static AMotionEventActions PointerAction(AMotionEventActions action, int index) =>
        (AMotionEventActions)((int)action | (index << 8)); // ACTION_POINTER_INDEX_SHIFT = 8

    private static (double Minimum, double Maximum) XRange(string name)
    {
        var axis = Plot(name).ActualModel.DefaultXAxis;
        return (axis.ActualMinimum, axis.ActualMaximum);
    }

    private static object Tracker(string name)
    {
        var host = typeof(PlotterControl).GetField("_host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(Plot(name));
        return host?.GetType().GetProperty("Tracker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(host);
    }

    /// <summary>The middle of the chart's plot area in decor-view pixels (Activity.DispatchTouchEvent coordinates).</summary>
    private static async Task<(int X, int Y)> ScreenCentreAsync(string name) => await OnUIThreadAsync(() =>
    {
        var plot = Plot(name);
        var area = plot.ActualModel.PlotArea;
        var activity = (CodeBrix.Android.UI.Hosting.CodeBrixActivity)AppHost.Activity;
        var scale = plot.XamlRoot?.RasterizationScale ?? 1d;
        var point = plot.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(area.Left + (area.Width / 2), area.Top + (area.Height / 2)));
        var content = new int[2];
        activity.RootLayout.ContentLayer.GetLocationOnScreen(content);
        var decor = new int[2];
        activity.Window.DecorView.GetLocationOnScreen(decor);
        return ((int)Math.Round(point.X * scale) + content[0] - decor[0], (int)Math.Round(point.Y * scale) + content[1] - decor[1]);
    }).ConfigureAwait(false);

    private static void Touch(AMotionEvent e)
    {
        AppHost.Activity.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static AMotionEvent Obtain(long downTime, long eventTime, AMotionEventActions action, params (int Id, int X, int Y)[] pointers)
    {
        var properties = new AMotionEvent.PointerProperties[pointers.Length];
        var coords = new AMotionEvent.PointerCoords[pointers.Length];
        for (var i = 0; i < pointers.Length; i++)
        {
            properties[i] = new AMotionEvent.PointerProperties { Id = pointers[i].Id, ToolType = AMotionEventToolType.Finger };
            coords[i] = new AMotionEvent.PointerCoords { X = pointers[i].X, Y = pointers[i].Y, Pressure = 1f, Size = 1f };
        }

        return AMotionEvent.Obtain(downTime, eventTime, action, pointers.Length, properties, coords, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Touchscreen, 0);
    }

    private static PlotterControl Plot(string name) => (PlotterControl)ElementRegistry.Resolve(name);

    private static PlotterCanvasElement Surface(string name) => Find<PlotterCanvasElement>(Plot(name));

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
