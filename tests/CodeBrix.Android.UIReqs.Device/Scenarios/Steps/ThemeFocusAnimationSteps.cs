using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The ThemeFocus group's steps: the things that are about the application rather than about
/// one control - which theme it is drawn in, which control the keyboard is talking to, what an
/// animation does over time, what a visual state changes, and what a picture looks like under
/// each Stretch mode.
/// <para>
/// A theme change is the one thing here that outlives its scenario, because it is a change to
/// the running application: the class puts the theme back in a hook as well as in the
/// scenarios that ask for it, so a scenario that fails halfway cannot leave the next one on
/// the wrong palette.
/// </para>
/// </summary>
[Binding]
public sealed class ThemeFocusAnimationSteps
{
	/// <summary>How long a theme change is given to repaint every control under the root.</summary>
	public static readonly TimeSpan ThemeSettleDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>How long a visual state's colour change is given to finish.</summary>
	public static readonly TimeSpan StateSettleDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>How long a focus change is given to draw or take away a focus visual.</summary>
	public static readonly TimeSpan FocusSettleDelay = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// How much longer than its own duration an animation is given before a scenario says it has
	/// finished. The animation has to reach its last frame AND have that frame drawn.
	/// </summary>
	public static readonly TimeSpan AnimationGraceDelay = TimeSpan.FromMilliseconds(300);

	private const string StoryboardKey = "uireqs.w9.storyboard";
	private const string StoryboardDurationKey = "uireqs.w9.storyboardDuration";
	private const string StoryboardFinishedKey = "uireqs.w9.storyboardFinished";
	private const string StateAcceptedKey = "uireqs.w9.stateAccepted";
	private const string ThemeColorsKey = "uireqs.w9.themeColors.";

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public ThemeFocusAnimationSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the ThemeFocus group's controls, before the first scenario.
	/// </summary>
	[BeforeTestRun(Order = 13)]
	public static void Register_the_theme_controls() => ThemeElements.Register();

	/// <summary>
	/// Puts the application's theme back before anything else is reset, so that the next
	/// scenario is drawn on the palette every other group's requirements assume.
	/// </summary>
	/// <returns>A task that completes once the theme is back.</returns>
	[AfterScenario(Order = 1)]
	public static async Task Put_the_theme_back() => await ThemeElements.RestoreRootThemeAsync().ConfigureAwait(false);

	// ---------------------------------------------------------------- theme

