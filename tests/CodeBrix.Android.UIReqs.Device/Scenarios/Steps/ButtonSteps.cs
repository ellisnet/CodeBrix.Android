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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The Buttons group's steps: the buttons and toggles, the commands they run, the flyouts they
/// open, and the gestures a touch panel can make. Every pattern here names the control it
/// belongs to, so that no other coverage group's sentence can match one of them.
/// <para>
/// The panel is touch-only, so there is no hover: a requirement about a pointer-over look
/// cannot be written here, and the pressed look is reached with a real press and release
/// rather than with a mouse.
/// </para>
/// </summary>
[Binding]
public sealed class ButtonSteps
{
	/// <summary>
	/// How long a press is left in place before the frame that shows the pressed look is
	/// asked for. A visual state change is not instantaneous on a real panel, and this is the
	/// scenario's way of saying "the finger stayed down".
	/// </summary>
	public static readonly TimeSpan PressSettleDelay = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// How far from the edge of the panel a "somewhere else" tap lands, so that the tap is
	/// well inside the panel but nowhere near the control the scenario is talking about.
	/// </summary>
	public const int PanelEdgeInset = 24;

	/// <summary>The template part a ToggleSwitch's knob is drawn as.</summary>
	public const string ToggleSwitchKnobPart = "SwitchKnob";

	/// <summary>The template part a ToggleSwitch's track is drawn as.</summary>
	public const string ToggleSwitchTrackPart = "SwitchKnobBounds";

	/// <summary>
	/// The template part of a ToggleSwitch that a finger has to land on. A ToggleSwitch is
	/// wider than its switch - the rest of it belongs to the header and the on/off content -
	/// so the middle of the control is not necessarily the middle of the switch.
	/// </summary>
	public const string ToggleSwitchAreaPart = "SwitchAreaGrid";

	/// <summary>The template part a CheckBox's box is drawn as.</summary>
	public const string CheckBoxBoxPart = "NormalRectangle";

	/// <summary>The template part a CheckBox's glyph is drawn in.</summary>
	public const string CheckBoxGlyphPart = "CheckGlyph";

	/// <summary>
	/// How far inside the box the glyph area is measured. The glyph part covers the box
	/// exactly, so its outermost pixels are the box's own outline; a requirement about a glyph
	/// is a requirement about what is drawn INSIDE the box.
	/// </summary>
	public const int CheckBoxGlyphInset = 4;

	/// <summary>
	/// How long the ToggleSwitch is given to finish its state animation before the frame that
	/// shows the new state is asked for. The template moves the knob at once but fades the
	/// track's fill in over a theme duration, so a frame taken immediately would show a switch
	/// caught mid-change.
	/// </summary>
	public static readonly TimeSpan SwitchAnimationDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>The template part a SplitButton's primary half is.</summary>
	public const string SplitButtonPrimaryPart = "PrimaryButton";

	/// <summary>The template part a SplitButton's drop-down half is.</summary>
	public const string SplitButtonSecondaryPart = "SecondaryButton";

	/// <summary>
	/// How long the panel is left alone before a finger lands on a template part. A control
	/// that has only just been built and laid out is not yet listening for a gesture, and a
	/// real finger never arrives in the same instant as the control it touches.
	/// </summary>
	public static readonly TimeSpan BeforeTapDelay = TimeSpan.FromMilliseconds(250);

	private const int PointerId = 0;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public ButtonSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the Buttons group's controls and binds the theme colour
	/// names, once the application is up and its resource dictionaries exist.
	/// </summary>
	/// <returns>A task that completes once a feature file may name those controls and colours.</returns>
	[BeforeTestRun(Order = 10)]
	public static async Task Register_the_buttons_and_toggles()
	{
		ButtonElements.Register();
		await ThemeColors.RegisterAsync().ConfigureAwait(false);
	}

	/// <summary>Starts every scenario with no command of its own.</summary>
	/// <remarks>
	/// A flyout this class opens needs no bookkeeping here: the scenario reset in
	/// <c>ScenarioHooks</c> closes every popup that is open, whoever opened it.
	/// </remarks>
	[BeforeScenario(Order = 1)]
	public static void Forget_what_the_last_scenario_named() => TestCommands.Clear();

	// ------------------------------------------------------------- commands

