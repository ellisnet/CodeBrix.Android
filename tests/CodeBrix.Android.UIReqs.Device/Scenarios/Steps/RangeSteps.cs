using System;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The Range group's steps: the controls that carry a value along a track, and the touch
/// gestures that change it. A thumb is somewhere else in every frame, so the steps that talk
/// about one moving remember where it was when each frame was taken rather than reading its
/// rectangle again afterwards.
/// </summary>
[Binding]
public sealed class RangeSteps
{
	/// <summary>How many moves a drag is delivered in, so that it looks like a finger and not a jump.</summary>
	public const int DragSteps = 8;

	/// <summary>How long the panel is given between the moves of a drag.</summary>
	public static readonly TimeSpan DragStepDelay = TimeSpan.FromMilliseconds(20);

	/// <summary>How long a finger rests on the panel before it starts moving.</summary>
	public static readonly TimeSpan DragPressDelay = TimeSpan.FromMilliseconds(80);

	/// <summary>How long the panel is left alone between two looks at a running animation.</summary>
	public static readonly TimeSpan IndicatorPollInterval = TimeSpan.FromMilliseconds(120);

	/// <summary>The template part a horizontal Slider's thumb is drawn as.</summary>
	public const string SliderThumbPart = "HorizontalThumb";

	/// <summary>The template part a horizontal Slider's whole track is drawn as.</summary>
	public const string SliderTrackPart = "HorizontalTrackRect";

	/// <summary>The template part a horizontal Slider's filled part is drawn as.</summary>
	public const string SliderFillPart = "HorizontalDecreaseRect";

	/// <summary>The template part a ProgressBar's filled indicator is drawn as.</summary>
	public const string ProgressBarIndicatorPart = "DeterminateProgressBarIndicator";

	/// <summary>The template part a vertical ScrollBar's thumb is drawn as.</summary>
	public const string ScrollBarThumbPart = "VerticalThumb";

	/// <summary>The template part a RatingControl's stars are laid out in.</summary>
	public const string RatingStarsPart = "RatingBackgroundStackPanel";

	/// <summary>How far out an "is {float}" claim about a range control's number may be.</summary>
	public const double ExactTolerance = 0.001;

	/// <summary>
	/// How far out an "is about {float}" claim may be: a tenth, which is the shape for a number
	/// a control took from a measured quantity rather than from the scenario.
	/// </summary>
	public const double AboutTolerance = 0.1;

	private const int PointerId = 0;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public RangeSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the Range group's controls, before the first scenario.
	/// </summary>
	[BeforeTestRun(Order = 11)]
	public static void Register_the_range_controls() => RangeElements.Register();

	// ---------------------------------------------------------------- value