	/// <summary>Asks the root element for a theme, which is how a running application changes one.</summary>
	/// <param name="theme">"Light", "Dark" or "Default".</param>
	/// <returns>A task that completes once every control under the root has been repainted.</returns>
	[Given("the root theme is set to {string}")]
	[When("the root theme is set to {string}")]
	public async Task When_the_root_theme_is_set_to(string theme)
	{
		await ThemeElements.SetRootThemeAsync(GherkinValue.ToEnum<ElementTheme>(theme)).ConfigureAwait(false);
		await SettleAsync(ThemeSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts which theme the root element is drawn in.</summary>
	/// <param name="theme">"Light" or "Dark".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the root is drawn in the {string} theme")]
	public async Task Then_the_root_is_drawn_in_the_theme(string theme) =>
		(await ThemeElements.RootThemeAsync().ConfigureAwait(false)).Should()
			.Be(GherkinValue.ToEnum<ElementTheme>(theme), "the theme the root is drawn in was asserted");

	/// <summary>Asserts which theme the whole application is on.</summary>
	/// <param name="theme">"Light" or "Dark".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the application theme is {string}")]
	public async Task Then_the_application_theme_is(string theme) =>
		(await ThemeElements.ApplicationThemeAsync().ConfigureAwait(false)).Should()
			.Be(GherkinValue.ToEnum<ApplicationTheme>(theme), "the application's theme was asserted");

	/// <summary>Asserts which theme one element under the root is drawn in.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="theme">"Light" or "Dark".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} is drawn in the {string} theme")]
	public async Task Then_is_drawn_in_the_theme(string name, string theme)
	{
		var actual = ElementTheme.Default;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = ElementRegistry.Resolve(name).ActualTheme).ConfigureAwait(false);

		actual.Should().Be(GherkinValue.ToEnum<ElementTheme>(theme),
			"the theme \"{0}\" is drawn in was asserted", name);
	}

	/// <summary>Asserts that a control's Foreground is darker than a colour.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="reference">The colour it must be darker than.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Foreground colour of {string} is darker than {string}")]
	public async Task Then_the_Foreground_colour_is_darker_than(string name, Color reference) =>
		AssertDarker(await ThemeElements.ForegroundColorAsync(name).ConfigureAwait(false), reference,
			"Foreground", name);

	/// <summary>Asserts that a control's Foreground is lighter than a colour.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="reference">The colour it must be lighter than.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Foreground colour of {string} is lighter than {string}")]
	public async Task Then_the_Foreground_colour_is_lighter_than(string name, Color reference) =>
		AssertLighter(await ThemeElements.ForegroundColorAsync(name).ConfigureAwait(false), reference,
			"Foreground", name);

	/// <summary>
	/// Asserts that a control really is painted one flat fill - the pixel half of "the theme
	/// decided what this control looks like". The colour itself is not named, because a theme
	/// brush is often translucent and what reaches the panel is that brush composited over
	/// whatever is behind it.
	/// </summary>
	/// <param name="percent">How much of the control's rectangle must be that one colour.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("at least {int} percent of {string} is one flat colour")]
	public async Task Then_at_least_percent_of_is_one_flat_colour(int percent, string name) =>
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false))
			.ShowsASingleFlatColor(percent / 100.0);

	/// <summary>
	/// Asserts that a control's text really is drawn in the colour its template resolved for
	/// it, measured against the fill the control ended up painted rather than against the
	/// panel background.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the text of {string} is drawn in its own Foreground colour")]
	public async Task Then_the_text_of_is_drawn_in_its_own_Foreground(string name)
	{
		var foreground = await ThemeElements.ForegroundColorAsync(name).ConfigureAwait(false);
		var region = await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false);
		region.InkColorOverItsSurfaceIs(foreground);
	}

	/// <summary>
	/// Remembers the colours a control's template resolved out of the theme, so that a later
	/// step can say they changed when the theme did.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="label">The name to remember them under.</param>
	/// <returns>A task that completes once the colours have been read.</returns>
	[Given("the theme colours of {string} are remembered as {string}")]
	[When("the theme colours of {string} are remembered as {string}")]
	public async Task When_the_theme_colours_are_remembered_as(string name, string label)
	{
		var background = await ThemeElements.BackgroundColorAsync(name).ConfigureAwait(false);
		var foreground = await ThemeElements.ForegroundColorAsync(name).ConfigureAwait(false);
		_scenarioContext[ThemeColorsKey + label] = (background, foreground);
	}

	/// <summary>
	/// Asserts that both of the colours a control's template resolved are different from the
	/// ones it had - which is what a ThemeResource re-resolving means on the tree.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="label">The name the earlier colours were remembered under.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the theme colours of {string} changed from {string}")]
	public async Task Then_the_theme_colours_changed_from(string name, string label)
	{
		var remembered = _scenarioContext.TryGetValue(ThemeColorsKey + label, out var stored)
			&& stored is ValueTuple<Color, Color> pair
			? pair
			: throw new InvalidOperationException(
				$"The scenario never remembered the theme colours of \"{name}\" as \"{label}\".");

		var background = await ThemeElements.BackgroundColorAsync(name).ConfigureAwait(false);
		var foreground = await ThemeElements.ForegroundColorAsync(name).ConfigureAwait(false);

		background.Should().NotBe(remembered.Item1,
			"the Background of \"{0}\" must not still be the one the {1} theme gave it", name, label);
		foreground.Should().NotBe(remembered.Item2,
			"the Foreground of \"{0}\" must not still be the one the {1} theme gave it", name, label);
	}

	// ---------------------------------------------------------------- focus

	/// <summary>
	/// Gives a control the keyboard, the way pressing Tab onto it would - a focus taken with
	/// the keyboard is the only kind that draws a focus visual.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the control has the focus and the panel has settled.</returns>
	[Given("the keyboard focus is given to {string}")]
	[When("the keyboard focus is given to {string}")]
	public async Task When_the_keyboard_focus_is_given_to(string name)
	{
		var taken = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var control = ElementRegistry.Resolve(name) as Control
				?? throw new NotSupportedException($"\"{name}\" is not a Control, so it cannot take the focus.");
			taken = control.Focus(FocusState.Keyboard);
		}).ConfigureAwait(false);

		taken.Should().BeTrue("\"{0}\" must be able to take the keyboard focus", name);
		await SettleAsync(FocusSettleDelay).ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that a focus visual is drawn around a control in one frame and not in another.
	/// The visual is drawn OUTSIDE the control, so the requirement is about the ring of pixels
	/// around it rather than about the control's own rectangle.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="focusedFrame">The frame in which the control has the focus.</param>
	/// <param name="unfocusedFrame">The frame in which it does not.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("a focus visual is drawn around {string} in frame {string} but not in frame {string}")]
	public async Task Then_a_focus_visual_is_drawn_around(string name, string focusedFrame, string unfocusedFrame)
	{
		var element = ElementRegistry.Resolve(name);
		var inner = await DeviceRect.OfAsync(element, inset: -1).ConfigureAwait(false);
		var outer = await DeviceRect.OfAsync(element, inset: -CanvasAssert.FocusRingWidth).ConfigureAwait(false);

		var focused = new Region(ScenarioFrames.Get(_scenarioContext, focusedFrame), outer,
			$"the ring around \"{name}\" in frame \"{focusedFrame}\"");
		var unfocused = new Region(ScenarioFrames.Get(_scenarioContext, unfocusedFrame), outer,
			$"the ring around \"{name}\" in frame \"{unfocusedFrame}\"");

		focused.RingGainedInk(unfocused, inner);
	}

	// ------------------------------------------------------------ animation

	/// <summary>Fades a control with a Storyboard, which is how an application animates one.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="from">The opacity the animation starts at.</param>
	/// <param name="to">The opacity it ends at.</param>
	/// <param name="milliseconds">How long it runs for.</param>
	/// <returns>A task that completes once the animation has been started.</returns>
	[When("the Opacity of {string} is animated from {float} to {float} over {int} milliseconds")]
	public async Task When_the_Opacity_is_animated(string name, float from, float to, int milliseconds)
	{
		var duration = TimeSpan.FromMilliseconds(milliseconds);
		var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			var animation = new DoubleAnimation
			{
				From = from,
				To = to,
				Duration = new Duration(duration),
				EnableDependentAnimation = true,
			};

			Storyboard.SetTarget(animation, element);
			Storyboard.SetTargetProperty(animation, "Opacity");

			var storyboard = new Storyboard();
			storyboard.Children.Add(animation);
			storyboard.Completed += (_, _) => finished.TrySetResult();

			_scenarioContext[StoryboardKey] = storyboard;
			_scenarioContext[StoryboardDurationKey] = duration;
			_scenarioContext[StoryboardFinishedKey] = finished;

			storyboard.Begin();
		}).ConfigureAwait(false);
	}

	/// <summary>Leaves a running animation alone for a while, so a frame catches it mid-flight.</summary>
	/// <param name="milliseconds">How long to leave it.</param>
	/// <returns>A task that completes once the time has passed.</returns>
	[When("the animation is left running for {int} milliseconds")]
	public async Task When_the_animation_is_left_running_for(int milliseconds) =>
		await SettleAsync(TimeSpan.FromMilliseconds(milliseconds)).ConfigureAwait(false);

	/// <summary>Waits the animation out: its whole duration, and a moment more to be drawn.</summary>
	/// <returns>A task that completes once the animation has finished.</returns>
	[When("the animation is left to finish")]
	public async Task When_the_animation_is_left_to_finish()
	{
		var duration = _scenarioContext.TryGetValue(StoryboardDurationKey, out var stored) && stored is TimeSpan span
			? span
			: throw new InvalidOperationException("The scenario has not started an animation.");

		await SettleAsync(duration + AnimationGraceDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts what Opacity a control ended up with. Opacity is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="expected">The opacity it must have.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Opacity of {string} is {float}")]
	public async Task Then_the_Opacity_of_is(string name, float expected)
	{
		var actual = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = ElementRegistry.Resolve(name).Opacity).ConfigureAwait(false);

		actual.Should().BeApproximately(expected, 0.01, "the Opacity of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that the animation told the application it had finished.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the animation reported that it finished")]
	public async Task Then_the_animation_reported_that_it_finished()
	{
		var finished = _scenarioContext.TryGetValue(StoryboardFinishedKey, out var stored)
			&& stored is TaskCompletionSource source
			&& source.Task.IsCompleted;

		await Task.CompletedTask.ConfigureAwait(false);
		finished.Should().BeTrue("the animation must raise Completed once it has run its course");
	}

	// --------------------------------------------------------- visual state

	/// <summary>
	/// Shows a control whose visual states are built in C#: one coloured panel, and one state
	/// per row of the table that repaints it.
	/// </summary>
	/// <param name="name">The name the scenario refers to the control by.</param>
	/// <param name="width">The control's width in logical pixels.</param>
	/// <param name="height">The control's height in logical pixels.</param>
	/// <param name="resting">The colour it shows before it is put into any state.</param>
	/// <param name="states">A State/Colour table, one row per visual state.</param>
	/// <returns>A task that completes once the control is showing.</returns>
	[Given("the application shows a state box named {string} {int} by {int} resting {string} with states:")]
	public async Task Given_the_application_shows_a_state_box(string name, int width, int height, Color resting,
		DataTable states)
	{
		ArgumentNullException.ThrowIfNull(states);

		var rows = states.Rows.Select(row => (State: row["State"], Colour: row["Colour"])).ToArray();

		FrameworkElement element = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var box = new StateBox(resting)
			{
				Name = name,
				Width = width,
				Height = height,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};

			foreach (var row in rows)
			{
				box.AddState(row.State, Colors.Parse(row.Colour));
			}

			ElementRegistry.Register(name, box);
			element = box;
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Puts a state box into one of its states, the way a control's own code would.</summary>
	/// <param name="name">The Gherkin name of the state box.</param>
	/// <param name="stateName">The state to go to.</param>
	/// <returns>A task that completes once the state's colour change has finished.</returns>
	[Given("the state box {string} is put into the state {string}")]
	[When("the state box {string} is put into the state {string}")]
	public async Task When_the_state_box_is_put_into_the_state(string name, string stateName)
	{
		var accepted = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var box = ElementRegistry.Resolve(name) as StateBox
				?? throw new NotSupportedException($"\"{name}\" is not a state box.");
			accepted = VisualStateManager.GoToState(box, stateName, false);
		}).ConfigureAwait(false);

		_scenarioContext[StateAcceptedKey] = accepted;
		await SettleAsync(StateSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts that the control knew the state it was asked for.</summary>
	[Then("the state box took the state")]
	public void Then_the_state_box_took_the_state() =>
		StateAccepted().Should().BeTrue("the control must know the state the scenario asked for");

	/// <summary>Asserts that the control did not know the state it was asked for.</summary>
	[Then("the state box did not take the state")]
	public void Then_the_state_box_did_not_take_the_state() =>
		StateAccepted().Should().BeFalse("the control must not claim a state it was never given");

	// ---------------------------------------------------------------- image

	/// <summary>
	/// Shows an Image holding a picture made at run time: two flat colours, one in each half,
	/// which is the smallest picture a Stretch mode can be seen in.
	/// </summary>
	/// <param name="name">The name the scenario refers to the Image by.</param>
	/// <param name="width">The Image's width in logical pixels.</param>
	/// <param name="height">The Image's height in logical pixels.</param>
	/// <param name="left">The colour of the picture's left half.</param>
	/// <param name="right">The colour of the picture's right half.</param>
	/// <returns>A task that completes once the Image is showing its picture.</returns>
	[Given("the application shows a two-colour Image named {string} {int} by {int} with its left half {string} and its right half {string}")]
	public async Task Given_the_application_shows_a_two_colour_Image(string name, int width, int height,
		Color left, Color right)
	{
		var element = await ElementFactory.CreateAsync("Image", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("Height", Number(height)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var picture = new BitmapImage();
			using (var stream = TestImage.OpenPng(left, right))
			{
				picture.SetSource(stream);
			}

			((Image) element).Source = picture;
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
		await SettleAsync(ThemeSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts how big the picture drawn inside a control is.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="width">The picture's width in device pixels.</param>
	/// <param name="height">The picture's height in device pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the picture inside {string} is {int} by {int} pixels")]
	public async Task Then_the_picture_inside_is_by_pixels(string name, int width, int height) =>
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false))
			.PictureIsSized(width, height);

	/// <summary>
	/// Captures a frame and remembers where the picture was drawn at that moment. An Image lays
	/// itself out around the picture, so the rectangle it fills is a different one under every
	/// Stretch and has to be taken when the frame is.
	/// </summary>
	/// <param name="name">The Gherkin name of the Image.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the picture {string} is captured as {string}")]
	public async Task When_the_picture_is_captured_as(string name, string captureName) =>
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, name, name).ConfigureAwait(false);

	/// <summary>
	/// Asserts that the place a picture filled in one capture no longer looks the way it did in
	/// another - the Stretch changed what reaches the panel, not only what the tree says.
	/// </summary>
	/// <param name="name">The Gherkin name of the Image.</param>
	/// <param name="fromCapture">The capture the picture's old place is taken from.</param>
	/// <param name="toCapture">The capture that must look different there.</param>
	[Then("where the picture {string} was in {string} looks different in {string}")]
	public void Then_where_the_picture_was_looks_different(string name, string fromCapture, string toCapture)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var was = before.PartRegion($"where the picture \"{name}\" was in capture \"{fromCapture}\"");
		var now = new Region(after.Frame, before.Part, $"that same place in capture \"{toCapture}\"");
		now.DiffersFrom(was);
	}

	/// <summary>Asserts that the strip along the right of a control is painted one colour.</summary>
	/// <param name="width">How many pixels wide the strip is.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="color">The colour the strip must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the rightmost {int} pixels of {string} are uniformly {string}")]
	public async Task Then_the_rightmost_pixels_of_are_uniformly(int width, string name, Color color) =>
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false))
			.RightStrip(width).IsUniformly(color);

	/// <summary>Asserts that the strip along the top of a control was left empty.</summary>
	/// <param name="height">How many pixels tall the strip is.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the topmost {int} pixels of {string} are blank")]
	public async Task Then_the_topmost_pixels_of_are_blank(int height, string name) =>
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false))
			.TopStrip(height).IsBlank();

	/// <summary>Asserts that the strip along the bottom of a control was left empty.</summary>
	/// <param name="height">How many pixels tall the strip is.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the bottommost {int} pixels of {string} are blank")]
	public async Task Then_the_bottommost_pixels_of_are_blank(int height, string name) =>
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false))
			.BottomStrip(height).IsBlank();

	// --------------------------------------------------------------- inner

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static async Task SettleAsync(TimeSpan delay)
	{
		await Task.Delay(delay, TestContext.Current.CancellationToken).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private bool StateAccepted() =>
		_scenarioContext.TryGetValue(StateAcceptedKey, out var stored) && stored is true;

	private static void AssertDarker(Color actual, Color reference, string what, string name) =>
		CanvasAssert.Luminance(actual).Should().BeLessThan(CanvasAssert.Luminance(reference),
			"the {0} of \"{1}\" is {2} and must be darker than {3}",
			what, name, ColorMatch.Describe(actual), ColorMatch.Describe(reference));

	private static void AssertLighter(Color actual, Color reference, string what, string name) =>
		CanvasAssert.Luminance(actual).Should().BeGreaterThan(CanvasAssert.Luminance(reference),
			"the {0} of \"{1}\" is {2} and must be lighter than {3}",
			what, name, ColorMatch.Describe(actual), ColorMatch.Describe(reference));
}