	/// <summary>Names a command that says it can execute.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	[Given("the command {string} can execute")]
	public void Given_the_command_can_execute(string commandName) =>
		TestCommands.Declare(commandName).IsExecutable = true;

	/// <summary>Names a command that says it cannot execute.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	[Given("the command {string} cannot execute")]
	public void Given_the_command_cannot_execute(string commandName) =>
		TestCommands.Declare(commandName).IsExecutable = false;

	/// <summary>Makes a command executable, which is what a bound control listens for.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	/// <returns>A task that completes once the UI thread has seen the change.</returns>
	[When("the command {string} becomes executable")]
	public Task When_the_command_becomes_executable(string commandName) =>
		ChangeExecutabilityAsync(commandName, executable: true);

	/// <summary>Makes a command refuse to execute.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	/// <returns>A task that completes once the UI thread has seen the change.</returns>
	[When("the command {string} stops being executable")]
	public Task When_the_command_stops_being_executable(string commandName) =>
		ChangeExecutabilityAsync(commandName, executable: false);

	/// <summary>Asserts that a command ran exactly once.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	[Then("the command {string} was executed once")]
	public void Then_the_command_was_executed_once(string commandName) =>
		AssertExecutionCount(commandName, 1);

	/// <summary>Asserts how often a command ran.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	/// <param name="times">How often it must have run.</param>
	[Then("the command {string} was executed {int} times")]
	public void Then_the_command_was_executed_times(string commandName, int times) =>
		AssertExecutionCount(commandName, times);

	/// <summary>Asserts that a command never ran.</summary>
	/// <param name="commandName">The Gherkin name of the command.</param>
	[Then("the command {string} was not executed")]
	public void Then_the_command_was_not_executed(string commandName) =>
		AssertExecutionCount(commandName, 0);

	// --------------------------------------------------------- button state

