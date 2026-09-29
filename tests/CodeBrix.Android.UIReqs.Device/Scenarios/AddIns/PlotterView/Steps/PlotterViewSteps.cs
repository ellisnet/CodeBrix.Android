using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.PlotterView.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Plotter;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Colors = CodeBrix.Platform.UI.Core.UIReqs.Support.Colors;
using PlotterControlElement = CodeBrix.Platform.UI.PlotterView.PlotterControl;

namespace CodeBrix.Platform.UI.AddIn.PlotterView.UIReqs.Steps;

/// <summary>
/// The PlotterView group's vocabulary: the chart control, the plots and interaction sets it is
/// given by name, the fingers that reach it, and the handful of facts about a plot that only
/// the plotting engine can answer - what the axes now cover, how big the plot area came out,
/// and whether the last render threw.
/// <para>
/// Everything else a scenario says - showing the control, sizing its cell, capturing a frame,
/// giving it the keyboard, pressing a key, and every claim about pixels - is the core
/// harness's own vocabulary, reached through this project's reqnroll.json binding assemblies.
/// </para>
/// <para>
/// The engine's model is not thread-safe and the control paints from the UI thread, so every
/// read of a model here happens inside <see cref="TestTargetFixture.RunOnUIThreadAsync(Action)"/>
/// AND under that model's own <c>SyncRoot</c> - the same lock the control's paint takes.
/// </para>
/// </summary>
[Binding]
public sealed class PlotterViewSteps
{
	/// <summary>The name a feature file builds the chart control with.</summary>
	public const string PlotterKind = "Plotter";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-plotterview</c> tag:
	/// the add-in assembly and its plotting engine. A machine that can build this project has
	/// both, so nothing is ever skipped for it here - but a run whose add-in could not be
	/// loaded reports skips with a reason instead of a dozen failures about an unknown element
	/// kind.
	/// </summary>
	public const string PlotterViewPrerequisite = "plotterview";

	/// <summary>
	/// How far inside the zoom rectangle a colour claim looks. The rectangle is stroked as well
	/// as filled and its edges are antialiased, so the fill is asserted about the inside of it.
	/// </summary>
	public const int ZoomRectangleInset = 3;

	/// <summary>How many pointer moves a drag or a pinch is delivered in.</summary>
	public const int GestureSteps = 8;

	/// <summary>The pointer id of the finger a one-finger gesture uses.</summary>
	public const int FirstFinger = 0;

	/// <summary>The pointer id of the finger that joins for a two-finger gesture.</summary>
	public const int SecondFinger = 1;

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public PlotterViewSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// The four properties go in as TYPED setters: "Model" and "Controller" are words the whole
	/// assembly may one day want for something else, and the two colour words belong to this
	/// control alone. A control-specific name is never invented where the typed overload can
	/// keep the ordinary English word.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_PlotterView_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			try
			{
				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(PlotterViewPrerequisite,
					$"the PlotterView add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(PlotterKind, () => new PlotterControlElement());

		ElementFactory.RegisterProperty<PlotterControlElement>("Model",
			(plot, value) => plot.Model = PlotterModels.Build(value));
		ElementFactory.RegisterProperty<PlotterControlElement>("Controller",
			(plot, value) => plot.Controller = PlotterControllers.Build(value));
		ElementFactory.RegisterProperty<PlotterControlElement>("TrackerBackground",
			(plot, value) => plot.TrackerBackground = ToPlotterColor(Colors.Parse(value)));
		ElementFactory.RegisterProperty<PlotterControlElement>("ZoomRectangleFill",
			(plot, value) => plot.ZoomRectangleFill = ToPlotterColor(Colors.Parse(value)));
	}

	// ------------------------------------------------------------ engine facts

	/// <summary>
	/// Asserts that the last render threw nothing. The engine CAPTURES a render failure rather
	/// than throwing it, and a plot that failed to render looks exactly like a plot that had
	/// nothing to draw - so every scenario of this group ends here, and a blank frame can never
	/// pass for a correct one. A control with no model at all has rendered nothing and so has
	/// nothing to report.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the plot {string} reported no render error")]
	public async Task Then_the_plot_reported_no_render_error(string name)
	{
		Exception? failure = null;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var model = PlotOf(name).ActualModel;
			if (model is null)
			{
				return;
			}

			lock (model.SyncRoot)
			{
				failure = model.GetLastPlotException();
			}
		}).ConfigureAwait(false);

