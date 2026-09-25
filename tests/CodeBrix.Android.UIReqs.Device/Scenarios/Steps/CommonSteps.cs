using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using TextBlock = Microsoft.UI.Xaml.Controls.TextBlock;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The steps every coverage group shares: showing something, changing it, capturing a frame,
/// tapping, and the assertions about what a region looks like. A later group reuses these
/// rather than writing a near-duplicate sentence, because two step definitions that both match
/// one sentence make the scenario fail as ambiguous.
/// </summary>
[Binding]
public sealed class CommonSteps
{
	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public CommonSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	// ------------------------------------------------------------- content

	/// <summary>Shows an empty panel.</summary>
	/// <returns>A task that completes once the panel is empty.</returns>
	[Given("the application shows nothing")]
	public async Task Given_the_application_shows_nothing()
	{
		ElementRegistry.Clear();
		await TestTargetFixture.ClearContentAsync().ConfigureAwait(false);
	}

	/// <summary>Shows one element at its natural size.</summary>
	/// <param name="kind">The element kind, as ElementFactory spells it.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows a {word} named {string}")]
	public async Task Given_the_application_shows_a_named(string kind, string name) =>
		await ShowAsync(kind, name, null, null, null).ConfigureAwait(false);

	/// <summary>Shows one element at a fixed size.</summary>
	/// <param name="kind">The element kind.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="width">The width in logical pixels, which at scale 1.0 are device pixels.</param>
	/// <param name="height">The height in logical pixels.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows a {word} named {string} {int} by {int}")]
	public async Task Given_the_application_shows_a_named_sized(string kind, string name, int width, int height) =>
		await ShowAsync(kind, name, width, height, null).ConfigureAwait(false);

	/// <summary>Shows one element at a fixed size, painted a colour.</summary>
	/// <param name="kind">The element kind.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="width">The width in logical pixels.</param>
	/// <param name="height">The height in logical pixels.</param>
	/// <param name="background">The colour to paint it.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows a {word} named {string} {int} by {int} with Background {string}")]
	public async Task Given_the_application_shows_a_named_sized_with_background(
		string kind, string name, int width, int height, Color background) =>
		await ShowAsync(kind, name, width, height, background).ConfigureAwait(false);

