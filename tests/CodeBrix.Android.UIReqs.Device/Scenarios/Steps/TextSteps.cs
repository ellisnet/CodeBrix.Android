using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.System;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The steps the Text scenarios speak: building runs, typing on the panel's keyboard, and
/// saying where a piece of text's ink sits inside its own block.
/// <para>
/// What a control HOLDS is always asserted on the tree (Text, Password, SelectedText); only
/// how it LOOKS is asserted on pixels.
/// </para>
/// </summary>
[Binding]
public sealed class TextSteps
{
	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public TextSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the text controls and text properties the Text scenarios
	/// name. Registration is idempotent and goes through the factory's public seam.
	/// </summary>
	[BeforeScenario(Order = 1)]
	public static void Register_the_text_vocabulary() => TextVocabulary.Ensure();

	// ------------------------------------------------------------------ runs

	/// <summary>Replaces a TextBlock's content with a series of runs, each with its own colour.</summary>
	/// <param name="name">The Gherkin name of the TextBlock.</param>
	/// <param name="runs">A Text/Foreground table, one row per run.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[Given("the TextBlock {string} has runs:")]
	[When("the TextBlock {string} has runs:")]
	public async Task Given_the_TextBlock_has_runs(string name, DataTable runs)
	{
		var rows = ReadRows(runs);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var block = ElementRegistry.Resolve(name) as TextBlock
				?? throw new NotSupportedException($"\"{name}\" is not a TextBlock, so it has no runs.");
			block.Inlines.Clear();
			foreach (var (text, foreground) in rows)
			{
				block.Inlines.Add(NewRun(text, foreground));
			}

			block.UpdateLayout();
		}).ConfigureAwait(false);
	}

	// --------------------------------------------------------------- keyboard

	/// <summary>Types on the panel's keyboard, which is where the focused control reads it from.</summary>
	/// <param name="text">The text to type.</param>
	/// <returns>A task that completes once the keys have been delivered and the UI thread is idle.</returns>
	[When("the text {string} is typed")]
	public async Task When_the_text_is_typed(string text)
	{
		TestTargetFixture.Session.TypeText(text);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Presses and releases one key on the panel's keyboard.</summary>
	/// <param name="key">The key to press.</param>
	/// <returns>A task that completes once the key has been delivered and the UI thread is idle.</returns>
	[When("the key {string} is pressed")]
	public async Task When_the_key_is_pressed(VirtualKey key)
	{
		TestTargetFixture.Session.KeyDown(key);
		TestTargetFixture.Session.KeyUp(key);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Presses and releases one key while the Control key is held down. The panel's keyboard
	/// carries no modifier field of its own: the modifier state is what the modifier keys that
	/// are down say it is, exactly as it is on a real head, so a chord is the modifier pressed,
	/// the key pressed and released, and the modifier let go - in that order.
	/// </summary>
	/// <param name="key">The key to press.</param>
	/// <returns>A task that completes once the keys have been delivered and the UI thread is idle.</returns>
	[When("the key {string} is pressed with the Control key held down")]
	public async Task When_the_key_is_pressed_with_the_Control_key_held_down(VirtualKey key) =>
		await ChordAsync(key, control: true, shift: false).ConfigureAwait(false);

	/// <summary>Presses and releases one key while the Shift key is held down.</summary>
	/// <param name="key">The key to press.</param>
	/// <returns>A task that completes once the keys have been delivered and the UI thread is idle.</returns>
	[When("the key {string} is pressed with the Shift key held down")]
	public async Task When_the_key_is_pressed_with_the_Shift_key_held_down(VirtualKey key) =>
		await ChordAsync(key, control: false, shift: true).ConfigureAwait(false);

	/// <summary>
	/// Presses and releases one key while both the Control and the Shift keys are held down -
	/// the shape of chord a control reserves for itself precisely because the plain one already
	/// means something to whatever it is hosting.
	/// </summary>
	/// <param name="key">The key to press.</param>
	/// <returns>A task that completes once the keys have been delivered and the UI thread is idle.</returns>
	[When("the key {string} is pressed with the Control and Shift keys held down")]
	public async Task When_the_key_is_pressed_with_the_Control_and_Shift_keys_held_down(VirtualKey key) =>
		await ChordAsync(key, control: true, shift: true).ConfigureAwait(false);

	/// <summary>Selects everything a text control holds, as a long press and drag would.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[When("all the text of {string} is selected")]
	public async Task When_all_the_text_of_is_selected(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var box = ElementRegistry.Resolve(name) as TextBox
				?? throw new NotSupportedException($"\"{name}\" is not a TextBox, so it has no selection.");
			box.SelectAll();
		}).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts that a control is the one the keyboard is talking to.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} has keyboard focus")]
	public async Task Then_has_keyboard_focus(string name)
	{
		var state = await FocusStateAsync(name).ConfigureAwait(false);
		state.Should().NotBe(FocusState.Unfocused, "\"{0}\" must have the focus", name);
	}

	/// <summary>Asserts that a control is not the one the keyboard is talking to.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} does not have keyboard focus")]
	public async Task Then_does_not_have_keyboard_focus(string name)
	{
		var state = await FocusStateAsync(name).ConfigureAwait(false);
		state.Should().Be(FocusState.Unfocused, "\"{0}\" must not have the focus", name);
	}

	// ------------------------------------------------------------ the tree

	/// <summary>Asserts what a PasswordBox holds. The password is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the PasswordBox.</param>
	/// <param name="expected">The password it must hold.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Password of {string} is {string}")]
	public async Task Then_the_Password_of_is(string name, string expected)
	{
		var actual = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			actual = ElementRegistry.Resolve(name) as PasswordBox is { } box
				? box.Password
				: throw new NotSupportedException($"\"{name}\" is not a PasswordBox.");
		}).ConfigureAwait(false);

		actual.Should().Be(expected, "the Password of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts what part of a TextBox is selected.</summary>
	/// <param name="name">The Gherkin name of the TextBox.</param>
	/// <param name="expected">The text that must be selected.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the SelectedText of {string} is {string}")]
	public async Task Then_the_SelectedText_of_is(string name, string expected)
	{
		var actual = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			actual = ElementRegistry.Resolve(name) as TextBox is { } box
				? box.SelectedText
				: throw new NotSupportedException($"\"{name}\" is not a TextBox.");
		}).ConfigureAwait(false);

		actual.Should().Be(expected, "the SelectedText of \"{0}\" was asserted", name);
	}

	// ------------------------------------------------------- ink of the text

	/// <summary>Asserts which side of its own rectangle a piece of text's ink sits against.</summary>
	/// <param name="name">The Gherkin name of the text element.</param>
	/// <param name="side">left, centre or right.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} sits at the {word} of its block")]
	public async Task Then_the_ink_of_sits_at_the_of_its_block(string name, string side)
	{
		var region = await RegionAsync(name).ConfigureAwait(false);
		switch (side.ToUpperInvariant())
		{
			case "LEFT":
				region.InkHugsLeft();
				break;
			case "RIGHT":
				region.InkHugsRight();
				break;
			case "CENTRE":
			case "CENTER":
				region.InkIsCentred();
				break;
			default:
				throw new FormatException($"\"{side}\" is not a side. Write left, centre or right.");
		}
	}

	/// <summary>Asserts that a piece of text's ink grew taller between two captured frames.</summary>
	/// <param name="name">The Gherkin name of the text element.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} in frame {string} is taller than in frame {string}")]
	public async Task Then_the_ink_of_in_frame_is_taller_than_in_frame(string name, string frameName,
		string otherFrameName)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		region.InkIsTallerThan(other);
	}

	/// <summary>
	/// Asserts that a piece of text's ink grew taller between two captured frames by at least a
	/// factor the scenario names. The plain "is taller than" step leaves the factor at the
	/// harness's default, which is the smallest growth that counts as growth at all; a doubled
	/// font size deserves a claim with a number in it.
	/// </summary>
	/// <param name="name">The Gherkin name of the text element.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="factor">How many times as tall the ink must be.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} in frame {string} is at least {float} times as tall as in frame {string}")]
	public async Task Then_the_ink_of_in_frame_is_at_least_times_as_tall_as_in_frame(string name, string frameName,
		float factor, string otherFrameName)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		region.InkIsTallerThan(other, factor);
	}

	/// <summary>Asserts that a piece of text's ink is the same height in two captured frames.</summary>
	/// <param name="name">The Gherkin name of the text element.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} in frame {string} is as tall as in frame {string}")]
	public async Task Then_the_ink_of_in_frame_is_as_tall_as_in_frame(string name, string frameName,
		string otherFrameName)
	{
		var region = await RegionAsync(name, frameName).ConfigureAwait(false);
		var other = await RegionAsync(name, otherFrameName).ConfigureAwait(false);
		region.InkIsAsTallAs(other);
	}

	// ---------------------------------------------------------------- inner

	private Task<Region> RegionAsync(string elementName, string frameName = ScenarioFrames.CurrentFrameName) =>
		ScenarioFrames.RegionAsync(_scenarioContext, elementName, frameName);

	private static async Task ChordAsync(VirtualKey key, bool control, bool shift)
	{
		var session = TestTargetFixture.Session;

		if (control)
		{
			session.KeyDown(VirtualKey.Control);
		}

		if (shift)
		{
			session.KeyDown(VirtualKey.Shift);
		}

		session.KeyDown(key);
		session.KeyUp(key);

		if (shift)
		{
			session.KeyUp(VirtualKey.Shift);
		}

		if (control)
		{
			session.KeyUp(VirtualKey.Control);
		}

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<FocusState> FocusStateAsync(string name)
	{
		var state = FocusState.Unfocused;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			state = ElementRegistry.Resolve(name) is Control control
				? control.FocusState
				: throw new NotSupportedException($"\"{name}\" is not a Control, so it has no focus state.");
		}).ConfigureAwait(false);

		return state;
	}

	private static Run NewRun(string text, string foreground)
	{
		var run = new Run { Text = text };
		if (!string.IsNullOrWhiteSpace(foreground))
		{
			run.Foreground = new SolidColorBrush(Colors.Parse(foreground));
		}

		return run;
	}

	private static IReadOnlyList<(string Text, string Foreground)> ReadRows(DataTable table)
	{
		ArgumentNullException.ThrowIfNull(table);

		var textColumn = table.Header.First();
		var colorColumn = table.Header.Last();
		return table.Rows
			.Select(row => (row[textColumn], colorColumn == textColumn ? string.Empty : row[colorColumn]))
			.ToArray();
	}
}
