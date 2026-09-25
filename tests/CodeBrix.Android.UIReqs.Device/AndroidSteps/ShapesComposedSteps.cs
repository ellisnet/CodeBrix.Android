using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Reqnroll;
using SilverAssertions;
using AImeAction = Android.Views.InputMethods.ImeAction;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using ASystemClock = Android.OS.SystemClock;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using AViewStates = Android.Views.ViewStates;
using WColor = Windows.UI.Color;
using XamlCanvas = Microsoft.UI.Xaml.Controls.Canvas;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP6: the steps of the Android-only groups AndroidShapes (native shape drawing, Viewbox, Border) and
/// AndroidComposed (Expander, ColorPicker, the Material date and time pickers).
/// </summary>
[Binding]
public sealed class ShapesComposedSteps
{
    private const int StageWidth = 400;
    private const int StageHeight = 240;
    private static readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    private static MaterialDatePickerFlyout? _dateFlyout;
    private static MaterialTimePickerFlyout? _timeFlyout;
    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the steps for one scenario.</summary>
    /// <param name="scenarioContext">The scenario.</param>
    public ShapesComposedSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>Follows the Material pickers the scenario app shows (for the whole run).</summary>
    [BeforeTestRun(Order = 60)]
    public static void Follow_the_Material_pickers()
    {
        MaterialDatePickerFlyout.Shown += (_, flyout) => _dateFlyout = flyout;
        MaterialTimePickerFlyout.Shown += (_, flyout) => _timeFlyout = flyout;
    }

    /// <summary>
    /// The copied Navigation/Expander scenarios pin the Fluent template's parts (ExpanderHeader, ExpanderContentClip),
    /// so the run starts with the native Expander OFF - it is ON by default in apps since pin 1.0.268.12 (WPE1-1 C0d),
    /// as the native ComboBox / Material overlays are kept off for the copied groups.
    /// </summary>
    [BeforeTestRun(Order = 61)]
    public static void Keep_the_copied_Expanders_on_their_template() => ExpanderHandler.Enabled = false;

    /// <summary>Uses the native Expander (the app default; off for the copied scenarios) for the Expanders this scenario builds.</summary>
    [Given("native Expanders are used")]
    public static void Given_native_Expanders_are_used() => ExpanderHandler.Enabled = true;

    /// <summary>Forgets the counters and pickers after every scenario; the native Expander switch goes back off.</summary>
    [AfterScenario]
    public static void Forget_counters_and_pickers()
    {
        ExpanderHandler.Enabled = false;
        Counts.Clear();
        _dateFlyout = null;
        _timeFlyout = null;
    }

    // ---------------------------------------------------------------- shapes