	/// <summary>Shows one element with the properties a table lists.</summary>
	/// <param name="kind">The element kind.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="properties">A Property/Value table.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows a {word} named {string} with:")]
	public async Task Given_the_application_shows_a_named_with_table(string kind, string name, DataTable properties)
	{
		var element = await ElementFactory.CreateAsync(kind, name, ReadProperties(properties)).ConfigureAwait(false);
		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>
	/// Shows one element whose kind starts with a vowel, at its natural size. The harness's own
	/// sentence says "a {kind}", and "a AudioPlayer" is not a sentence anybody would write into
	/// a requirements document; this is the same step with the other article. Every "shows a"
	/// and "holds a" sentence has this twin, so no coverage group ever has to write an article
	/// step of its own for a control whose name begins with a vowel.
	/// </summary>
	/// <param name="kind">The element kind, as ElementFactory spells it.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows an {word} named {string}")]
	public async Task Given_the_application_shows_an_named(string kind, string name) =>
		await ShowAsync(kind, name, null, null, null).ConfigureAwait(false);

	/// <summary>Shows one element whose kind starts with a vowel, at a fixed size.</summary>
	/// <param name="kind">The element kind.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="width">The width in logical pixels, which at scale 1.0 are device pixels.</param>
	/// <param name="height">The height in logical pixels.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows an {word} named {string} {int} by {int}")]
	public async Task Given_the_application_shows_an_named_sized(string kind, string name, int width, int height) =>
		await ShowAsync(kind, name, width, height, null).ConfigureAwait(false);

	/// <summary>Shows one element whose kind starts with a vowel, at a fixed size and colour.</summary>
	/// <param name="kind">The element kind.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="width">The width in logical pixels.</param>
	/// <param name="height">The height in logical pixels.</param>
	/// <param name="background">The colour to paint it.</param>
	/// <returns>A task that completes once the element is showing.</returns>
	[Given("the application shows an {word} named {string} {int} by {int} with Background {string}")]
	public async Task Given_the_application_shows_an_named_sized_with_background(
		string kind, string name, int width, int height, Color background) =>
		await ShowAsync(kind, name, width, height, background).ConfigureAwait(false);

	/// <summary>Changes one property of an element that is already showing.</summary>
	/// <param name="property">The property to set.</param>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="value">The value to set it to.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[Given("the {word} of {string} is set to {string}")]
	[When("the {word} of {string} is set to {string}")]
	public async Task When_the_property_of_is_set_to(string property, string name, string value)
	{
		_scenarioContext[LastChangeKey] = $"{property} of \"{name}\" -> {value}";
		await ElementFactory.ApplyAsync(name, property, value).ConfigureAwait(false);
	}

	/// <summary>
	/// Lays a throwaway string out in a font before the scenario measures anything with it. The
	/// application's own font is warm before the first scenario; a scenario that draws with
	/// ANOTHER font says so here, because the first measurement through a font the text engine
	/// has not loaded yet is answered with an interim face.
	/// </summary>
	/// <param name="fontUri">The font URI, as the scenario's elements spell it.</param>
	/// <returns>A task that completes once the font has been laid out with.</returns>
	[Given("the font {string} is warm")]
	public async Task Given_the_font_is_warm(string fontUri) =>
		await FontWarmup.WarmAsync(GherkinValue.Unquote(fontUri)).ConfigureAwait(false);

	// -------------------------------------------------------------- frames

	/// <summary>
	/// Captures the frame that shows everything the scenario has done so far. It is a Given as
	/// well as a When: a control that has to be laid out and drawn once before anything can be
	/// asked of it is ARRANGED by the first capture, and a scenario should not have to open its
	/// When block to say so.
	/// </summary>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[Given("the frame is captured")]
	[When("the frame is captured")]
	public async Task When_the_frame_is_captured() =>
		await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName).ConfigureAwait(false);

	/// <summary>Captures a frame and gives it a name, so a later step can compare against it.</summary>
	/// <param name="frameName">The name to give the frame.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[Given("the frame is captured as {string}")]
	[When("the frame is captured as {string}")]
	public async Task When_the_frame_is_captured_as(string frameName) =>
		await ScenarioFrames.CaptureAsync(_scenarioContext, frameName).ConfigureAwait(false);

	// --------------------------------------------------------------- input