	/// <summary>Asserts the Value a range control carries. Value is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The value it must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Value of {string} is {float}")]
	public async Task Then_the_Value_of_is(string name, float expected) =>
		(await ValueOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(expected, 0.001,
			"the Value of \"{0}\" was asserted", name);

	/// <summary>Asserts that a range control's Value is above a number.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="threshold">The number it must be above.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Value of {string} is more than {float}")]
	public async Task Then_the_Value_of_is_more_than(string name, float threshold) =>
		(await ValueOfAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(threshold,
			"the Value of \"{0}\" was asserted", name);

	/// <summary>Asserts that a range control's Value is below a number.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="threshold">The number it must be below.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Value of {string} is less than {float}")]
	public async Task Then_the_Value_of_is_less_than(string name, float threshold) =>
		(await ValueOfAsync(name).ConfigureAwait(false)).Should().BeLessThan(threshold,
			"the Value of \"{0}\" was asserted", name);

	// ----------------------------------------------------- Maximum and Minimum

	/// <summary>
	/// Asserts the Maximum a range control carries. A control that takes its Maximum from
	/// somewhere else - a scrubber bound to a clip's length, a bar bound to a total - promises
	/// exactly this and nothing else, so the requirement has to be able to say it.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The value it must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Maximum of {string} is {float}")]
	public async Task Then_the_Maximum_of_is(string name, float expected) =>
		(await MaximumOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(expected, ExactTolerance,
			"the Maximum of \"{0}\" was asserted", name);

	/// <summary>
	/// Asserts the Maximum a range control carries, to within a tenth. This is the form for a
	/// Maximum that came from a MEASURED quantity - a file's length, a duration an engine
	/// reported - where the exact number is the engine's to decide and the requirement is that
	/// the control took it.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The value it must be near.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Maximum of {string} is about {float}")]
	public async Task Then_the_Maximum_of_is_about(string name, float expected) =>
		(await MaximumOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(expected, AboutTolerance,
			"the Maximum of \"{0}\" was asserted", name);

	/// <summary>Asserts that a range control's Maximum is above a number.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="threshold">The number it must be above.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Maximum of {string} is more than {float}")]
	public async Task Then_the_Maximum_of_is_more_than(string name, float threshold) =>
		(await MaximumOfAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(threshold,
			"the Maximum of \"{0}\" was asserted", name);

	/// <summary>Asserts the Minimum a range control carries.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The value it must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Minimum of {string} is {float}")]
	public async Task Then_the_Minimum_of_is(string name, float expected) =>
		(await MinimumOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(expected, ExactTolerance,
			"the Minimum of \"{0}\" was asserted", name);

	/// <summary>Asserts the Minimum a range control carries, to within a tenth.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The value it must be near.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Minimum of {string} is about {float}")]
	public async Task Then_the_Minimum_of_is_about(string name, float expected) =>
		(await MinimumOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(expected, AboutTolerance,
			"the Minimum of \"{0}\" was asserted", name);

	/// <summary>Asserts that a range control's Minimum is below a number.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="threshold">The number it must be below.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Minimum of {string} is less than {float}")]
	public async Task Then_the_Minimum_of_is_less_than(string name, float threshold) =>
		(await MinimumOfAsync(name).ConfigureAwait(false)).Should().BeLessThan(threshold,
			"the Minimum of \"{0}\" was asserted", name);

	/// <summary>Asserts how often a range control raised ValueChanged.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the ValueChanged of {string} was raised {int} times")]
	public void Then_the_ValueChanged_of_was_raised_times(string name, int times) =>
		EventRecorder.Count(name, RangeElements.ValueChangedEvent).Should().Be(times,
			"the ValueChanged of \"{0}\" was asserted; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));

	/// <summary>Asserts that a range control raised ValueChanged at least this often.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">The fewest raisings that satisfy the requirement.</param>
	[Then("the ValueChanged of {string} was raised at least {int} times")]
	public void Then_the_ValueChanged_of_was_raised_at_least_times(string name, int times) =>
		EventRecorder.Count(name, RangeElements.ValueChangedEvent).Should().BeGreaterThanOrEqualTo(times,
			"the ValueChanged of \"{0}\" was asserted; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));

	// --------------------------------------------------------------- Slider

	/// <summary>Taps a Slider's track a given share of the way along it.</summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="percent">How far along the track the finger lands.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the Slider {string} is tapped at {int} percent of its track")]
	public async Task When_the_Slider_is_tapped_at_percent_of_its_track(string name, int percent)
	{
		var track = await TrackOfAsync(name, SliderTrackPart).ConfigureAwait(false);
		var x = track.X + (int) Math.Round(track.Width * (percent / 100.0));
		var y = track.Center.Y;

		TestTargetFixture.Session.Tap(Math.Clamp(x, track.X, track.Right - 1), y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Drags a Slider's thumb with one finger: press on the thumb, move in several steps, and
	/// lift - the three calls a touch panel actually delivers.
	/// </summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="distance">How many device pixels to the right the finger travels.</param>
	/// <returns>A task that completes once the finger has been lifted and the UI thread is idle.</returns>
	[When("the Slider thumb of {string} is dragged {int} pixels to the right")]
	public async Task When_the_Slider_thumb_is_dragged_pixels_to_the_right(string name, int distance)
	{
		ElementRegistry.Resolve(name);
		var thumb = await DeviceRect.OfAsync(ElementRegistry.Resolve(SliderThumbPart)).ConfigureAwait(false);
		var (startX, y) = thumb.Center;

		TestTargetFixture.Session.TouchPress(PointerId, startX, y);
		await DelayAsync(DragPressDelay).ConfigureAwait(false);

		for (var step = 1; step <= DragSteps; step++)
		{
			var x = startX + (int) Math.Round(distance * (step / (double) DragSteps));
			TestTargetFixture.Session.TouchMove(PointerId, x, y);
			await DelayAsync(DragStepDelay).ConfigureAwait(false);
		}

		TestTargetFixture.Session.TouchRelease(PointerId, startX + distance, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Captures a frame and remembers where the Slider's thumb was in it, because the thumb is
	/// somewhere else in the next frame.
	/// </summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the Slider thumb of {string} is captured as {string}")]
	public async Task When_the_Slider_thumb_is_captured_as(string name, string captureName) =>
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, SliderThumbPart, SliderTrackPart)
			.ConfigureAwait(false);

	/// <summary>
	/// Asserts that the place the Slider's thumb used to occupy no longer looks the way it did.
	/// </summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="fromCapture">The capture the thumb's old place is taken from.</param>
	/// <param name="toCapture">The capture that must look different there.</param>
	[Then("where the Slider thumb of {string} was in {string} looks different in {string}")]
	public void Then_where_the_Slider_thumb_was_looks_different(string name, string fromCapture, string toCapture)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var was = before.PartRegion($"where the thumb of the Slider \"{name}\" was in capture \"{fromCapture}\"");
		var now = new Region(after.Frame, before.Part, $"that same place in capture \"{toCapture}\"");
		now.DiffersFrom(was);
	}

	/// <summary>Asserts that the Slider's thumb was actually drawn in a capture.</summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("the Slider thumb of {string} had ink in {string}")]
	public void Then_the_Slider_thumb_had_ink_in(string name, string captureName) =>
		CapturedParts.Get(_scenarioContext, captureName)
			.PartRegion($"the thumb of the Slider \"{name}\" in capture \"{captureName}\"")
			.HasInk();

	/// <summary>Asserts that the Slider's thumb moved to the right between two captures.</summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	/// <param name="minimum">The fewest device pixels the thumb must have travelled.</param>
	[Then("the Slider thumb of {string} moved right from {string} to {string} by at least {int} pixels")]
	public void Then_the_Slider_thumb_moved_right(string name, string fromCapture, string toCapture, int minimum)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);

		(after.Part.X - before.Part.X).Should().BeGreaterThanOrEqualTo(minimum,
			"the thumb of the Slider \"{0}\" must move right: it was at {1} in capture \"{2}\" and at {3} in capture \"{4}\"",
			name, before.Part, fromCapture, after.Part, toCapture);
	}

	/// <summary>
	/// Asserts how far along its track a Slider's filled part reaches, as the tree lays it out.
	/// </summary>
	/// <param name="name">The Gherkin name of the Slider.</param>
	/// <param name="percent">The share of the track that must be filled.</param>
	/// <param name="tolerance">How many device pixels the answer may be out by.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Slider {string} fills {int} percent of its track, within {int} pixels")]
	public async Task Then_the_Slider_fills_percent_of_its_track(string name, int percent, int tolerance)
	{
		ElementRegistry.Resolve(name);
		var track = await WidthOfAsync(SliderTrackPart).ConfigureAwait(false);
		var thumb = await WidthOfAsync(SliderThumbPart).ConfigureAwait(false);
		var fill = await WidthOfAsync(SliderFillPart).ConfigureAwait(false);

		// The thumb has to fit inside the track at either end, so the distance the value can
		// actually travel is the track less one thumb.
		var travel = track - thumb;
		var expected = travel * (percent / 100.0);

		Math.Abs(fill - expected).Should().BeLessThanOrEqualTo(tolerance,
			"the filled part of the Slider \"{0}\" must be {1} of the {2} pixels its thumb can travel, so about {3} pixels wide, but it is {4}",
			name, PercentText(percent), travel, Pixels(expected), Pixels(fill));
	}

	// ---------------------------------------------------------- ProgressBar

	/// <summary>
	/// Asserts how much of a ProgressBar's width its indicator covers, as the tree lays it out.
	/// </summary>
	/// <param name="name">The Gherkin name of the ProgressBar.</param>
	/// <param name="percent">The share of the width the indicator must cover.</param>
	/// <param name="tolerance">How many device pixels the answer may be out by.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ProgressBar {string} fills {int} percent of its width, within {int} pixels")]
	public async Task Then_the_ProgressBar_fills_percent_of_its_width(string name, int percent, int tolerance)
	{
		var bar = await WidthOfAsync(name).ConfigureAwait(false);
		var indicator = await WidthOfAsync(ProgressBarIndicatorPart).ConfigureAwait(false);
		var expected = bar * (percent / 100.0);

		Math.Abs(indicator - expected).Should().BeLessThanOrEqualTo(tolerance,
			"the indicator of the ProgressBar \"{0}\" must be {1} of its {2} pixel width, so about {3} pixels wide, but it is {4}",
			name, PercentText(percent), Pixels(bar), Pixels(expected), Pixels(indicator));
	}

	/// <summary>
	/// Asserts how far across a ProgressBar one colour actually reaches - the same requirement
	/// as the one above, but read off the panel instead of out of the tree.
	/// </summary>
	/// <param name="name">The Gherkin name of the ProgressBar.</param>
	/// <param name="percent">How far across the colour must reach.</param>
	/// <param name="color">The colour the filled part is painted.</param>
	/// <param name="tolerance">How many device pixels the answer may be out by.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ProgressBar {string} is painted {int} percent of the way across in {string}, within {int} pixels")]
	public async Task Then_the_ProgressBar_is_painted_percent_of_the_way_across(
		string name, int percent, Color color, int tolerance)
	{
		var region = await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false);
		var painted = region.ColorBounds(color);
		var expected = region.Bounds.Width * (percent / 100.0);

		painted.X.Should().BeLessThanOrEqualTo(region.Bounds.X + tolerance,
			"the painted part of the ProgressBar \"{0}\" must start at the left of its track, but it starts at {1}",
			name, painted);
		Math.Abs(painted.Width - expected).Should().BeLessThanOrEqualTo(tolerance,
			"the painted part of the ProgressBar \"{0}\" must be {1} of its {2} pixel track, so about {3} pixels wide, but it is {4}",
			name, PercentText(percent), region.Bounds.Width,
			expected.ToString("0.0", CultureInfo.InvariantCulture), painted);
	}

	/// <summary>
	/// Leaves a running progress control alone for a while, so that a scenario about something
	/// that takes time - an indicator sweeping across a track, a ring fading in - can look at
	/// two moments rather than at one.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="milliseconds">How long to leave it running.</param>
	/// <returns>A task that completes once that long has passed and the UI thread is idle.</returns>
	[When("the ProgressBar {string} is left running for {int} milliseconds")]
	[When("the ProgressRing {string} is left running for {int} milliseconds")]
	public async Task When_the_progress_control_is_left_running_for_milliseconds(string name, int milliseconds)
	{
		ElementRegistry.Resolve(name);
		await DelayAsync(TimeSpan.FromMilliseconds(milliseconds)).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that a ProgressBar with no value of its own nevertheless paints an indicator:
	/// frames are taken until one shows the colour, because an indeterminate indicator sweeps
	/// across the track and is off the end of it for part of every sweep.
	/// </summary>
	/// <param name="name">The Gherkin name of the ProgressBar.</param>
	/// <param name="color">The colour the indicator is painted.</param>
	/// <param name="milliseconds">How long the indicator has to show itself.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ProgressBar {string} paints an indicator in {string} within {int} milliseconds")]
	public async Task Then_the_ProgressBar_paints_an_indicator_within(string name, Color color, int milliseconds)
	{
		await Poll.UntilTheRegionShowsAsync(
			_scenarioContext,
			name,
			region => CanvasAssert.FractionMatching(region, color) >= CanvasAssert.ContainsFraction,
			TimeSpan.FromMilliseconds(milliseconds),
			IndicatorPollInterval).ConfigureAwait(false);

		// Either the indicator showed itself or the budget is gone; the last frame the poll
		// looked at is the scenario's current frame either way. Stating the requirement even
		// when the poll gave up is what puts the panel's colours in the report.
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false)).Contains(color);
	}

	// --------------------------------------------------------- ScrollBar

	/// <summary>
	/// Captures a frame and remembers where the ScrollBar's thumb was in it.
	/// </summary>
	/// <param name="name">The Gherkin name of the ScrollBar.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the ScrollBar thumb of {string} is captured as {string}")]
	public async Task When_the_ScrollBar_thumb_is_captured_as(string name, string captureName) =>
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, ScrollBarThumbPart, name)
			.ConfigureAwait(false);

	/// <summary>Asserts that the ScrollBar's thumb was actually drawn in a capture.</summary>
	/// <param name="name">The Gherkin name of the ScrollBar.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("the ScrollBar thumb of {string} had ink in {string}")]
	public void Then_the_ScrollBar_thumb_had_ink_in(string name, string captureName) =>
		CapturedParts.Get(_scenarioContext, captureName)
			.PartRegion($"the thumb of the ScrollBar \"{name}\" in capture \"{captureName}\"")
			.HasInk();

	/// <summary>Asserts that the ScrollBar's thumb moved down between two captures.</summary>
	/// <param name="name">The Gherkin name of the ScrollBar.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	/// <param name="minimum">The fewest device pixels the thumb must have travelled.</param>
	[Then("the ScrollBar thumb of {string} moved down from {string} to {string} by at least {int} pixels")]
	public void Then_the_ScrollBar_thumb_moved_down(string name, string fromCapture, string toCapture, int minimum)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);

		(after.Part.Y - before.Part.Y).Should().BeGreaterThanOrEqualTo(minimum,
			"the thumb of the ScrollBar \"{0}\" must move down: it was at {1} in capture \"{2}\" and at {3} in capture \"{4}\"",
			name, before.Part, fromCapture, after.Part, toCapture);
	}

	// ------------------------------------------------------ RatingControl

	/// <summary>Taps one of a RatingControl's stars, counting from one at the left.</summary>
	/// <param name="star">Which star to tap.</param>
	/// <param name="name">The Gherkin name of the RatingControl.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("star {int} of the RatingControl {string} is tapped")]
	public async Task When_star_of_the_RatingControl_is_tapped(int star, string name)
	{
		var maxRating = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			maxRating = ElementRegistry.Resolve(name) is RatingControl rating
				? rating.MaxRating
				: throw new NotSupportedException($"\"{name}\" is not a RatingControl.");
		}).ConfigureAwait(false);

		var stars = await DeviceRect.OfAsync(ElementRegistry.Resolve(RatingStarsPart)).ConfigureAwait(false);
		var starWidth = stars.Width / (double) maxRating;
		var x = stars.X + (int) Math.Round(starWidth * (star - 0.5));

		TestTargetFixture.Session.Tap(Math.Clamp(x, stars.X, stars.Right - 1), stars.Center.Y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ---------------------------------------------------------------- inner

	private static string PercentText(int percent) =>
		string.Create(CultureInfo.InvariantCulture, $"{percent}%");

	private static string Pixels(double width) => width.ToString("0.0", CultureInfo.InvariantCulture);

	// Widths come from the laid-out tree rather than from a device rectangle, because an
	// indicator with nothing to show is legitimately nought pixels wide and has no rectangle.
	private static async Task<double> WidthOfAsync(string name)
	{
		var width = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			width = ElementRegistry.Resolve(name).ActualWidth).ConfigureAwait(false);

		return width;
	}

	private static async Task<double> ValueOfAsync(string name)
	{
		var value = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			value = RangeElements.ValueOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);

		return value;
	}

	private static async Task<double> MaximumOfAsync(string name)
	{
		var maximum = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			maximum = RangeElements.MaximumOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);

		return maximum;
	}

	private static async Task<double> MinimumOfAsync(string name)
	{
		var minimum = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			minimum = RangeElements.MinimumOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);

		return minimum;
	}

	private static async Task<DeviceRect> TrackOfAsync(string name, string partName)
	{
		// Resolving the control first turns a mistyped control name into a clear failure
		// rather than a puzzling one about a template part.
		ElementRegistry.Resolve(name);
		return await DeviceRect.OfAsync(ElementRegistry.Resolve(partName), inset: 0).ConfigureAwait(false);
	}

	private static Task DelayAsync(TimeSpan delay) =>
		Task.Delay(delay, TestContext.Current.CancellationToken);
}