    /// <summary>Shows one shape of every kind, each registered by name.</summary>
    [Given("the application shows one shape of every kind")]
    public async Task Given_one_shape_of_every_kind()
    {
        await ShowStageAsync(stage =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            panel.Children.Add(Named("rect", new Rectangle { Width = 50, Height = 40, Fill = Brush("Red"), RadiusX = 6, RadiusY = 6 }));
            panel.Children.Add(Named("oval", new Ellipse { Width = 50, Height = 40, Fill = Brush("Blue") }));
            panel.Children.Add(Named("rule", new Line { X1 = 0, Y1 = 20, X2 = 50, Y2 = 20, Stroke = Brush("Black"), StrokeThickness = 4 }));
            panel.Children.Add(Named("poly", new Polygon { Points = Points("0,40 25,0 50,40"), Fill = Brush("Lime") }));
            panel.Children.Add(Named("zigzag", new Polyline { Points = Points("0,40 12,0 25,40 37,0 50,40"), Stroke = Brush("Black"), StrokeThickness = 3 }));
            panel.Children.Add(Named("figure", new XamlPath { Data = Geometry("M 0,0 L 50,0 L 25,40 Z"), Fill = Brush("Red") }));
            stage.Children.Add(panel);
        }).ConfigureAwait(false);
    }

    /// <summary>Shows a thick Line with its two figure caps.</summary>
    [Given("the stage shows a {word} Line {int} thick from {int},{int} to {int},{int} with caps {string} and {string}")]
    public async Task Given_a_Line_with_caps(string color, int thickness, int x1, int y1, int x2, int y2, string startCap, string endCap)
    {
        await ShowStageAsync(stage => stage.Children.Add(Tracked("rule", new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = Brush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = Cap(startCap),
            StrokeEndLineCap = Cap(endCap),
        }))).ConfigureAwait(false);
    }

    /// <summary>Shows a dashed Line with a dash cap.</summary>
    [Given("the stage shows a {word} Line {int} thick from {int},{int} to {int},{int} dashed {string} with dash cap {string}")]
    public async Task Given_a_dashed_Line(string color, int thickness, int x1, int y1, int x2, int y2, string dashes, string dashCap) =>
        await ShowDashedLineAsync(color, thickness, x1, y1, x2, y2, dashes, dashCap, "Flat").ConfigureAwait(false);

    /// <summary>Shows a dashed Line with a dash cap and a start cap of its own.</summary>
    [Given("the stage shows a {word} Line {int} thick from {int},{int} to {int},{int} dashed {string} with dash cap {string} and start cap {string}")]
    public async Task Given_a_dashed_Line_with_a_start_cap(string color, int thickness, int x1, int y1, int x2, int y2, string dashes, string dashCap, string startCap) =>
        await ShowDashedLineAsync(color, thickness, x1, y1, x2, y2, dashes, dashCap, startCap).ConfigureAwait(false);

    /// <summary>Shows a filled Polygon and an open Polyline of the same shape side by side.</summary>
    [Given("the stage shows a Lime Polygon beside a Blue Polyline of the same three-point shape")]
    public async Task Given_a_Polygon_beside_a_Polyline()
    {
        await ShowStageAsync(stage =>
        {
            stage.Children.Add(Named("poly", new Polygon { Points = Points("20,220 100,60 180,220"), Fill = Brush("Lime") }));
            stage.Children.Add(Named("zigzag", new Polyline { Points = Points("220,220 300,60 380,220"), Stroke = Brush("Blue"), StrokeThickness = 8 }));
        }).ConfigureAwait(false);
    }

    /// <summary>Shows a Path (the whole stage) filling a square with a horizontal gradient.</summary>
    [Given("the stage shows a Path filling the square {int},{int} to {int},{int} with a horizontal gradient from {string} to {string}")]
    public async Task Given_a_gradient_Path(int x1, int y1, int x2, int y2, string from, string to)
    {
        await ShowStageAsync(stage =>
        {
            var gradient = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0.5), EndPoint = new Windows.Foundation.Point(1, 0.5) };
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Parse(from), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Parse(to), Offset = 1 });
            stage.Children.Add(Named("figure", new XamlPath
            {
                Data = Geometry(string.Create(CultureInfo.InvariantCulture, $"M {x1},{y1} L {x2},{y1} L {x2},{y2} L {x1},{y2} Z")),
                Fill = gradient,
            }));
        }).ConfigureAwait(false);
    }

    /// <summary>Shows a small square Path stretched (Fill) to a size, with a stroke.</summary>
    [Given("the stage shows a {int} by {int} square Path stretched to {int} by {int} with a {int} thick {string} stroke over a {string} fill")]
    public async Task Given_a_stretched_Path(int dataWidth, int dataHeight, int width, int height, int thickness, string stroke, string fill)
    {
        await ShowStageAsync(stage => stage.Children.Add(Named("figure", new XamlPath
        {
            Data = Geometry(string.Create(CultureInfo.InvariantCulture, $"M 0,0 L {dataWidth},0 L {dataWidth},{dataHeight} L 0,{dataHeight} Z")),
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            Stroke = Brush(stroke),
            StrokeThickness = thickness,
            Fill = Brush(fill),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        }))).ConfigureAwait(false);
    }

    /// <summary>Shows a square Path in a larger shape with a given Stretch.</summary>
    [Given("the stage shows a {int} by {int} square Path in a {int} by {int} shape with Stretch {string} and a {string} fill")]
    public async Task Given_a_Path_with_Stretch(int dataWidth, int dataHeight, int width, int height, string stretch, string fill)
    {
        await ShowStageAsync(stage => stage.Children.Add(Named("figure", new XamlPath
        {
            Data = Geometry(string.Create(CultureInfo.InvariantCulture, $"M 0,0 L {dataWidth},0 L {dataWidth},{dataHeight} L 0,{dataHeight} Z")),
            Width = width,
            Height = height,
            Stretch = Enum.Parse<Stretch>(stretch),
            Fill = Brush(fill),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        }))).ConfigureAwait(false);
    }

    /// <summary>Shows a filled Ellipse placed in the stage, its taps counted.</summary>
    [Given("the stage shows a {word} Ellipse named {string} {int} by {int} at {int},{int}")]
    public async Task Given_an_Ellipse(string color, string name, int width, int height, int x, int y)
    {
        await ShowStageAsync(stage =>
        {
            var ellipse = Tracked(name, new Ellipse { Width = width, Height = height, Fill = Brush(color) });
            XamlCanvas.SetLeft(ellipse, x);
            XamlCanvas.SetTop(ellipse, y);
            stage.Children.Add(ellipse);
        }).ConfigureAwait(false);
    }

    /// <summary>Taps (Core-injected) a point given in an element's device pixels.</summary>
    [When("the point {int}, {int} inside {string} is tapped")]
    public async Task When_the_point_inside_is_tapped(int x, int y, string name)
    {
        var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), 0).ConfigureAwait(false);
        TestTargetFixture.Session.Tap(bounds.X + x, bounds.Y + y);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts how many Tapped events a counted shape raised.</summary>
    [Then("the {word} {string} was tapped {int} times")]
    public async Task Then_the_shape_was_tapped(string kind, string name, int times)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + ".Tapped").Should().Be(times, "the {0} \"{1}\" must have been tapped {2} times", kind, name, times);
    }

    /// <summary>Asserts which of two colours a pixel (in an element's device pixels) is nearer to.</summary>
    [Then("the pixel at {int}, {int} inside {string} is nearer {string} than {string}")]
    public async Task Then_the_pixel_is_nearer(int x, int y, string name, string nearer, string farther)
    {
        var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), 0).ConfigureAwait(false);
        var pixel = ScenarioFrames.Current(_scenarioContext).GetPixel(bounds.X + x, bounds.Y + y);
        var a = Colors.Parse(nearer);
        var b = Colors.Parse(farther);
        Distance(pixel, a).Should().BeLessThan(Distance(pixel, b),
            "the pixel at {0}, {1} inside \"{2}\" is #{3:X2}{4:X2}{5:X2}, which must be nearer {6} than {7}", x, y, name, pixel.Red, pixel.Green, pixel.Blue, nearer, farther);
    }

    /// <summary>Shows a Viewbox holding a larger grid of four coloured quarters.</summary>
    [Given("the application shows a {int} by {int} Viewbox named {string} holding a {int} by {int} grid of four colours")]
    public async Task Given_a_Viewbox(int width, int height, string name, int childWidth, int childHeight)
    {
        Viewbox box = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var grid = new Grid { Width = childWidth, Height = childHeight };
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var colors = new[] { "Red", "Lime", "Blue", "Yellow" };
            for (var i = 0; i < 4; i++)
            {
                var quarter = new Border { Background = Brush(colors[i]) };
                Grid.SetRow(quarter, i / 2);
                Grid.SetColumn(quarter, i % 2);
                grid.Children.Add(quarter);
            }

            box = new Viewbox { Name = name, Width = width, Height = height, Child = grid, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            ElementRegistry.Register(name, box);
        }).ConfigureAwait(false);
        await ShowAsync(box).ConfigureAwait(false);
    }

    /// <summary>Shows a Border with per-side thicknesses.</summary>
    [Given("the application shows a Border named {string} {int} by {int} with BorderThickness {string} in {string} over {string}")]
    public async Task Given_a_Border_with_thickness(string name, int width, int height, string thickness, string borderBrush, string background)
    {
        Border border = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            border = new Border
            {
                Name = name,
                Width = width,
                Height = height,
                BorderThickness = (Thickness)XamlBindingHelper.ConvertValue(typeof(Thickness), thickness),
                BorderBrush = Brush(borderBrush),
                Background = Brush(background),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ElementRegistry.Register(name, border);
        }).ConfigureAwait(false);
        await ShowAsync(border).ConfigureAwait(false);
    }

    /// <summary>Shows a Border with per-corner radii.</summary>
    [Given("the application shows a Border named {string} {int} by {int} with CornerRadius {string} in {string}")]
    public async Task Given_a_Border_with_corners(string name, int width, int height, string radius, string background)
    {
        Border border = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            border = new Border
            {
                Name = name,
                Width = width,
                Height = height,
                CornerRadius = (CornerRadius)XamlBindingHelper.ConvertValue(typeof(CornerRadius), radius),
                Background = Brush(background),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ElementRegistry.Register(name, border);
        }).ConfigureAwait(false);
        await ShowAsync(border).ConfigureAwait(false);
    }

    // -------------------------------------------------------------- Expander

    /// <summary>A real finger taps the native header row of an Expander.</summary>
    [When("a real finger taps the header of the Expander {string}")]
    public async Task When_a_real_finger_taps_the_header(string name)
    {
        var center = (0, 0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = (ExpanderView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;
            var location = new int[2];
            view.HeaderRow.GetLocationInWindow(location);
            center = (location[0] + (view.HeaderRow.Width / 2), location[1] + (view.HeaderRow.Height / 2));
        }).ConfigureAwait(false);
        await RealTapAsync(center.Item1, center.Item2).ConfigureAwait(false);
        await DelayAsync(300).ConfigureAwait(false);
    }

    /// <summary>Counts an Expander's Expanding and Collapsed events.</summary>
    [Given("the Expanding and Collapsed of the Expander {string} are counted")]
    public async Task Given_the_Expander_events_are_counted(string name)
    {
        Counts[name + ".Expanding"] = 0;
        Counts[name + ".Collapsed"] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var expander = (Expander)ElementRegistry.Resolve(name);
            expander.Expanding += (_, _) => Counts[name + ".Expanding"]++;
            expander.Collapsed += (_, _) => Counts[name + ".Collapsed"]++;
        }).ConfigureAwait(false);
    }

    /// <summary>Asserts the counted Expander events.</summary>
    [Then("the Expander {string} raised Expanding {int} times and Collapsed {int} times")]
    public async Task Then_the_Expander_raised(string name, int expanding, int collapsed)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + ".Expanding").Should().Be(expanding);
        Counts.GetValueOrDefault(name + ".Collapsed").Should().Be(collapsed);
    }

    /// <summary>Shows an Expander whose header is an element (a TextBlock).</summary>
    [Given("the application shows an Expander named {string} whose header is a TextBlock")]
    public async Task Given_an_Expander_with_an_element_header(string name)
    {
        Expander expander = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            expander = new Expander { Name = name, Width = 400, Header = new TextBlock { Text = "Element header" }, Content = new TextBlock { Text = "Body" }, VerticalAlignment = VerticalAlignment.Top };
            ElementRegistry.Register(name, expander);
        }).ConfigureAwait(false);
        await ShowAsync(expander).ConfigureAwait(false);
    }

    /// <summary>Asserts that no view of an element (or inside it) is of a native type.</summary>
    [Then("{string} is not shown by a native {word}")]
    public async Task Then_is_not_shown_by_a_native(string name, string widget)
    {
        var found = new List<string>();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var view in Views(ElementRegistry.Resolve(name)))
            {
                for (var type = view.GetType(); type != null && type != typeof(Java.Lang.Object); type = type.BaseType)
                {
                    found.Add(type.Name);
                }
            }
        }).ConfigureAwait(false);
        found.Should().NotContain(widget);
    }

    // ----------------------------------------------------------- ColorPicker

    /// <summary>Shows a ColorPicker with alpha and every text input (as Pinta's colour dialog asks for).</summary>
    [Given("the application shows a ColorPicker named {string} with alpha and every text input")]
    public async Task Given_a_ColorPicker_with_alpha(string name) => await ShowColorPickerAsync(name, alpha: true).ConfigureAwait(false);

    /// <summary>Shows a ColorPicker without alpha.</summary>
    [Given("the application shows a ColorPicker named {string} without alpha")]
    public async Task Given_a_ColorPicker_without_alpha(string name) => await ShowColorPickerAsync(name, alpha: false).ConfigureAwait(false);

    /// <summary>Sets a ColorPicker's Color from Core.</summary>
    [When("the Color of the ColorPicker {string} is set to {string}")]
    public async Task When_the_Color_is_set(string name, string hex)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((ColorPicker)ElementRegistry.Resolve(name)).Color = Hex(hex)).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the text of a ColorPicker's native hex field.</summary>
    [Then("the hex field of the ColorPicker {string} shows {string}")]
    public async Task Then_the_hex_field_shows(string name, string text)
    {
        var shown = string.Empty;
        await TestTargetFixture.RunOnUIThreadAsync(() => shown = PickerView(name).HexEditor.Text ?? string.Empty).ConfigureAwait(false);
        shown.Should().Be(text);
    }

    /// <summary>Asserts the texts of a ColorPicker's native channel fields.</summary>
    [Then("the channel fields of the ColorPicker {string} show {int}, {int}, {int} and {int} percent")]
    public async Task Then_the_channel_fields_show(string name, int red, int green, int blue, int alphaPercent)
    {
        var texts = new string[4];
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = PickerView(name);
            texts[0] = view.RedField.Text ?? string.Empty;
            texts[1] = view.GreenField.Text ?? string.Empty;
            texts[2] = view.BlueField.Text ?? string.Empty;
            texts[3] = view.AlphaEditor.Text ?? string.Empty;
        }).ConfigureAwait(false);
        texts.Should().Equal(Text(red), Text(green), Text(blue), Text(alphaPercent));
    }

    /// <summary>Counts a ColorPicker's ColorChanged events.</summary>
    [Given("the ColorChanged of the ColorPicker {string} is counted")]
    public async Task Given_the_ColorChanged_is_counted(string name)
    {
        Counts[name + ".ColorChanged"] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            ((ColorPicker)ElementRegistry.Resolve(name)).ColorChanged += (_, _) => Counts[name + ".ColorChanged"]++).ConfigureAwait(false);
    }

    /// <summary>A real finger taps a ColorPicker's spectrum at a place given as percentages.</summary>
    [When("a real finger taps the spectrum of the ColorPicker {string} at {int} percent across and {int} percent down")]
    public async Task When_a_real_finger_taps_the_spectrum(string name, int across, int down)
    {
        var point = (0, 0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var spectrum = PickerView(name).Spectrum;
            var location = new int[2];
            spectrum.GetLocationInWindow(location);
            point = (location[0] + (int)Math.Round(spectrum.Width * across / 100.0), location[1] + (int)Math.Round(spectrum.Height * down / 100.0));
        }).ConfigureAwait(false);
        await RealTapAsync(point.Item1, point.Item2).ConfigureAwait(false);
    }

    /// <summary>Asserts a ColorPicker's Color within a few steps per channel.</summary>
    [Then("the Color of the ColorPicker {string} is near {int}, {int}, {int}")]
    public async Task Then_the_Color_is_near(string name, int red, int green, int blue)
    {
        var color = default(WColor);
        await TestTargetFixture.RunOnUIThreadAsync(() => color = ((ColorPicker)ElementRegistry.Resolve(name)).Color).ConfigureAwait(false);
        ((int)color.R).Should().BeInRange(red - 10, red + 10);
        ((int)color.G).Should().BeInRange(green - 10, green + 10);
        ((int)color.B).Should().BeInRange(blue - 10, blue + 10);
    }

    /// <summary>Asserts a ColorPicker's exact Color.</summary>
    [Then("the Color of the ColorPicker {string} is {string}")]
    public async Task Then_the_Color_is(string name, string hex)
    {
        var color = default(WColor);
        await TestTargetFixture.RunOnUIThreadAsync(() => color = ((ColorPicker)ElementRegistry.Resolve(name)).Color).ConfigureAwait(false);
        color.Should().Be(Hex(hex));
    }

    /// <summary>Asserts that a ColorPicker raised ColorChanged.</summary>
    [Then("the ColorPicker {string} raised ColorChanged at least once")]
    public async Task Then_the_ColorPicker_raised_ColorChanged(string name)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + ".ColorChanged").Should().BeGreaterThan(0);
    }

    /// <summary>Types hex text into a ColorPicker's native hex field and presses the IME's Done.</summary>
    [When("{string} is entered into the hex field of the ColorPicker {string}")]
    public async Task When_hex_is_entered(string text, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var editor = PickerView(name).HexEditor;
            editor.RequestFocus();
            editor.Text = text;
            editor.OnEditorAction(AImeAction.Done);
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts that a ColorPicker's alpha slider is not shown.</summary>
    [Then("the alpha slider of the ColorPicker {string} is hidden")]
    public async Task Then_the_alpha_slider_is_hidden(string name)
    {
        var state = AViewStates.Visible;
        await TestTargetFixture.RunOnUIThreadAsync(() => state = PickerView(name).AlphaSlider.Visibility).ConfigureAwait(false);
        state.Should().Be(AViewStates.Gone);
    }

    // ------------------------------------------------------------- pickers

    /// <summary>Shows a DatePicker on a day.</summary>
    [Given("the application shows a DatePicker named {string} on {int}-{int}-{int}")]
    public async Task Given_a_DatePicker(string name, int year, int month, int day)
    {
        DatePicker picker = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            picker = new DatePicker { Name = name, Date = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero), VerticalAlignment = VerticalAlignment.Top };
            ElementRegistry.Register(name, picker);
        }).ConfigureAwait(false);
        await ShowAsync(picker).ConfigureAwait(false);
    }

    /// <summary>Counts a DatePicker's DateChanged events.</summary>
    [Given("the DateChanged of the DatePicker {string} is counted")]
    public async Task Given_the_DateChanged_is_counted(string name)
    {
        Counts[name + ".DateChanged"] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            ((DatePicker)ElementRegistry.Resolve(name)).DateChanged += (_, _) => Counts[name + ".DateChanged"]++).ConfigureAwait(false);
    }

    /// <summary>Asserts that the Material date picker dialog is showing.</summary>
    [Then("a Material date picker is showing")]
    public async Task Then_a_Material_date_picker_is_showing()
    {
        var showing = await PollAsync(() => _dateFlyout?.Dialog is { IsAdded: true, Dialog.IsShowing: true }).ConfigureAwait(false);
        showing.Should().BeTrue("a MaterialDatePicker dialog must be showing");
    }

    /// <summary>Asserts that the Material time picker dialog is showing.</summary>
    [Then("a Material time picker is showing")]
    public async Task Then_a_Material_time_picker_is_showing()
    {
        var showing = await PollAsync(() => _timeFlyout?.Dialog is { IsAdded: true, Dialog.IsShowing: true }).ConfigureAwait(false);
        showing.Should().BeTrue("a MaterialTimePicker dialog must be showing");
    }

    /// <summary>Presses the showing Material picker's OK (confirm) button.</summary>
    [When("the Material picker's OK button is pressed")]
    public async Task When_the_OK_button_is_pressed()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            AView? root = _dateFlyout?.Dialog?.View ?? _timeFlyout?.Dialog?.View;
            var ok = FindButton(root!, "confirm_button") ?? FindButton(root!, "material_timepicker_ok_button");
            ok!.PerformClick();
        }).ConfigureAwait(false);
        await DelayAsync(500).ConfigureAwait(false);
    }

    /// <summary>Chooses a day the way the dialog's OK does (the Material picker's selection cannot be set once built).</summary>
    [When("the Material date picker chooses {int}-{int}-{int}")]
    public async Task When_the_date_picker_chooses(int year, int month, int day)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var flyout = _dateFlyout!;
            flyout.Pick(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
            flyout.Dialog!.Dismiss();
        }).ConfigureAwait(false);
        await DelayAsync(500).ConfigureAwait(false);
    }

    /// <summary>Sets the Material time picker's hour and minute and presses its OK button.</summary>
    [When("the Material time picker is confirmed at {int}:{int}")]
    public async Task When_the_time_picker_is_confirmed(int hour, int minute)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var dialog = _timeFlyout!.Dialog!;
            dialog.Hour = hour;
            dialog.Minute = minute;
            FindButton(dialog.View!, "material_timepicker_ok_button")!.PerformClick();
        }).ConfigureAwait(false);
        await DelayAsync(500).ConfigureAwait(false);
    }

    /// <summary>Asserts that no Material picker dialog is showing and the flyout closed.</summary>
    [Then("no Material picker is showing")]
    public async Task Then_no_Material_picker_is_showing()
    {
        var closed = await PollAsync(() =>
            (_dateFlyout == null || (_dateFlyout.Dialog == null && !_dateFlyout.IsOpen))
            && (_timeFlyout == null || (_timeFlyout.Dialog == null && !_timeFlyout.IsOpen))).ConfigureAwait(false);
        closed.Should().BeTrue("the Material picker must be dismissed and its flyout closed");
    }

    /// <summary>
    /// Asserts that the soft keyboard is not showing (AP7-M): a picker in its text / keyboard input mode raises the keyboard
    /// for its own fields, and the keyboard goes with the dialog.
    /// </summary>
    [Then("the soft keyboard is not showing")]
    public async Task Then_the_soft_keyboard_is_not_showing()
    {
        var hidden = await PollAsync(() => AppHost.Activity?.Window?.DecorView?.RootWindowInsets is not { } insets
            || !insets.IsVisible(global::Android.Views.WindowInsets.Type.Ime())).ConfigureAwait(false);
        hidden.Should().BeTrue("the soft keyboard a Material picker raised must go when the picker closes");
    }

    /// <summary>Asserts a DatePicker's Date (the day).</summary>
    [Then("the DatePicker {string} shows {int}-{int}-{int}")]
    public async Task Then_the_DatePicker_shows(string name, int year, int month, int day)
    {
        var date = default(DateTimeOffset);
        await TestTargetFixture.RunOnUIThreadAsync(() => date = ((DatePicker)ElementRegistry.Resolve(name)).Date).ConfigureAwait(false);
        date.Date.Should().Be(new DateTime(year, month, day));
    }

    /// <summary>Asserts that DateChanged was raised at least once (kept for scenarios that only need "raised").</summary>
    [Then("the DatePicker {string} raised DateChanged at least once")]
    public async Task Then_the_DatePicker_raised_DateChanged_at_least_once(string name)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + ".DateChanged").Should().BeGreaterThan(0);
    }

    /// <summary>Asserts the counted DateChanged events.</summary>
    [Then("the DatePicker {string} raised DateChanged {int} times")]
    public async Task Then_the_DatePicker_raised_DateChanged(string name, int times)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + ".DateChanged").Should().Be(times);
    }

    /// <summary>Shows a TimePicker at a time.</summary>
    [Given("the application shows a TimePicker named {string} at {int}:{int}")]
    public async Task Given_a_TimePicker(string name, int hour, int minute)
    {
        TimePicker picker = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            picker = new TimePicker { Name = name, Time = new TimeSpan(hour, minute, 0), VerticalAlignment = VerticalAlignment.Top };
            ElementRegistry.Register(name, picker);
        }).ConfigureAwait(false);
        await ShowAsync(picker).ConfigureAwait(false);
    }

    /// <summary>Asserts a TimePicker's Time.</summary>
    [Then("the TimePicker {string} shows {int}:{int}")]
    public async Task Then_the_TimePicker_shows(string name, int hour, int minute)
    {
        var time = default(TimeSpan);
        await TestTargetFixture.RunOnUIThreadAsync(() => time = ((TimePicker)ElementRegistry.Resolve(name)).Time).ConfigureAwait(false);
        time.Should().Be(new TimeSpan(hour, minute, 0));
    }

    // ------------------------------------------------------------- helpers

    private static async Task ShowDashedLineAsync(string color, int thickness, int x1, int y1, int x2, int y2, string dashes, string dashCap, string startCap)
    {
        await ShowStageAsync(stage =>
        {
            var array = new DoubleCollection();
            foreach (var part in dashes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                array.Add(double.Parse(part, CultureInfo.InvariantCulture));
            }

            stage.Children.Add(Tracked("rule", new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Brush(color),
                StrokeThickness = thickness,
                StrokeDashArray = array,
                StrokeDashCap = Cap(dashCap),
                StrokeStartLineCap = Cap(startCap),
            }));
        }).ConfigureAwait(false);
    }

    /// <summary>Shows a 400 x 240 Canvas named "stage" (nothing painted behind it) filled by <paramref name="build"/>.</summary>
    private static async Task ShowStageAsync(Action<XamlCanvas> build)
    {
        XamlCanvas stage = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            stage = new XamlCanvas
            {
                Name = "stage",
                Width = StageWidth,
                Height = StageHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ElementRegistry.Register("stage", stage);
            build(stage);
        }).ConfigureAwait(false);
        await ShowAsync(stage).ConfigureAwait(false);
    }

    private static async Task ShowAsync(FrameworkElement element)
    {
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task ShowColorPickerAsync(string name, bool alpha)
    {
        ColorPicker picker = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            picker = new ColorPicker
            {
                Name = name,
                IsAlphaEnabled = alpha,
                IsHexInputVisible = true,
                IsColorChannelTextInputVisible = true,
                IsColorSliderVisible = true,
                IsMoreButtonVisible = false,
                MinWidth = 320,
                Width = 360,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ElementRegistry.Register(name, picker);
        }).ConfigureAwait(false);
        await ShowAsync(picker).ConfigureAwait(false);
    }

    private static TShape Named<TShape>(string name, TShape shape)
        where TShape : Shape
    {
        shape.Name = name;
        ElementRegistry.Register(name, shape);
        return shape;
    }

    private static TShape Tracked<TShape>(string name, TShape shape)
        where TShape : Shape
    {
        Named(name, shape);
        Counts[name + ".Tapped"] = 0;
        shape.Tapped += (_, _) => Counts[name + ".Tapped"] = Counts.GetValueOrDefault(name + ".Tapped") + 1;
        return shape;
    }

    private static ColorPickerView PickerView(string name) =>
        (ColorPickerView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;

    private static SolidColorBrush Brush(string color) => new(Colors.Parse(color));

    private static PenLineCap Cap(string cap) => Enum.Parse<PenLineCap>(cap);

    private static Geometry Geometry(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);

    private static PointCollection Points(string points)
    {
        var collection = new PointCollection();
        foreach (var pair in points.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = pair.Split(',');
            collection.Add(new Windows.Foundation.Point(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture)));
        }

        return collection;
    }

    private static WColor Hex(string hex)
    {
        var value = uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return WColor.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static double Distance(CodeBrix.Android.UIReqs.Device.Canvas.PixelColor pixel, WColor color)
    {
        var r = pixel.Red - color.R;
        var g = pixel.Green - color.G;
        var b = pixel.Blue - color.B;
        return Math.Sqrt((r * r) + (g * g) + (b * b));
    }

    private static AView? FindButton(AView root, string idName)
    {
        var id = root.Context!.Resources!.GetIdentifier(idName, "id", root.Context.PackageName);
        return id == 0 ? null : root.FindViewById(id);
    }

    private static IEnumerable<AView> Views(FrameworkElement element)
    {
        var root = NativeViewLocator.ViewOf(element);
        if (root == null)
        {
            yield break;
        }

        var pending = new Stack<AView>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var view = pending.Pop();
            yield return view;
            if (view is AViewGroup group)
            {
                for (var i = group.ChildCount - 1; i >= 0; i--)
                {
                    if (group.GetChildAt(i) is { } child)
                    {
                        pending.Push(child);
                    }
                }
            }
        }
    }

    private static async Task RealTapAsync(int x, int y)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
            Dispatch(down, down + 50, AMotionEventActions.Up, x, y);
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static void Dispatch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var e = AMotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
        e.SetSource(AInputSourceType.Touchscreen);
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static async Task<bool> PollAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            var ok = false;
            await TestTargetFixture.RunOnUIThreadAsync(() => ok = condition()).ConfigureAwait(false);
            if (ok)
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task DelayAsync(int milliseconds)
    {
        await Task.Delay(milliseconds).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }
}
