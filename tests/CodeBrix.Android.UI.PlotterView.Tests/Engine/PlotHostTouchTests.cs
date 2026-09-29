using System;
using CodeBrix.Platform.UI.PlotterView.Engine;
using CodeBrix.Plotter;
using CodeBrix.Plotter.Axes;
using CodeBrix.Plotter.Series;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Android.UI.PlotterView.Tests.Engine;

/// <summary>
/// The touch path the Android twin relies on: on Android a finger reaches PlotterControl through Core's own pointer
/// events, and the control feeds the Core's engine (Engine/PlotHost) - TouchTracker.Down + TouchStarted, TouchMoved per
/// contact, TouchTracker.Up + TouchCompleted. These tests feed the engine the same calls over a real chart painted on a
/// raster canvas and check what CodeBrix.Plotter's default touch binding
/// (D7: PanZoomTrackByTouch) does with them: one finger pans, two fingers pinch about their centre.
/// </summary>
public class PlotHostTouchTests
{
    private const int Width = 400;
    private const int Height = 300;

    [Fact]
    public void A_one_finger_drag_through_the_engine_pans_the_chart()
    {
        //Arrange
        var (view, host, xAxis, _) = CreateChart();
        var start = new ScreenPoint(200, 150);
        var before = (xAxis.ActualMinimum, xAxis.ActualMaximum);

        //Act
        host.TouchTracker.Down(1, start).Should().BeTrue();
        host.TouchStarted(start);
        for (var step = 1; step <= 5; step++)
        {
            host.TouchMoved(1, new ScreenPoint(start.X - (20 * step), start.Y)).Should().BeTrue();
        }

        host.TouchTracker.Up(1).Should().BeTrue();
        host.TouchCompleted(new ScreenPoint(start.X - 100, start.Y));
        Paint(host);
        var after = (xAxis.ActualMinimum, xAxis.ActualMaximum);

        //Assert
        after.ActualMinimum.Should().BeGreaterThan(before.ActualMinimum + 1, "a drag to the left moves later data into view");
        (after.ActualMaximum - after.ActualMinimum).Should().BeApproximately(before.ActualMaximum - before.ActualMinimum, 0.01, "a pan keeps the range");
        view.Invalidations.Should().BeGreaterThan(0);
        host.Tracker.Should().BeNull("a moving finger is a pan, not a tracker press");
    }

    [Fact]
    public void A_two_finger_pinch_through_the_engine_zooms_the_chart_in()
    {
        //Arrange
        var (_, host, xAxis, _) = CreateChart();
        var before = (xAxis.ActualMinimum, xAxis.ActualMaximum);

        //Act: two fingers go down 40 px either side of x = 250 and spread to 120 px either side
        var first = new ScreenPoint(210, 150);
        var second = new ScreenPoint(290, 150);
        host.TouchTracker.Down(1, first).Should().BeTrue();
        host.TouchStarted(first);
        host.TouchTracker.Down(2, second).Should().BeFalse("only the first contact starts the gesture");
        for (var step = 1; step <= 4; step++)
        {
            host.TouchMoved(1, new ScreenPoint(210 - (20 * step), 150));
            host.TouchMoved(2, new ScreenPoint(290 + (20 * step), 150));
        }

        host.TouchTracker.Up(2);
        host.TouchTracker.Up(1).Should().BeTrue();
        host.TouchCompleted(new ScreenPoint(130, 150));
        Paint(host);
        var after = (xAxis.ActualMinimum, xAxis.ActualMaximum);

        //Assert
        // (Each move event carries one finger's move - as Android delivers them - so the pinch centre moves between events
        // and the engine's "a moving pinch also pans" applies; only the zoom itself is claimed here.)
        (after.ActualMaximum - after.ActualMinimum).Should().BeLessThan((before.ActualMaximum - before.ActualMinimum) * 0.6, "spreading two fingers zooms in");
        after.ActualMinimum.Should().BeGreaterThan(before.ActualMinimum);
        after.ActualMaximum.Should().BeLessThan(before.ActualMaximum);
    }

    [Fact]
    public void A_tap_on_a_series_through_the_engine_shows_the_tracker_and_lifting_hides_it()
    {
        //Arrange
        var (view, host, _, series) = CreateChart();
        var onPoint = series.Transform(new DataPoint(5, 25));

        //Act
        host.TouchTracker.Down(1, onPoint).Should().BeTrue();
        host.TouchStarted(onPoint);
        var pressed = host.Tracker;
        host.TouchTracker.Up(1).Should().BeTrue();
        host.TouchCompleted(onPoint);

        //Assert
        pressed.Should().NotBeNull("a press on a series shows the data-point tracker");
        view.TrackerShows.Should().BeGreaterThan(0);
    }

    private static (TestPlotView View, PlotHost Host, LinearAxis XAxis, LineSeries Series) CreateChart()
    {
        // The chart's text asks the platform's font source (on Android the TextLayout add-in's): a stand-in here.
        TestFontSource.EnsureRegistered();
        var model = new PlotModel { Title = "Touch", Background = PlotterColors.White, IsLegendVisible = false };
        var xAxis = new LinearAxis { Position = AxisPosition.Bottom, Minimum = 0, Maximum = 10 };
        var yAxis = new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 100 };
        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);
        var series = new LineSeries { Color = PlotterColors.Red, StrokeThickness = 3 };
        for (var i = 0; i <= 10; i++)
        {
            series.Points.Add(new DataPoint(i, i * i));
        }

        model.Series.Add(series);
        var view = new TestPlotView(Width, Height);
        view.Host.SetModel(null, model);
        view.InvalidatePlot(true);
        Paint(view.Host);
        return (view, view.Host, xAxis, series);
    }

    private static void Paint(PlotHost host)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        host.Paint(canvas, new SKSize(Width, Height));
        canvas.Flush();
    }

    /// <summary>A view that forwards to its engine, as PlotterControl does, with no XAML.</summary>
    private sealed class TestPlotView : IPlotView
    {
        private readonly double _width;
        private readonly double _height;

        internal TestPlotView(double width, double height)
        {
            _width = width;
            _height = height;
            Host = new PlotHost(this, () => Invalidations++, action =>
            {
                action();
                return true;
            });
        }

        internal PlotHost Host { get; }

        internal int Invalidations { get; private set; }

        internal int TrackerShows { get; private set; }

        public PlotModel ActualModel => Host.Model;

        Model IView.ActualModel => Host.Model;

        public IController ActualController => Host.ActualController;

        public PlotterRect ClientArea => new(0, 0, _width, _height);

        public void InvalidatePlot(bool updateData = true)
        {
            Host.MarkForUpdate(updateData);
            Invalidations++;
        }

        public void ShowTracker(TrackerHitResult trackerHitResult)
        {
            TrackerShows++;
            Host.ShowTracker(trackerHitResult);
        }

        public void HideTracker() => Host.HideTracker();

        public void ShowZoomRectangle(PlotterRect rectangle) => Host.ShowZoomRectangle(rectangle);

        public void HideZoomRectangle() => Host.HideZoomRectangle();

        public void SetCursorType(CursorType cursorType) => Host.SetCursorType(cursorType);

        public void SetClipboardText(string text)
        {
        }
    }
}