		failure.Should().BeNull("the last render of \"{0}\" must have completed, but it captured {1}",
			name, failure?.ToString() ?? "nothing");
	}

	/// <summary>
	/// Asserts that the engine gave the plot a real area to draw in. A plot laid out into
	/// nothing renders no ink at all, which a colour claim would report as a blank region
	/// without ever saying why.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the plot area of {string} has a positive size")]
	public async Task Then_the_plot_area_of_has_a_positive_size(string name)
	{
		var area = await PlotAreaAsync(name).ConfigureAwait(false);

		area.Width.Should().BeGreaterThan(0, "the plot area of \"{0}\" is {1}", name, Describe(area));
		area.Height.Should().BeGreaterThan(0, "the plot area of \"{0}\" is {1}", name, Describe(area));
	}

	/// <summary>
	/// Asserts that the engine was handed the whole control to draw in: the plot's own bounds
	/// are the control's laid-out size, so nothing of the chart is cropped and no strip of the
	/// control is left unpainted.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the plot of {string} fills its client area")]
	public async Task Then_the_plot_of_fills_its_client_area(string name)
	{
		var bounds = default(PlotterRect);
		var width = 0.0;
		var height = 0.0;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var plot = PlotOf(name);
			width = plot.ActualWidth;
			height = plot.ActualHeight;
			var model = ModelOf(plot, name);
			lock (model.SyncRoot)
			{
				bounds = model.PlotBounds;
			}
		}).ConfigureAwait(false);

		bounds.Width.Should().BeApproximately(width, 1.0,
			"the plot of \"{0}\" is {1} and the control is {2} wide", name, Describe(bounds), width);
		bounds.Height.Should().BeApproximately(height, 1.0,
			"the plot of \"{0}\" is {1} and the control is {2} tall", name, Describe(bounds), height);
	}

	/// <summary>
	/// Remembers what the axes of a plot cover right now, so that a later step can say how a
	/// key, a finger or a reset changed it.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="rangeName">The name to remember this range under.</param>
	/// <returns>A task that completes once the ranges have been read.</returns>
	[Given("the axes of {string} are remembered as {string}")]
	[When("the axes of {string} are remembered as {string}")]
	public async Task When_the_axes_of_are_remembered_as(string name, string rangeName)
	{
		var ranges = await AxisRangesAsync(name).ConfigureAwait(false);
		_scenarioContext[RangeKey(rangeName)] = ranges;
	}

	/// <summary>Asserts that the x axis now covers less than it did in a remembered range.</summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="rangeName">The remembered range to compare against.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the x-axis range of {string} is narrower than in {string}")]
	public async Task Then_the_x_axis_range_of_is_narrower_than_in(string name, string rangeName)
	{
		var now = await AxisRangesAsync(name).ConfigureAwait(false);
		var before = Remembered(rangeName);

		now.X.Width.Should().BeLessThan(before.X.Width,
			"the x axis of \"{0}\" must cover less than it did in \"{1}\": it was {2} and it is {3}",
			name, rangeName, Describe(before.X), Describe(now.X));
	}

	/// <summary>Asserts that the x axis covers exactly as much as it did in a remembered range.</summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="rangeName">The remembered range to compare against.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the x-axis range of {string} is as wide as in {string}")]
	public async Task Then_the_x_axis_range_of_is_as_wide_as_in(string name, string rangeName)
	{
		var now = await AxisRangesAsync(name).ConfigureAwait(false);
		var before = Remembered(rangeName);

		now.X.Width.Should().BeApproximately(before.X.Width, before.X.Width * RangeTolerance,
			"the x axis of \"{0}\" must cover as much as it did in \"{1}\": it was {2} and it is {3}",
			name, rangeName, Describe(before.X), Describe(now.X));
	}

	/// <summary>
	/// Asserts that the x axis is looking somewhere else than it was: both of its bounds moved,
	/// and both moved by the same amount, which is what makes the move a pan rather than a zoom.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="rangeName">The remembered range to compare against.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the x-axis of {string} has shifted from {string}")]
	public async Task Then_the_x_axis_of_has_shifted_from(string name, string rangeName)
	{
		var now = await AxisRangesAsync(name).ConfigureAwait(false);
		var before = Remembered(rangeName);
		var movedMinimum = now.X.Minimum - before.X.Minimum;
		var movedMaximum = now.X.Maximum - before.X.Maximum;

		Math.Abs(movedMinimum).Should().BeGreaterThan(before.X.Width * MinimumShift,
			"the x axis of \"{0}\" must have moved away from \"{1}\": it was {2} and it is {3}",
			name, rangeName, Describe(before.X), Describe(now.X));
		movedMaximum.Should().BeApproximately(movedMinimum, before.X.Width * RangeTolerance,
			"both ends of the x axis of \"{0}\" must move together: the minimum moved by {1} and the "
			+ "maximum by {2}", name, movedMinimum, movedMaximum);
	}

	/// <summary>
	/// Asserts that both axes cover exactly what they covered in the range remembered as
	/// "initial" - the reset the scenario asked for put the plot back where it started.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the axes of {string} are back to their initial range")]
	public async Task Then_the_axes_of_are_back_to_their_initial_range(string name)
	{
		var now = await AxisRangesAsync(name).ConfigureAwait(false);
		var before = Remembered(InitialRangeName);

		now.X.Minimum.Should().BeApproximately(before.X.Minimum, before.X.Width * RangeTolerance,
			"the x axis of \"{0}\" started at {1} and is at {2}", name, Describe(before.X), Describe(now.X));
		now.X.Maximum.Should().BeApproximately(before.X.Maximum, before.X.Width * RangeTolerance,
			"the x axis of \"{0}\" started at {1} and is at {2}", name, Describe(before.X), Describe(now.X));
		now.Y.Minimum.Should().BeApproximately(before.Y.Minimum, before.Y.Width * RangeTolerance,
			"the y axis of \"{0}\" started at {1} and is at {2}", name, Describe(before.Y), Describe(now.Y));
		now.Y.Maximum.Should().BeApproximately(before.Y.Maximum, before.Y.Width * RangeTolerance,
			"the y axis of \"{0}\" started at {1} and is at {2}", name, Describe(before.Y), Describe(now.Y));
	}

	/// <summary>Asserts that the x axis now reaches further right than it did.</summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="rangeName">The remembered range to compare against.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the x-axis maximum of {string} is greater than in {string}")]
	public async Task Then_the_x_axis_maximum_of_is_greater_than_in(string name, string rangeName)
	{
		var now = await AxisRangesAsync(name).ConfigureAwait(false);
		var before = Remembered(rangeName);

		now.X.Maximum.Should().BeGreaterThan(before.X.Maximum,
			"the x axis of \"{0}\" must reach the new data: it was {1} and it is {2}",
			name, Describe(before.X), Describe(now.X));
	}

	/// <summary>
	/// Adds one data point past the end of a plot's line and invalidates it, which is the
	/// engine's documented way of changing what a chart shows: mutate the model under its own
	/// lock, then ask the view to re-read it.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the change has been applied and drawn.</returns>
	[When("a point is added beyond the data of {string} and the plot is invalidated")]
	public async Task When_a_point_is_added_beyond_the_data_of_and_the_plot_is_invalidated(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var plot = PlotOf(name);
			var model = ModelOf(plot, name);
			lock (model.SyncRoot)
			{
				PlotterModels.AddAPointBeyondTheData(model);
			}

			model.InvalidatePlot(true);
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ----------------------------------------------------------------- fingers

	/// <summary>
	/// Puts a finger on the middle of the plot area and LEAVES it there. A tracker is shown
	/// while a finger is down and taken away again when it lifts, so a scenario that wants to
	/// see one captures its frame between this step and the lift.
	/// <para>
	/// Only the plotting engine knows where the plot area is, which is why the press is placed
	/// here rather than by the core's "a finger is put down on ..."; where it landed is recorded
	/// in the core's own finger state, so the core's "that finger is lifted" takes it back up.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the touch has been delivered and the UI thread is idle.</returns>
	[When("a finger is put down at the centre of the plot area of {string}")]
	public async Task When_a_finger_is_put_down_at_the_centre_of_the_plot_area_of(string name)
	{
		var (x, y) = await PlotAreaCentreAsync(name).ConfigureAwait(false);
		Finger.PutDown(x, y);
		TestTargetFixture.Session.TouchPress(Finger.PointerId, x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Drags one finger across the plot area, from its centre, in a straight line and in even
	/// steps - the gesture a person makes to push a chart sideways.
	/// </summary>
	/// <param name="deltaX">How far right the finger travels, in device pixels.</param>
	/// <param name="deltaY">How far down the finger travels, in device pixels.</param>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the whole gesture has been delivered.</returns>
	[When("a finger drags {int}, {int} pixels across the plot area of {string}")]
	public async Task When_a_finger_drags_pixels_across_the_plot_area_of(int deltaX, int deltaY, string name)
	{
		var (x, y) = await PlotAreaCentreAsync(name).ConfigureAwait(false);
		var session = TestTargetFixture.Session;

		session.TouchPress(FirstFinger, x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		for (var step = 1; step <= GestureSteps; step++)
		{
			session.TouchMove(FirstFinger,
				x + (deltaX * step / GestureSteps),
				y + (deltaY * step / GestureSteps));
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		}

		session.TouchRelease(FirstFinger, x + deltaX, y + deltaY);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Pinches the plot area apart with two fingers: the first stays where it landed and the
	/// second walks away from it along the x axis. Holding one finger still is what makes the
	/// gesture a pure spread - the distance between the contacts is all that changes, so what
	/// the plot does can only be a zoom.
	/// </summary>
	/// <param name="spread">How much further apart the fingers end up, in device pixels.</param>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the whole gesture has been delivered.</returns>
	[When("two fingers spread {int} pixels apart across the plot area of {string}")]
	public async Task When_two_fingers_spread_pixels_apart_across_the_plot_area_of(int spread, string name)
	{
		var (centreX, centreY) = await PlotAreaCentreAsync(name).ConfigureAwait(false);
		var area = await PlotAreaAsync(name).ConfigureAwait(false);
		var gap = (int) Math.Round(area.Width / 6.0);
		var session = TestTargetFixture.Session;
		var anchorX = centreX - gap;
		var movingX = centreX + gap;

		session.TouchPress(FirstFinger, anchorX, centreY);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		session.TouchPress(SecondFinger, movingX, centreY);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		for (var step = 1; step <= GestureSteps; step++)
		{
			session.TouchMove(SecondFinger, movingX + (spread * step / GestureSteps), centreY);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		}

		session.TouchRelease(SecondFinger, movingX + spread, centreY);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		session.TouchRelease(FirstFinger, anchorX, centreY);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// -------------------------------------------------------- the zoom rectangle

	/// <summary>
	/// Shows the zoom rectangle over the middle of the plot area. A chart on a touch panel has
	/// no middle-drag to raise one with, so a scenario asks the view for it directly - which is
	/// also how an application that draws its own selection gesture would.
	/// </summary>
	/// <param name="width">The rectangle's width in device pixels.</param>
	/// <param name="height">The rectangle's height in device pixels.</param>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the rectangle has been shown and the UI thread is idle.</returns>
	[When("a zoom rectangle {int} by {int} is shown at the centre of the plot area of {string}")]
	public async Task When_a_zoom_rectangle_is_shown_at_the_centre_of_the_plot_area_of(
		int width, int height, string name)
	{
		var area = await PlotAreaAsync(name).ConfigureAwait(false);
		var left = area.Left + ((area.Width - width) / 2.0);
		var top = area.Top + ((area.Height - height) / 2.0);
		var rectangle = new PlotterRect(Math.Round(left), Math.Round(top), width, height);

		_scenarioContext[ZoomRectangleKey] = rectangle;

		await TestTargetFixture.RunOnUIThreadAsync(() => PlotOf(name).ShowZoomRectangle(rectangle))
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Takes the zoom rectangle away again.</summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes once the rectangle is gone and the UI thread is idle.</returns>
	[When("the zoom rectangle of {string} is hidden")]
	public async Task When_the_zoom_rectangle_of_is_hidden(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => PlotOf(name).HideZoomRectangle())
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts what colour fills the rectangle the scenario asked for. The claim is about the
	/// inside of it, a few pixels in from the stroked and antialiased edge.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="color">The colour the fill must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the zoom rectangle area of {string} is uniformly {string}")]
	public async Task Then_the_zoom_rectangle_area_of_is_uniformly(string name, Color color)
	{
		var region = await ZoomRectangleRegionAsync(name).ConfigureAwait(false);
		region.IsUniformly(color);
	}

	// ------------------------------------------------------------- chart text

	/// <summary>Asserts that the strip the engine reserved for the plot's title was drawn in.</summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the title of {string} has ink")]
	public async Task Then_the_title_of_has_ink(string name)
	{
		var region = await TitleRegionAsync(name).ConfigureAwait(false);
		region.HasInk();
	}

	/// <summary>
	/// Asserts what colour the plot's title was drawn in. This is the whole of the font claim:
	/// the title is only there at all if the engine resolved the model's font family through
	/// the application's own fonts, and it is only THIS colour if what was drawn is the title
	/// rather than something else that happens to be in the strip.
	/// </summary>
	/// <param name="name">The Gherkin name of the chart control.</param>
	/// <param name="color">The colour the title's ink must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink color of the title of {string} is {string}")]
	public async Task Then_the_ink_color_of_the_title_of_is(string name, Color color)
	{
		var region = await TitleRegionAsync(name).ConfigureAwait(false);
		region.InkColorIs(color);
	}

	// --------------------------------------------------------------- inner

	private const string InitialRangeName = "initial";

	private const string ZoomRectangleKey = "uireqs.plotterview.zoomRectangle";

	private const double RangeTolerance = 0.001;

	private const double MinimumShift = 0.02;

	private static string RangeKey(string rangeName) =>
		string.Create(CultureInfo.InvariantCulture, $"uireqs.plotterview.range.{rangeName}");

	private static PlotterColor ToPlotterColor(Color color) =>
		PlotterColor.FromArgb(color.A, color.R, color.G, color.B);

	private static PlotterControlElement PlotOf(string name) =>
		ElementRegistry.Resolve(name) as PlotterControlElement
		?? throw new NotSupportedException(
			$"\"{name}\" is not a Plotter, so it shows no plot.");

	private static PlotModel ModelOf(PlotterControlElement plot, string name) =>
		plot.ActualModel ?? throw new NotSupportedException(
			$"\"{name}\" has no Model, so there is nothing to ask about its plot.");

	private static string Describe(PlotterRect rectangle) => string.Create(CultureInfo.InvariantCulture,
		$"({rectangle.Left:0.##},{rectangle.Top:0.##}) {rectangle.Width:0.##} x {rectangle.Height:0.##}");

	private static string Describe(AxisRange range) => string.Create(CultureInfo.InvariantCulture,
		$"{range.Minimum:0.####} to {range.Maximum:0.####}");

	private AxisRanges Remembered(string rangeName) =>
		_scenarioContext.TryGetValue(RangeKey(rangeName), out var stored) && stored is AxisRanges ranges
			? ranges
			: throw new InvalidOperationException(
				$"No axis range was remembered as \"{rangeName}\". A scenario remembers one with "
				+ "\"the axes of ... are remembered as ...\" before the step that compares against it.");

	private static async Task ReadTheModelAsync(string name, Action<PlotModel> read) =>
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var model = ModelOf(PlotOf(name), name);
			lock (model.SyncRoot)
			{
				read(model);
			}
		}).ConfigureAwait(false);

	private static async Task<PlotterRect> PlotAreaAsync(string name)
	{
		var area = default(PlotterRect);
		await ReadTheModelAsync(name, model => area = model.PlotArea).ConfigureAwait(false);
		return area;
	}

	private static async Task<AxisRanges> AxisRangesAsync(string name)
	{
		AxisRanges? ranges = null;
		await ReadTheModelAsync(name, model =>
		{
			var x = model.DefaultXAxis ?? throw new NotSupportedException(
				$"The plot of \"{name}\" has no x axis, so nothing can be said about its range.");
			var y = model.DefaultYAxis ?? throw new NotSupportedException(
				$"The plot of \"{name}\" has no y axis, so nothing can be said about its range.");

			ranges = new AxisRanges(
				new AxisRange(x.ActualMinimum, x.ActualMaximum),
				new AxisRange(y.ActualMinimum, y.ActualMaximum));
		}).ConfigureAwait(false);

		return ranges!;
	}

	private static async Task<(int X, int Y)> PlotAreaCentreAsync(string name)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		var area = await PlotAreaAsync(name).ConfigureAwait(false);

		// Display scale is pinned at 1.0, so a device-independent pixel of the plot's own
		// coordinate space is a device pixel of the panel.
		return (
			bounds.X + (int) Math.Round(area.Left + (area.Width / 2.0)),
			bounds.Y + (int) Math.Round(area.Top + (area.Height / 2.0)));
	}

	private async Task<Region> ZoomRectangleRegionAsync(string name)
	{
		if (!_scenarioContext.TryGetValue(ZoomRectangleKey, out var stored) || stored is not PlotterRect rectangle)
		{
			throw new InvalidOperationException(
				"No zoom rectangle has been shown, so there is no area to look at.");
		}

		return await SubRegionAsync(name, rectangle, ZoomRectangleInset,
			$"the zoom rectangle of \"{name}\"").ConfigureAwait(false);
	}

	private async Task<Region> TitleRegionAsync(string name)
	{
		var title = default(PlotterRect);
		await ReadTheModelAsync(name, model => title = model.TitleArea).ConfigureAwait(false);

		return await SubRegionAsync(name, title, inset: 0, $"the title of \"{name}\"").ConfigureAwait(false);
	}

	private async Task<Region> SubRegionAsync(string name, PlotterRect rectangle, int inset,
		string description)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		var frame = ScenarioFrames.Current(_scenarioContext);
		var area = new DeviceRect(
			bounds.X + (int) Math.Round(rectangle.Left) + inset,
			bounds.Y + (int) Math.Round(rectangle.Top) + inset,
			(int) Math.Round(rectangle.Width) - (inset * 2),
			(int) Math.Round(rectangle.Height) - (inset * 2));

		if (area.IsEmpty)
		{
			throw new InvalidOperationException(
				$"{description} came out as {area}, which has nothing in it to look at.");
		}

		return new Region(frame, area, description);
	}

	private sealed record AxisRange(double Minimum, double Maximum)
	{
		public double Width => Maximum - Minimum;
	}

	private sealed record AxisRanges(AxisRange X, AxisRange Y);
}