	/// <summary>Asserts that a button will take a tap.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the button {string} is enabled")]
	public async Task Then_the_button_is_enabled(string name) =>
		(await IsEnabledAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("\"{0}\" must be enabled", name);

	/// <summary>Asserts that a button will not take a tap.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the button {string} is disabled")]
	public async Task Then_the_button_is_disabled(string name) =>
		(await IsEnabledAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("\"{0}\" must be disabled", name);

	/// <summary>Asserts that a two-state or three-state toggle is checked.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the toggle {string} is checked")]
	public async Task Then_the_toggle_is_checked(string name) =>
		(await IsCheckedAsync(name).ConfigureAwait(false)).Should()
			.Be(true, "\"{0}\" must be checked", name);

	/// <summary>Asserts that a two-state or three-state toggle is not checked.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the toggle {string} is not checked")]
	public async Task Then_the_toggle_is_not_checked(string name) =>
		(await IsCheckedAsync(name).ConfigureAwait(false)).Should()
			.Be(false, "\"{0}\" must not be checked", name);

	/// <summary>Asserts that a three-state toggle is in its indeterminate state.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the toggle {string} is indeterminate")]
	public async Task Then_the_toggle_is_indeterminate(string name) =>
		(await IsCheckedAsync(name).ConfigureAwait(false)).Should()
			.BeNull("\"{0}\" must be indeterminate", name);

	/// <summary>Asserts that a ToggleSwitch is on.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToggleSwitch {string} is on")]
	public async Task Then_the_ToggleSwitch_is_on(string name) =>
		(await IsOnAsync(name).ConfigureAwait(false)).Should().BeTrue("\"{0}\" must be on", name);

	/// <summary>Asserts that a ToggleSwitch is off.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToggleSwitch {string} is off")]
	public async Task Then_the_ToggleSwitch_is_off(string name) =>
		(await IsOnAsync(name).ConfigureAwait(false)).Should().BeFalse("\"{0}\" must be off", name);

	// -------------------------------------------------------------- events

	/// <summary>Asserts that a control raised Click at least this often.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">The fewest raisings that satisfy the requirement.</param>
	[Then("the Click of {string} was raised at least {int} times")]
	public void Then_the_Click_of_was_raised_at_least_times(string name, int times) =>
		EventRecorder.Count(name, "Click").Should().BeGreaterThanOrEqualTo(times,
			"the Click of \"{0}\" was asserted; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));

	/// <summary>Asserts that a ToggleSwitch raised Toggled exactly once.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	[Then("the Toggled of {string} was raised once")]
	public void Then_the_Toggled_of_was_raised_once(string name) => AssertEventCount(name, "Toggled", 1);

	/// <summary>Asserts how often a ToggleSwitch raised Toggled.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the Toggled of {string} was raised {int} times")]
	public void Then_the_Toggled_of_was_raised_times(string name, int times) =>
		AssertEventCount(name, "Toggled", times);

	/// <summary>Asserts how often a toggle raised Checked.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the Checked of {string} was raised {int} times")]
	public void Then_the_Checked_of_was_raised_times(string name, int times) =>
		AssertEventCount(name, "Checked", times);

	/// <summary>Asserts how often a toggle raised Unchecked.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the Unchecked of {string} was raised {int} times")]
	public void Then_the_Unchecked_of_was_raised_times(string name, int times) =>
		AssertEventCount(name, "Unchecked", times);

	// ------------------------------------------------------------ gestures

	/// <summary>Puts a finger down on the middle of a control and leaves it there.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the press has settled.</returns>
	[When("a finger is pressed on {string}")]
	public async Task When_a_finger_is_pressed_on(string name)
	{
		var (x, y) = await CenterOfAsync(name).ConfigureAwait(false);
		TestTargetFixture.Session.TouchPress(PointerId, x, y);
		await SettleAsync(PressSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Lifts the finger off a control at the point it was pressed.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the release has settled.</returns>
	[When("the finger on {string} is lifted")]
	public async Task When_the_finger_on_is_lifted(string name)
	{
		var (x, y) = await CenterOfAsync(name).ConfigureAwait(false);
		TestTargetFixture.Session.TouchRelease(PointerId, x, y);
		await SettleAsync(PressSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Holds a finger on a control for a real length of time, then lifts it.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="milliseconds">How long the finger stays down.</param>
	/// <returns>A task that completes once the finger has been lifted and the UI thread is idle.</returns>
	[When("a finger is held on {string} for {int} milliseconds")]
	public async Task When_a_finger_is_held_on_for_milliseconds(string name, int milliseconds)
	{
		var (x, y) = await CenterOfAsync(name).ConfigureAwait(false);
		TestTargetFixture.Session.TouchPress(PointerId, x, y);
		await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), Xunit.TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		TestTargetFixture.Session.TouchRelease(PointerId, x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Taps a point of the panel that is nowhere near a control - the corner furthest from it -
	/// which is how a finger dismisses an open flyout.
	/// </summary>
	/// <param name="name">The Gherkin name of the control to stay away from.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("a point far from {string} is tapped")]
	public async Task When_a_point_far_from_is_tapped(string name)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		var (width, height) = TestTargetFixture.PanelSize;
		var (centerX, centerY) = bounds.Center;
		var x = centerX < width / 2 ? width - PanelEdgeInset : PanelEdgeInset;
		var y = centerY < height / 2 ? height - PanelEdgeInset : PanelEdgeInset;

		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Taps the switch itself, which is the part of a ToggleSwitch a finger works.</summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the switch of the ToggleSwitch {string} is tapped")]
	public Task When_the_switch_of_the_ToggleSwitch_is_tapped(string name) =>
		TapPartAsync(name, ToggleSwitchAreaPart);

	/// <summary>Taps the half of a SplitButton that raises Click.</summary>
	/// <param name="name">The Gherkin name of the SplitButton.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the primary half of the SplitButton {string} is tapped")]
	public Task When_the_primary_half_of_the_SplitButton_is_tapped(string name) =>
		TapPartAsync(name, SplitButtonPrimaryPart);

	/// <summary>Taps the half of a SplitButton that opens its flyout.</summary>
	/// <param name="name">The Gherkin name of the SplitButton.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the drop-down half of the SplitButton {string} is tapped")]
	public Task When_the_drop_down_half_of_the_SplitButton_is_tapped(string name) =>
		TapPartAsync(name, SplitButtonSecondaryPart);

	// ------------------------------------------------------------ CheckBox

	/// <summary>Asserts that nothing is drawn on a CheckBox's box.</summary>
	/// <param name="name">The Gherkin name of the CheckBox.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the box of the CheckBox {string} shows no glyph")]
	public async Task Then_the_box_of_the_CheckBox_shows_no_glyph(string name) =>
		(await GlyphRegionAsync(name).ConfigureAwait(false)).ShowsASingleFlatColor();

	/// <summary>Asserts that a glyph is drawn on a CheckBox's box.</summary>
	/// <param name="name">The Gherkin name of the CheckBox.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the box of the CheckBox {string} shows a glyph")]
	public async Task Then_the_box_of_the_CheckBox_shows_a_glyph(string name) =>
		(await GlyphRegionAsync(name).ConfigureAwait(false)).ShowsMoreThanOneColor();

	/// <summary>Asserts what colour the glyph on a CheckBox's box is drawn in.</summary>
	/// <param name="name">The Gherkin name of the CheckBox.</param>
	/// <param name="color">The colour the glyph must contain.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the box of the CheckBox {string} shows a glyph in {string}")]
	public async Task Then_the_box_of_the_CheckBox_shows_a_glyph_in(string name, Color color)
	{
		var region = await GlyphRegionAsync(name).ConfigureAwait(false);
		region.ShowsMoreThanOneColor();
		region.Contains(color);
	}

	/// <summary>Asserts what colour a CheckBox's box is filled with.</summary>
	/// <param name="name">The Gherkin name of the CheckBox.</param>
	/// <param name="color">The colour the box must be mostly filled with.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the box of the CheckBox {string} is filled with {string}")]
	public async Task Then_the_box_of_the_CheckBox_is_filled_with(string name, Color color) =>
		(await BoxRegionAsync(name).ConfigureAwait(false)).Contains(color, minFraction: 0.5);

	/// <summary>Asserts that a CheckBox's box carries none of a colour.</summary>
	/// <param name="name">The Gherkin name of the CheckBox.</param>
	/// <param name="color">The colour the box must not carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the box of the CheckBox {string} is not filled with {string}")]
	public async Task Then_the_box_of_the_CheckBox_is_not_filled_with(string name, Color color) =>
		(await BoxRegionAsync(name).ConfigureAwait(false)).DoesNotContain(color);

	// -------------------------------------------------------- ToggleSwitch

	/// <summary>
	/// Captures a frame and remembers where the ToggleSwitch's knob was in it, because the
	/// knob is somewhere else in the next frame.
	/// </summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the ToggleSwitch knob of {string} is captured as {string}")]
	public async Task When_the_ToggleSwitch_knob_is_captured_as(string name, string captureName)
	{
		await SettleAsync(SwitchAnimationDelay).ConfigureAwait(false);
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, ToggleSwitchKnobPart, ToggleSwitchTrackPart)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that the place the knob used to occupy no longer looks the way it did - the
	/// pixel-level half of "the knob moved", which the rectangles alone cannot say.
	/// </summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="fromCapture">The capture the knob's old place is taken from.</param>
	/// <param name="toCapture">The capture that must look different there.</param>
	[Then("where the ToggleSwitch knob of {string} was in {string} looks different in {string}")]
	public void Then_where_the_ToggleSwitch_knob_was_looks_different(string name, string fromCapture, string toCapture)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var was = before.PartRegion($"where the knob of the ToggleSwitch \"{name}\" was in capture \"{fromCapture}\"");
		var now = new Region(after.Frame, before.Part,
			$"that same place in capture \"{toCapture}\"");
		now.DiffersFrom(was);
	}

	/// <summary>Asserts what colour a ToggleSwitch's track is painted in a capture.</summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="color">The colour most of the track must be.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("the ToggleSwitch track of {string} is filled with {string} in {string}")]
	public void Then_the_ToggleSwitch_track_is_filled_with(string name, Color color, string captureName) =>
		TrackRegion(name, captureName).Contains(color, minFraction: 0.4);

	/// <summary>Asserts that a ToggleSwitch's track is not painted a colour in a capture.</summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="color">The colour the track must not carry.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("the ToggleSwitch track of {string} is not filled with {string} in {string}")]
	public void Then_the_ToggleSwitch_track_is_not_filled_with(string name, Color color, string captureName) =>
		TrackRegion(name, captureName).DoesNotContain(color);

	/// <summary>Asserts that the ToggleSwitch's knob was actually drawn in a capture.</summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("the ToggleSwitch knob of {string} had ink in {string}")]
	public void Then_the_ToggleSwitch_knob_had_ink_in(string name, string captureName) =>
		CapturedParts.Get(_scenarioContext, captureName)
			.PartRegion($"the knob of the ToggleSwitch \"{name}\" in capture \"{captureName}\"")
			.HasInk();

	/// <summary>Asserts that the ToggleSwitch's knob moved to the right between two captures.</summary>
	/// <param name="name">The Gherkin name of the ToggleSwitch.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	/// <param name="minimum">The fewest device pixels the knob must have travelled.</param>
	[Then("the ToggleSwitch knob of {string} moved right from {string} to {string} by at least {int} pixels")]
	public void Then_the_ToggleSwitch_knob_moved_right(string name, string fromCapture, string toCapture, int minimum) =>
		AssertMovedRight("the knob of the ToggleSwitch", name, fromCapture, toCapture, minimum);

	// -------------------------------------------------------------- flyouts

	/// <summary>
	/// Shows a button that carries a flyout whose whole content is one panel of a known
	/// colour, so that "the flyout is showing" is something a person can see on the panel.
	/// </summary>
	/// <param name="kind">The element kind, which must be a button that owns a flyout.</param>
	/// <param name="name">The name the scenario refers to the button by.</param>
	/// <param name="panelName">The name the scenario refers to the flyout's panel by.</param>
	/// <param name="width">The panel's width in logical pixels.</param>
	/// <param name="height">The panel's height in logical pixels.</param>
	/// <param name="color">The colour the panel is painted.</param>
	/// <returns>A task that completes once the button is showing.</returns>
	[Given("the application shows a {word} named {string} with a flyout panel named {string} {int} by {int} painted {string}")]
	public async Task Given_the_application_shows_a_button_with_a_flyout_panel(
		string kind, string name, string panelName, int width, int height, Color color)
	{
		var element = await ElementFactory.CreateAsync(kind, name,
			new[] { new KeyValuePair<string, string>("Content", "Menu") }).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = new Border
			{
				Name = panelName,
				Width = width,
				Height = height,
				Background = new SolidColorBrush(color),
			};
			ElementRegistry.Register(panelName, panel);

			var flyout = new Flyout { Content = panel };
			switch (element)
			{
				case SplitButton split:
					split.Flyout = flyout;
					break;
				case Button button:
					button.Flyout = flyout;
					break;
				default:
					throw new NotSupportedException(
						$"A {element.GetType().Name} named \"{name}\" carries no flyout the harness can set.");
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Asserts that a button's flyout is showing.</summary>
	/// <param name="name">The Gherkin name of the button.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the flyout of {string} is open")]
	public async Task Then_the_flyout_of_is_open(string name) =>
		(await IsFlyoutOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the flyout of \"{0}\" must be open", name);

	/// <summary>Asserts that a button's flyout is not showing.</summary>
	/// <param name="name">The Gherkin name of the button.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the flyout of {string} is closed")]
	public async Task Then_the_flyout_of_is_closed(string name) =>
		(await IsFlyoutOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the flyout of \"{0}\" must be closed", name);

	// --------------------------------------------------------- RadioButtons

	/// <summary>
	/// Shows several RadioButtons at once, which is the only way a requirement about a group
	/// can be stated: exclusivity is a statement about more than one control.
	/// </summary>
	/// <param name="buttons">A Name/Group/Content table, one row per RadioButton.</param>
	/// <returns>A task that completes once the group is showing.</returns>
	[Given("the application shows a RadioButton group with:")]
	public async Task Given_the_application_shows_a_RadioButton_group(DataTable buttons)
	{
		ArgumentNullException.ThrowIfNull(buttons);

		var rows = buttons.Rows
			.Select(row => (Name: row["Name"], Group: row["Group"], Content: row["Content"]))
			.ToArray();

		StackPanel panel = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			panel = new StackPanel
			{
				Name = "radioGroup",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			ElementRegistry.Register("radioGroup", panel);

			foreach (var row in rows)
			{
				var button = new RadioButton
				{
					Name = row.Name,
					GroupName = row.Group,
					Content = row.Content,
				};
				button.Checked += (_, _) => EventRecorder.Record(row.Name, "Checked");
				button.Unchecked += (_, _) => EventRecorder.Record(row.Name, "Unchecked");
				ElementRegistry.Register(row.Name, button);
				panel.Children.Add(button);
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(panel).ConfigureAwait(false);
	}

	// ---------------------------------------------------------------- inner

	private static void AssertEventCount(string name, string eventName, int expected) =>
		EventRecorder.Count(name, eventName).Should().Be(expected,
			"the {0} of \"{1}\" was asserted; the scenario recorded [{2}]",
			eventName, name, string.Join(", ", EventRecorder.Recorded));

	private static void AssertExecutionCount(string commandName, int expected) =>
		TestCommands.Resolve(commandName).ExecutionCount.Should().Be(expected,
			"the executions of the command \"{0}\" were asserted; the scenario named [{1}]",
			commandName, string.Join(", ", TestCommands.Described));

	private void AssertMovedRight(string what, string name, string fromCapture, string toCapture, int minimum)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var travelled = after.Part.X - before.Part.X;

		travelled.Should().BeGreaterThanOrEqualTo(minimum,
			"{0} \"{1}\" must move right: it was at {2} in capture \"{3}\" and at {4} in capture \"{5}\"",
			what, name, before.Part, fromCapture, after.Part, toCapture);
	}

	private static async Task ChangeExecutabilityAsync(string commandName, bool executable)
	{
		var command = TestCommands.Resolve(commandName);
		await TestTargetFixture.RunOnUIThreadAsync(() => command.IsExecutable = executable).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<bool> IsEnabledAsync(string name)
	{
		var enabled = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			enabled = ElementRegistry.Resolve(name) switch
			{
				Control control => control.IsEnabled,
				_ => throw new NotSupportedException($"\"{name}\" is not a control, so it has no IsEnabled."),
			};
		}).ConfigureAwait(false);

		return enabled;
	}

	private static async Task<bool?> IsCheckedAsync(string name)
	{
		bool? state = null;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			state = ElementRegistry.Resolve(name) switch
			{
				ToggleButton toggle => toggle.IsChecked,
				_ => throw new NotSupportedException($"\"{name}\" is not a toggle, so it has no IsChecked."),
			};
		}).ConfigureAwait(false);

		return state;
	}

	private static async Task<bool> IsOnAsync(string name)
	{
		var on = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			on = ElementRegistry.Resolve(name) switch
			{
				ToggleSwitch toggle => toggle.IsOn,
				_ => throw new NotSupportedException($"\"{name}\" is not a ToggleSwitch, so it has no IsOn."),
			};
		}).ConfigureAwait(false);

		return on;
	}

	private static async Task<bool> IsFlyoutOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var flyout = ElementRegistry.Resolve(name) switch
			{
				SplitButton split => split.Flyout,
				Button button => button.Flyout,
				_ => throw new NotSupportedException($"\"{name}\" carries no flyout."),
			};

			open = flyout is { IsOpen: true };
		}).ConfigureAwait(false);

		return open;
	}

	private static async Task<(int X, int Y)> CenterOfAsync(string name)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		return bounds.Center;
	}

	private static async Task TapPartAsync(string name, string partName)
	{
		// The part is inside the control's template, so it resolves through the visual tree
		// rather than through the scenario's own names.
		ElementRegistry.Resolve(name);
		var (x, y) = await CenterOfAsync(partName).ConfigureAwait(false);
		await SettleAsync(BeforeTapDelay).ConfigureAwait(false);
		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task SettleAsync(TimeSpan delay)
	{
		await Task.Delay(delay, Xunit.TestContext.Current.CancellationToken).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private Region TrackRegion(string name, string captureName) =>
		CapturedParts.Get(_scenarioContext, captureName)
			.ContainerRegion($"the track of the ToggleSwitch \"{name}\" in capture \"{captureName}\"");

	private Task<Region> BoxRegionAsync(string name) =>
		PartRegionAsync(name, CheckBoxBoxPart, "the box", DeviceRect.DefaultInset);

	private Task<Region> GlyphRegionAsync(string name) =>
		PartRegionAsync(name, CheckBoxGlyphPart, "the inside of the box", CheckBoxGlyphInset);

	private async Task<Region> PartRegionAsync(string name, string partName, string what, int inset)
	{
		ElementRegistry.Resolve(name);
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(partName), inset).ConfigureAwait(false);
		var frame = ScenarioFrames.Current(_scenarioContext);
		return new Region(frame, bounds, string.Create(CultureInfo.InvariantCulture,
			$"{what} of \"{name}\""));
	}
}