	/// <summary>
	/// Taps the centre of an element, which is where a finger would land. It is a Given as well
	/// as a When, because a tap is a PRECONDITION as often as it is an action: a control that
	/// only reads the keyboard once it has been touched is touched in the arrange block, and the
	/// scenario's When block stays about the thing the requirement is about.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[Given("{string} is tapped")]
	[When("{string} is tapped")]
	public async Task When_is_tapped(string name)
	{
		var element = ElementRegistry.Resolve(name);
		var bounds = await DeviceRect.OfAsync(element).ConfigureAwait(false);
		var (x, y) = bounds.Center;
		_scenarioContext[LastChangeKey] = string.Create(CultureInfo.InvariantCulture,
			$"tap on \"{name}\" at ({x},{y})");
		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Taps one point of the panel. A Given as well as a When, for the same reason.</summary>
	/// <param name="x">The x coordinate, in device pixels.</param>
	/// <param name="y">The y coordinate, in device pixels.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[Given("the point {int}, {int} is tapped")]
	[When("the point {int}, {int} is tapped")]
	public async Task When_the_point_is_tapped(int x, int y)
	{
		_scenarioContext[LastChangeKey] = string.Create(CultureInfo.InvariantCulture, $"tap at ({x},{y})");
		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Puts a finger on the centre of an element and LEAVES it there. A tap is a press and a
	/// release in one step, so anything a control shows only while a finger rests on it is gone
	/// again by the time a tap returns; a scenario that wants to see one captures its frame
	/// between this step and the lift.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the press has been delivered and the UI thread is idle.</returns>
	[Given("a finger is put down on {string}")]
	[When("a finger is put down on {string}")]
	public async Task When_a_finger_is_put_down_on(string name)
	{
		var element = ElementRegistry.Resolve(name);
		var bounds = await DeviceRect.OfAsync(element).ConfigureAwait(false);
		var (x, y) = bounds.Center;
		_scenarioContext[LastChangeKey] = string.Create(CultureInfo.InvariantCulture,
			$"finger down on \"{name}\" at ({x},{y})");
		Finger.PutDown(x, y);
		TestTargetFixture.Session.TouchPress(Finger.PointerId, x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Lifts the finger the scenario put down, wherever it was put down - the centre of an
	/// element, or a place only the control's own engine could work out.
	/// </summary>
	/// <returns>A task that completes once the release has been delivered and the UI thread is idle.</returns>
	/// <exception cref="InvalidOperationException">No finger is down.</exception>
	[Given("that finger is lifted")]
	[When("that finger is lifted")]
	public async Task When_that_finger_is_lifted()
	{
		var (x, y) = Finger.Lift();
		_scenarioContext[LastChangeKey] = string.Create(CultureInfo.InvariantCulture,
			$"finger lifted at ({x},{y})");
		TestTargetFixture.Session.TouchRelease(Finger.PointerId, x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ---------------------------------------------- appearance, one frame

	/// <summary>Asserts that an element's region is painted one colour throughout.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="color">The colour it must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} is uniformly {string}")]
	public async Task Then_the_region_of_is_uniformly(string name, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).IsUniformly(color);

	/// <summary>Asserts that a colour is present in an element's region.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} contains {string}")]
	public async Task Then_the_region_of_contains(string name, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).Contains(color);

	/// <summary>Asserts that a colour covers at least a share of an element's region.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="percent">The share, as a percentage.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} contains at least {float} percent {string}")]
	public async Task Then_the_region_of_contains_at_least_percent(string name, float percent, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).Contains(color, percent / 100.0);

	/// <summary>Asserts that a colour is absent from an element's region.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} does not contain {string}")]
	public async Task Then_the_region_of_does_not_contain(string name, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).DoesNotContain(color);

	/// <summary>
	/// Asserts that a colour covers at most a share of an element's region. "Does not contain"
	/// judges a colour against the harness's own idea of absent, which is far too strict a claim
	/// about a control that paints a hairline or an antialiased glyph in it; this is the same
	/// claim with the share the requirement actually means, and it is what says that a region is
	/// not JUST one colour without pretending to know what else is in it.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="percent">The share, as a percentage.</param>
	/// <param name="color">The colour that must be scarce.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} contains at most {float} percent {string}")]
	public async Task Then_the_region_of_contains_at_most_percent(string name, float percent, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).DoesNotContain(color, percent / 100.0);

	/// <summary>
	/// Captures frames until a colour covers enough of an element's region, or the budget is
	/// gone. This is the general shape of "an engine put this on the panel, and here is how long
	/// it has": what a person sees is a PAINT-time fact about a change that happened somewhere
	/// else - a decoded picture, a composited page, a rendered surface, an animated frame - and
	/// there is no event that says "what you are about to see has arrived".
	/// <para>
	/// It is a Given and a When as well as a Then, because "the picture is there" is a
	/// PRECONDITION at least as often as it is a claim. The requirement is stated after the poll
	/// gives up, against the last frame the poll looked at, so a run that never gets there fails
	/// with the region's real colours in the report rather than with a bare timeout.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="percent">The share of the region the colour must cover.</param>
	/// <param name="color">The colour to wait for.</param>
	/// <param name="milliseconds">How long it has to appear in.</param>
	/// <returns>A task that completes when the colour is there, or the budget is gone.</returns>
	[Given("the region of {string} shows at least {float} percent {string} within {int} milliseconds")]
	[When("the region of {string} shows at least {float} percent {string} within {int} milliseconds")]
	[Then("the region of {string} shows at least {float} percent {string} within {int} milliseconds")]
	public async Task Then_the_region_of_shows_at_least_percent_within(string name, float percent,
		Color color, int milliseconds)
	{
		var minimum = percent / 100.0;
		var shown = await Poll.UntilTheRegionShowsAsync(
			_scenarioContext,
			name,
			region => CanvasAssert.FractionMatching(region, color) >= minimum,
			TimeSpan.FromMilliseconds(milliseconds),
			PollInterval).ConfigureAwait(false);

		if (!shown)
		{
			// The poll left the last frame it looked at as the scenario's current one, so this
			// states the requirement about exactly the frame that did not satisfy it.
			(await RegionAsync(name).ConfigureAwait(false)).Contains(color, minimum);
		}
	}

	/// <summary>Asserts what colour the ink inside an element's region is.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="color">The colour the ink must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink color of {string} is {string}")]
	public async Task Then_the_ink_color_of_is(string name, Color color) =>
		(await RegionAsync(name).ConfigureAwait(false)).InkColorIs(color);

	/// <summary>Asserts that something was drawn inside an element's region.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} has ink")]
	public async Task Then_the_region_of_has_ink(string name) =>
		(await RegionAsync(name).ConfigureAwait(false)).HasInk();

	/// <summary>Waits for something to be drawn inside an element's region.</summary>
	/// <remarks>
	/// <para>
	/// The sentence without a budget states the requirement about the frame the scenario already
	/// has. This one polls, capturing a fresh frame each time, for ink that arrives under its own
	/// steam - a decoded video frame, a late first paint - and so it is a Given and a When as well
	/// as a Then, because "there is a picture" is a PRECONDITION at least as often as it is a
	/// claim. The requirement is stated after the poll gives up, against the last frame the poll
	/// looked at, so a run that never gets there fails with the region's real colours in the
	/// report rather than with a bare timeout.
	/// </para>
	/// </remarks>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="milliseconds">How long the ink has to appear in.</param>
	/// <returns>A task that completes when there is ink, or the budget is gone.</returns>
	[Given("the region of {string} has ink within {int} milliseconds")]
	[When("the region of {string} has ink within {int} milliseconds")]
	[Then("the region of {string} has ink within {int} milliseconds")]
	public async Task Then_the_region_of_has_ink_within(string name, int milliseconds)
	{
		var inked = await Poll.UntilTheRegionShowsAsync(
			_scenarioContext,
			name,
			region => CanvasAssert.InkFraction(region) >= CanvasAssert.HasInkFraction,
			TimeSpan.FromMilliseconds(milliseconds),
			PollInterval).ConfigureAwait(false);

		if (!inked)
		{
			// The poll left the last frame it looked at as the scenario's current one, so this
			// states the requirement about exactly the frame that did not satisfy it.
			(await RegionAsync(name).ConfigureAwait(false)).HasInk();
		}
	}

	/// <summary>Asserts that an element's region is nothing but panel background.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} is blank")]
	public async Task Then_the_region_of_is_blank(string name) =>
		(await RegionAsync(name).ConfigureAwait(false)).IsBlank();

	/// <summary>Asserts that an element's region runs as a gradient between two colours.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="axis">"horizontal" or "vertical".</param>
	/// <param name="start">The colour at the start of the axis.</param>
	/// <param name="end">The colour at the end of the axis.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} runs as a {word} gradient from {string} to {string}")]
	public async Task Then_the_region_of_runs_as_a_gradient(string name, string axis, Color start, Color end) =>
		(await RegionAsync(name).ConfigureAwait(false))
			.GradientRunsFrom(start, end, GherkinValue.ToEnum<GradientAxis>(axis));

	// -------------------------------------------- appearance, named frames

	/// <summary>Asserts that an element's region in a named frame is painted one colour.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The captured frame to look at.</param>
	/// <param name="color">The colour it must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} is uniformly {string}")]
	public async Task Then_the_region_of_in_frame_is_uniformly(string name, string frameName, Color color) =>
		(await RegionAsync(name, frameName).ConfigureAwait(false)).IsUniformly(color);

	/// <summary>
	/// Asserts that a colour is present in an element's region in a named frame. The negative
	/// half of this claim has always been sayable about a named frame; without the positive half
	/// a scenario that means "this colour is there in the earlier frame and gone in the later
	/// one" has to make its first claim while the earlier frame is still the current one, which
	/// forces the order of the assertions rather than letting the requirement read as written.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The captured frame to look at.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} contains {string}")]
	public async Task Then_the_region_of_in_frame_contains(string name, string frameName, Color color) =>
		(await RegionAsync(name, frameName).ConfigureAwait(false)).Contains(color);

	/// <summary>Asserts that a colour covers at least a share of an element's region in a named frame.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The captured frame to look at.</param>
	/// <param name="percent">The share, as a percentage.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} contains at least {float} percent {string}")]
	public async Task Then_the_region_of_in_frame_contains_at_least_percent(string name, string frameName,
		float percent, Color color) =>
		(await RegionAsync(name, frameName).ConfigureAwait(false)).Contains(color, percent / 100.0);

	/// <summary>Asserts that a colour is absent from an element's region in a named frame.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The captured frame to look at.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} does not contain {string}")]
	public async Task Then_the_region_of_in_frame_does_not_contain(string name, string frameName, Color color) =>
		(await RegionAsync(name, frameName).ConfigureAwait(false)).DoesNotContain(color);

	/// <summary>Asserts what colour the ink of an element is in a named frame.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The captured frame to look at.</param>
	/// <param name="color">The colour the ink must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink color of {string} in frame {string} is {string}")]
	public async Task Then_the_ink_color_of_in_frame_is(string name, string frameName, Color color) =>
		(await RegionAsync(name, frameName).ConfigureAwait(false)).InkColorIs(color);

	/// <summary>Asserts that an element's region changed between two captured frames.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} differs from frame {string}")]
	public async Task Then_the_region_of_in_frame_differs_from_frame(string name, string frameName, string otherFrameName)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		region.DiffersFrom(other);
	}

	/// <summary>Asserts that an element's region did not change between two captured frames.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} in frame {string} is unchanged from frame {string}")]
	public async Task Then_the_region_of_in_frame_is_unchanged_from_frame(string name, string frameName, string otherFrameName)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		region.SameAs(other);
	}

	/// <summary>Asserts that an element's ink sits in the same place in two captured frames.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frameName">One frame.</param>
	/// <param name="otherFrameName">The other frame.</param>
	/// <param name="tolerance">How many pixels each edge may move.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink bounds of {string} in frames {string} and {string} agree within {int} pixel(s)")]
	public async Task Then_the_ink_bounds_of_in_frames_agree_within(string name, string frameName,
		string otherFrameName, int tolerance)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		var bounds = region.InkBounds();
		var otherBounds = other.InkBounds();
		var difference = bounds.LargestEdgeDifference(otherBounds);

		difference.Should().BeLessThanOrEqualTo(tolerance,
			"the ink of \"{0}\" must not move: it is {1} in frame \"{2}\" and {3} in frame \"{4}\"",
			name, bounds, frameName, otherBounds, otherFrameName);
	}

	/// <summary>Asserts that only one element's rectangle repainted between two captured frames.</summary>
	/// <param name="name">The Gherkin name of the element that was allowed to change.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("nothing outside {string} changed between frames {string} and {string}")]
	public async Task Then_nothing_outside_changed_between_frames(string name, string frameName, string otherFrameName)
	{
		var element = ElementRegistry.Resolve(name);
		var bounds = await DeviceRect.OfAsync(element, inset: -1).ConfigureAwait(false);
		var frame = ScenarioFrames.Get(_scenarioContext, frameName);
		var other = ScenarioFrames.Get(_scenarioContext, otherFrameName);
		frame.UnchangedOutside(other, bounds);
	}

	// ----------------------------------------------------- waiting for a while

	/// <summary>
	/// Captures frames until an element's region stops changing, or the budget is gone. Several
	/// framework controls slide or fade into place over a fixed duration and report NOTHING when
	/// they arrive - a transport chrome entering its shown state, a panel opening - so a scenario
	/// that is going to compare two frames of one has to know first that it has finished moving.
	/// <para>
	/// The rule everywhere else in this harness is "wait on a signal, or poll with a budget, and
	/// never sleep". There is no signal here, so this is the poll: two frames in a row that show
	/// the same region, inside <see cref="SettleBudget"/>. The two frames are stored under names
	/// no feature file would choose, so a scenario's own frames are never overwritten by them.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the element whose region must go still.</param>
	/// <returns>A task that completes once two frames in a row show the same region.</returns>
	[Given("the region of {string} has stopped changing")]
	[When("the region of {string} has stopped changing")]
	public async Task Given_the_region_of_has_stopped_changing(string name)
	{
		ElementRegistry.Resolve(name);

		var settled = await Poll.UntilAsync(
			async () =>
			{
				await ScenarioFrames.CaptureAsync(_scenarioContext, SettlingFrameName).ConfigureAwait(false);
				await ScenarioFrames.CaptureAsync(_scenarioContext, SettledFrameName).ConfigureAwait(false);

				var before = await RegionAsync(name, SettlingFrameName).ConfigureAwait(false);
				var after = await RegionAsync(name, SettledFrameName).ConfigureAwait(false);

				return CanvasAssert.FractionDiffering(after, before) <= CanvasAssert.SameFraction;
			},
			SettleBudget,
			SettleInterval).ConfigureAwait(false);

		settled.Should().BeTrue(
			"the region of \"{0}\" must stop changing within {1} ms, or nothing can be compared against it",
			name, SettleBudget.TotalMilliseconds);
	}

	/// <summary>
	/// Leaves the panel alone for a while. This is the ONE sanctioned wait that is not a poll on
	/// a signal, and it exists for the one requirement shape that cannot have one: a NEGATIVE
	/// requirement - a paused player's scrubber does not move, a debounced setter does not fire
	/// twice, a switched-off timer does not repaint - is satisfied by nothing happening, and
	/// nothing signals that nothing happened. So the budget is stated in the sentence, the same
	/// way a poll states its own, and the scenario says how long "long enough" is.
	/// <para>
	/// Everything else waits on a signal or polls with a budget. A step that reaches for this
	/// because a positive thing is slow to arrive is writing a sleep, and a sleep is what makes
	/// a suite slow when it passes and flaky when it does not.
	/// </para>
	/// </summary>
	/// <param name="milliseconds">How long to leave it alone.</param>
	/// <returns>A task that completes once that long has passed and the UI thread is idle.</returns>
	[Given("the panel is left alone for {int} milliseconds")]
	[When("the panel is left alone for {int} milliseconds")]
	public async Task When_the_panel_is_left_alone_for_milliseconds(int milliseconds)
	{
		await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ------------------------------------------------------ tree and events

	/// <summary>
	/// Asserts what text an element holds. Text content is a fact about the tree. A coverage
	/// group whose control carries its text somewhere the harness has never heard of teaches it
	/// with <see cref="ElementFactory.RegisterTextReader{TElement}"/> rather than writing a
	/// near-duplicate sentence of its own; a registered reader is consulted first.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="expected">The text it must hold.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Text of {string} is {string}")]
	public async Task Then_the_Text_of_is(string name, string expected)
	{
		var actual = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			actual = ElementFactory.TryReadText(element, out var registered)
				? registered
				: element switch
				{
					TextBlock text => text.Text,
					TextBox box => box.Text,
					_ => throw new NotSupportedException(
						$"\"{name}\" has no Text the harness can read. A coverage group teaches it one "
						+ "with ElementFactory.RegisterTextReader."),
				};
		}).ConfigureAwait(false);

		actual.Should().Be(expected, "the Text of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts an element's laid-out size, read from the visual tree.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="width">The expected width in device pixels.</param>
	/// <param name="height">The expected height in device pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} is {int} by {int} device pixels")]
	public async Task Then_is_by_device_pixels(string name, int width, int height)
	{
		var actualWidth = 0;
		var actualHeight = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			actualWidth = (int) Math.Round(element.ActualWidth);
			actualHeight = (int) Math.Round(element.ActualHeight);
		}).ConfigureAwait(false);

		actualWidth.Should().Be(width, "the laid-out width of \"{0}\" was asserted", name);
		actualHeight.Should().Be(height, "the laid-out height of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that nothing of that name is anywhere in the tree or the registry.</summary>
	/// <param name="name">The Gherkin name that must not resolve.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("no element named {string} exists")]
	public async Task Then_no_element_named_exists(string name)
	{
		var resolves = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => resolves = ElementRegistry.TryResolve(name, out _))
			.ConfigureAwait(false);

		resolves.Should().BeFalse(
			"nothing named \"{0}\" may be left on the panel; the registry holds [{1}] and the tree holds [{2}]",
			name,
			string.Join(", ", ElementRegistry.Names),
			string.Join(", ", VirtualApplication.Instance.NamedElements()));
	}

	/// <summary>Asserts that an element raised Click exactly once.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	[Then("the Click of {string} was raised once")]
	public void Then_the_Click_of_was_raised_once(string name) => AssertEventCount(name, "Click", 1);

	/// <summary>Asserts how often an element raised Click.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the Click of {string} was raised {int} times")]
	public void Then_the_Click_of_was_raised_times(string name, int times) => AssertEventCount(name, "Click", times);

	/// <summary>Asserts that an element never raised Click.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	[Then("the Click of {string} was not raised")]
	public void Then_the_Click_of_was_not_raised(string name) => AssertEventCount(name, "Click", 0);

	/// <summary>
	/// Forgets every event the scenario has counted so far, so that what follows is measured
	/// from here. An element that raises its event on being loaded, resized or shown has already
	/// raised it by the time a scenario reaches the part it is about. A Given as well as a When:
	/// forgetting what has happened so far is arranging, not acting.
	/// </summary>
	[Given("the recorded events are forgotten")]
	[When("the recorded events are forgotten")]
	public void When_the_recorded_events_are_forgotten() => EventRecorder.Clear();

	/// <summary>
	/// Asserts that an element raised an event at all. A control that repaints on being loaded,
	/// resized, shown or hidden cannot honestly be given an exact count, so a requirement about
	/// it counts "at least once" and leaves the exact number to the framework.
	/// </summary>
	/// <param name="eventName">The event's name.</param>
	/// <param name="name">The Gherkin name of the element.</param>
	[Then("the {word} of {string} was raised at least once")]
	public void Then_the_event_of_was_raised_at_least_once(string eventName, string name) =>
		EventRecorder.Count(name, eventName).Should().BeGreaterThan(0,
			"the {0} of \"{1}\" was asserted; the scenario recorded [{2}]",
			eventName, name, string.Join(", ", EventRecorder.Recorded));

	/// <summary>
	/// Asserts that an element did not raise an event. The sentence says NEVER rather than "not"
	/// so that it cannot also match the Click steps above, which name their event literally.
	/// </summary>
	/// <param name="eventName">The event's name.</param>
	/// <param name="name">The Gherkin name of the element.</param>
	[Then("the {word} of {string} was never raised")]
	public void Then_the_event_of_was_never_raised(string eventName, string name) =>
		EventRecorder.Count(name, eventName).Should().Be(0,
			"the {0} of \"{1}\" was asserted; the scenario recorded [{2}]",
			eventName, name, string.Join(", ", EventRecorder.Recorded));

	/// <summary>
	/// Waits, with a budget, for an element to raise an event, and then asserts that it did.
	/// Anything with an engine behind it finishes when it finishes - a file is decoded, a page
	/// is composited, an instrument is loaded, a clip reaches its end - and the event is the
	/// signal; the budget is how long the thing behind it is allowed to take.
	/// <para>
	/// There is nothing to poll about the event itself: it is recorded as it is raised. The poll
	/// is over the RECORD of it, with the UI thread let drain between two looks, so that an
	/// event raised on the UI thread has somewhere to be raised from.
	/// </para>
	/// <para>
	/// A Given and a When as well as a Then: "the media opened" is a precondition as often as it
	/// is a claim. Say "was raised at least once" instead when the thing has already happened and
	/// there is nothing to wait for.
	/// </para>
	/// </summary>
	/// <param name="eventName">The event's name.</param>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="milliseconds">How long it has to be raised in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the {word} of {string} is raised within {int} milliseconds")]
	[When("the {word} of {string} is raised within {int} milliseconds")]
	[Then("the {word} of {string} is raised within {int} milliseconds")]
	public async Task Then_the_event_of_is_raised_within(string eventName, string name, int milliseconds)
	{
		await Poll.UntilAsync(
			async () =>
			{
				await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
				return EventRecorder.Count(name, eventName) > 0;
			},
			TimeSpan.FromMilliseconds(milliseconds),
			PollInterval).ConfigureAwait(false);

		EventRecorder.Count(name, eventName).Should().BeGreaterThan(0,
			"the {0} of \"{1}\" had {2} milliseconds to be raised; the scenario recorded [{3}]",
			eventName, name, milliseconds, string.Join(", ", EventRecorder.Recorded));
	}

	// --------------------------------------------------------------- inner

	private const string LastChangeKey = "uireqs.lastChange";

	/// <summary>The name the first of the settle poll's two frames is stored under.</summary>
	private const string SettlingFrameName = "the settling frame";

	/// <summary>The name the second of the settle poll's two frames is stored under.</summary>
	private const string SettledFrameName = "the settled frame";

	/// <summary>How long a region has to stop changing in, before the settle poll gives up.</summary>
	private static readonly TimeSpan SettleBudget = TimeSpan.FromSeconds(3);

	/// <summary>How long the settle poll leaves the panel alone between two pairs of frames.</summary>
	private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(80);

	/// <summary>How long the bounded-wait steps leave the panel alone between two attempts.</summary>
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

	private void AssertEventCount(string name, string eventName, int expected) =>
		EventRecorder.Count(name, eventName).Should().Be(expected,
			"the {0} of \"{1}\" was asserted; the scenario recorded [{2}]",
			eventName, name, string.Join(", ", EventRecorder.Recorded));

	private Task<Region> RegionAsync(string elementName, string frameName = ScenarioFrames.CurrentFrameName) =>
		ScenarioFrames.RegionAsync(_scenarioContext, elementName, frameName);

	private async Task ShowAsync(string kind, string name, int? width, int? height, Color? background)
	{
		var properties = new List<KeyValuePair<string, string>>();
		if (width is { } w)
		{
			properties.Add(new KeyValuePair<string, string>("Width", w.ToString(CultureInfo.InvariantCulture)));
		}

		if (height is { } h)
		{
			properties.Add(new KeyValuePair<string, string>("Height", h.ToString(CultureInfo.InvariantCulture)));
		}

		if (background is { } color)
		{
			properties.Add(new KeyValuePair<string, string>("Background", Colors.Describe(color)));
		}

		var element = await ElementFactory.CreateAsync(kind, name, properties).ConfigureAwait(false);
		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	private static IEnumerable<KeyValuePair<string, string>> ReadProperties(DataTable table)
	{
		ArgumentNullException.ThrowIfNull(table);

		return table.Rows.Select(row => new KeyValuePair<string, string>(
			row[table.Header.First()],
			row[table.Header.Last()]));
	}
}
