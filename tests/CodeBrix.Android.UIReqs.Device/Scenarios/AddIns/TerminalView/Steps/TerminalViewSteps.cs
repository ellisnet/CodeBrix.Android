using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.TerminalView.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.UI.TextLayout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Reqnroll;
using SilverAssertions;
using SkiaSharp;
using Windows.UI;
using CellMetrics = CodeBrix.Platform.UI.TerminalView.Rendering.CellMetrics;
using Colors = CodeBrix.Platform.UI.Core.UIReqs.Support.Colors;
using TerminalControlElement = CodeBrix.Platform.UI.TerminalView.TerminalControl;
using TerminalPalette = CodeBrix.Terminal.Engine.Color;

namespace CodeBrix.Platform.UI.AddIn.TerminalView.UIReqs.Steps;

/// <summary>
/// The TerminalView group's vocabulary: the terminal control, the scripts of VT output it is
/// fed, the keyboard chords and the finger that reach it, and the facts only a terminal can be
/// asked - how big its grid came out, what it emitted, what it copied, what it called itself,
/// and what one CELL of the grid looks like.
/// <para>
/// A terminal does not expose its buffer, so every claim about content here is a claim about
/// pixels or about an event. The cell and row steps exist because a scenario has no other way
/// to point at a character: the grid is the control's own geometry, worked out from the
/// monospace cell it measured, and nothing outside the control knows where a cell is.
/// </para>
/// <para>
/// Everything else a scenario says - showing the control, sizing its cell, capturing a frame,
/// pressing a key, typing, and every claim about a region or a frame - is the core harness's
/// own vocabulary, reached through this project's reqnroll.json binding assemblies.
/// </para>
/// </summary>
[Binding]
public sealed class TerminalViewSteps
{
	/// <summary>The name a feature file builds the terminal control with.</summary>
	public const string TerminalKind = "Terminal";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-terminalview</c> tag:
	/// the add-in assembly, its VT engine and the monospace font package they all arrive with. A
	/// machine that can build this project has them, so nothing is ever skipped for it here - but
	/// a run whose add-in could not be loaded reports skips with a reason instead of a dozen
	/// failures about an unknown element kind.
	/// </summary>
	public const string TerminalViewPrerequisite = "terminalview";

	/// <summary>The feature-file name of the palette's dark red, which SGR 31 selects.</summary>
	public const string PaletteRed = "PaletteRed";

	/// <summary>
	/// The feature-file name of the palette's bright red, which SGR 31 selects once the run is
	/// also BOLD - the classic bold-as-bright promotion.
	/// </summary>
	public const string PaletteBrightRed = "PaletteBrightRed";

	/// <summary>The palette index SGR 31 selects.</summary>
	public const int PaletteRedIndex = 1;

	/// <summary>The palette index a bold SGR 31 run is promoted to.</summary>
	public const int PaletteBrightRedIndex = 9;

	/// <summary>
	/// How far inside a cell a colour claim looks. A cell is a fraction of a pixel wider than a
	/// whole number of pixels, so the boundary column between two cells belongs to neither of
	/// them as far as a requirement is concerned.
	/// </summary>
	public const int CellInset = 1;

	/// <summary>The fewest columns the control will lay a grid out in, however small it is.</summary>
	public const int MinimumColumns = 4;

	/// <summary>The fewest rows the control will lay a grid out in.</summary>
	public const int MinimumRows = 2;

	/// <summary>The font size the warm-up measures the cell at.</summary>
	public const float WarmupFontSize = 24f;

	private static readonly object RegistrationLock = new();

	private static readonly object TrafficLock = new();

	private static readonly Dictionary<string, TerminalTraffic> Traffic = new(StringComparer.Ordinal);

	private static readonly TextFontWeight[] WarmupWeights = [TextFontWeight.Normal, TextFontWeight.Bold];

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public TerminalViewSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// Every property goes in as a TYPED setter. None of these words is universal, but the typed
	/// overload is what guarantees that: a word registered for the terminal control alone cannot
	/// be taken away from another coverage group, whatever it decides to call its own colours.
	/// </para>
	/// <para>
	/// The two palette colours are registered by NAME from the engine's own table rather than
	/// written out as hexadecimal, so a scenario says "PaletteRed" and means the colour SGR 31
	/// actually selects.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_TerminalView_vocabulary()
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
				Prerequisite.Missing(TerminalViewPrerequisite,
					$"the TerminalView add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	/// <summary>
	/// Lays a throwaway string out in the monospace face, in both weights the scenarios draw
	/// with, before the first scenario measures a cell.
	/// <para>
	/// The grid dimensions come from a real text measurement: the control measures one glyph in
	/// the terminal font and divides its own size by the result. The text engine answers the
	/// first measurement through a face it has not loaded yet with an interim one, so a control
	/// laid out in that moment would fit its grid to the wrong cell and never re-measure. This
	/// is one real measurement per weight, awaited, on the UI thread. It is not a sleep.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once the face has been measured with.</returns>
	[BeforeTestRun(Order = 11)]
	public static async Task Warm_the_terminal_font()
	{
		if (Prerequisite.IsMissing(TerminalViewPrerequisite, out _))
		{
			return;
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var cell = CellMetrics.Measure(TerminalScripts.TerminalFont, WarmupFontSize);
			if (cell.Width <= 1f || cell.Height <= 1f)
			{
				// A font that measures to nothing is the silent disaster the family rule about
				// system fonts exists to prevent: the grid would come out as the control's
				// minimum and every cell claim would point at the wrong pixels.
				var measured = string.Create(CultureInfo.InvariantCulture, $"{cell.Width} x {cell.Height}");
				throw new InvalidOperationException(
					$"A terminal cell in \"{TerminalScripts.TerminalFont}\" measured {measured}, so the "
					+ "grid would be nonsense. Is the .ttf (and its .ttf.manifest) beside the test "
					+ "executable at the path the URI resolves to?");
			}

			foreach (var weight in WarmupWeights)
			{
				using var warm = TextLayoutEngine.Layout(
					[new TextRunDescriptor(FontWarmup.WarmupText, TerminalScripts.TerminalFont,
						WarmupFontSize, weight)]);
				if (warm.Size.Width > 0f && warm.Size.Height > 0f)
				{
					continue;
				}

				var measured = string.Create(CultureInfo.InvariantCulture,
					$"{warm.Size.Width} x {warm.Size.Height}");
				throw new InvalidOperationException(
					$"The engine laid text out in {weight} to {measured} through "
					+ $"\"{TerminalScripts.TerminalFont}\", so nothing would be painted with it.");
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Forgets what the terminals of the previous scenario emitted, copied and called
	/// themselves. The core hooks forget the event COUNTS; what the events carried is this
	/// group's own record and is forgotten here, at the same moment.
	/// </summary>
	[BeforeScenario(Order = 2)]
	public static void Forget_the_terminal_traffic()
	{
		lock (TrafficLock)
		{
			Traffic.Clear();
		}
	}

	// ------------------------------------------------------------ feeding it

	/// <summary>
	/// Feeds a named script of VT output into a terminal, the way a transport's read loop would.
	/// <para>
	/// A terminal marshals what it is fed onto the UI thread and drops it silently if it has not
	/// been loaded onto the panel yet, so this step insists on a loaded control: a scenario that
	/// fed too early would otherwise fail much later, as "the terminal is blank", with nothing
	/// to say why.
	/// </para>
	/// </summary>
	/// <param name="scriptName">The fixture name, as <see cref="TerminalScripts"/> spells it.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes once the output has been parsed and drawn.</returns>
	[Given("the script {string} is fed to {string}")]
	[When("the script {string} is fed to {string}")]
	public async Task When_the_script_is_fed_to(string scriptName, string name) =>
		await FeedAsync(name, TerminalScripts.Build(scriptName)).ConfigureAwait(false);

	/// <summary>
	/// Turns the cursor off, which is what a host does before it draws something a person is
	/// meant to look at rather than type into.
	/// <para>
	/// Every scenario that compares two frames says this first. A terminal has no property that
	/// stops the cursor: a focused one blinks twice a second with no completion signal to wait
	/// on, and an unfocused one draws a steady outline over whichever cell the buffer is at. The
	/// escape sequence takes both away.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes once the cursor is gone.</returns>
	[Given("the cursor of {string} is hidden")]
	[When("the cursor of {string} is hidden")]
	public async Task When_the_cursor_of_is_hidden(string name) =>
		await FeedAsync(name, TerminalScripts.HideCursor).ConfigureAwait(false);

	/// <summary>
	/// Resets a terminal, which is what a host does between sessions: the screen, the scrollback,
	/// the attributes and the cursor all go back to how they started.
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes once the reset has been drawn.</returns>
	[When("the terminal {string} is reset")]
	public async Task When_the_terminal_is_reset(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => TerminalOf(name).Reset()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// -------------------------------------------------------------- the keys

	/// <summary>
	/// Gives a terminal the keyboard through its own method, which is what an application calls
	/// when it wants the person to start typing into a session it just opened.
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes once the control has the focus.</returns>
	[When("{string} is given focus")]
	public async Task When_is_given_focus(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => TerminalOf(name).GrabFocus()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ----------------------------------------------------------- the finger

	/// <summary>
	/// Drags one finger along a row of cells, from the middle of one cell to the middle of
	/// another, in one step per cell - the gesture a person makes to select a run of text. The
	/// selection runs from where the finger went down UP TO the cell it is resting in, which is
	/// why a scenario names the cell after the last one it means to select.
	/// <para>
	/// Only the control knows where a cell is, which is why the gesture is placed here rather
	/// than by the core's own finger steps; it lifts the finger itself, so nothing is left
	/// resting on the panel.
	/// </para>
	/// </summary>
	/// <param name="from">The column the finger goes down in.</param>
	/// <param name="to">The column it lifts from.</param>
	/// <param name="row">The row it runs along, counting from the top of the grid.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes once the whole gesture has been delivered.</returns>
	[When("a finger drags from cell {int} to cell {int} along row {int} of {string}")]
	public async Task When_a_finger_drags_from_cell_to_cell_along_row_of(int from, int to, int row, string name)
	{
		if (to <= from)
		{
			throw new ArgumentOutOfRangeException(nameof(to), to,
				"A drag runs from one cell to a later one, so it ends further right than it started.");
		}

		var cell = await MetricsAsync(name).ConfigureAwait(false);
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		var session = TestTargetFixture.Session;
		var y = bounds.Y + (int) Math.Round((row + 0.5) * cell.Height);

		int At(int column) => bounds.X + (int) Math.Round((column + 0.5) * cell.Width);

		session.TouchPress(Finger.PointerId, At(from), y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		for (var column = from + 1; column <= to; column++)
		{
			session.TouchMove(Finger.PointerId, At(column), y);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		}

		session.TouchRelease(Finger.PointerId, At(to), y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// --------------------------------------------------------------- the grid

	/// <summary>Asserts how many columns and rows the terminal's grid came out as.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="columns">The expected column count.</param>
	/// <param name="rows">The expected row count.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the grid of {string} is {int} columns by {int} rows")]
	public async Task Then_the_grid_of_is_columns_by_rows(string name, int columns, int rows)
	{
		var grid = await GridAsync(name).ConfigureAwait(false);

		grid.Columns.Should().Be(columns, "the column count of \"{0}\" was asserted", name);
		grid.Rows.Should().Be(rows, "the row count of \"{0}\" was asserted", name);
	}

	/// <summary>
	/// Asserts that the grid is as many whole cells as fit in the space the control was given -
	/// which is the requirement a host relies on when it tells the far end how big the window is.
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the grid of {string} fits the space it was given")]
	public async Task Then_the_grid_of_fits_the_space_it_was_given(string name)
	{
		var cell = await MetricsAsync(name).ConfigureAwait(false);
		var canvas = await CanvasSizeAsync(name).ConfigureAwait(false);
		var grid = await GridAsync(name).ConfigureAwait(false);
		var columns = Math.Max(MinimumColumns, (int) (canvas.Width / cell.Width));
		var rows = Math.Max(MinimumRows, (int) (canvas.Height / cell.Height));
		var measured = string.Create(CultureInfo.InvariantCulture,
			$"the drawing surface of \"{name}\" is {canvas.Width} x {canvas.Height} and a cell is "
			+ $"{cell.Width} x {cell.Height}");

		grid.Columns.Should().Be(columns, "{0}", measured);
		grid.Rows.Should().Be(rows, "{0}", measured);
	}

	/// <summary>
	/// Asserts that the last size the control announced is the size it is actually laid out in.
	/// A host forwards that announcement to the far end as the window size, so an announcement
	/// that disagreed with the grid would wrap every line the far end sent back.
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the last GridResized of {string} reported the grid it now has")]
	public async Task Then_the_last_GridResized_of_reported_the_grid_it_now_has(string name)
	{
		var grid = await GridAsync(name).ConfigureAwait(false);
		var announced = LastGrid(name) ?? throw new InvalidOperationException(
			$"\"{name}\" has never announced a grid size, so there is nothing to compare against. "
			+ "The control announces one whenever the grid re-fits, which includes being laid out.");

		announced.Should().Be(grid, "the last GridResized of \"{0}\" was asserted", name);
	}

	/// <summary>Remembers the grid a terminal has now, so a later step can say how it changed.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="gridName">The name to remember this grid under.</param>
	/// <returns>A task that completes once the grid has been read.</returns>
	[Given("the grid of {string} is remembered as {string}")]
	[When("the grid of {string} is remembered as {string}")]
	public async Task When_the_grid_of_is_remembered_as(string name, string gridName)
	{
		var grid = await GridAsync(name).ConfigureAwait(false);
		_scenarioContext[GridKey(gridName)] = grid;
	}

	/// <summary>Asserts that the grid now holds fewer columns AND fewer rows than it did.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="gridName">The remembered grid to compare against.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the grid of {string} is smaller than in {string}")]
	public async Task Then_the_grid_of_is_smaller_than_in(string name, string gridName)
	{
		var now = await GridAsync(name).ConfigureAwait(false);
		var before = Remembered(gridName);

		now.Columns.Should().BeLessThan(before.Columns,
			"the grid of \"{0}\" was {1} and is {2}", name, Describe(before), Describe(now));
		now.Rows.Should().BeLessThan(before.Rows,
			"the grid of \"{0}\" was {1} and is {2}", name, Describe(before), Describe(now));
	}

	/// <summary>Asserts that the scroll bar is showing, which it does once there is history.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the scroll bar of {string} is showing")]
	public async Task Then_the_scroll_bar_of_is_showing(string name) =>
		(await ScrollBarVisibilityAsync(name).ConfigureAwait(false)).Should().Be(Visibility.Visible,
			"the scroll bar of \"{0}\" was asserted", name);

	/// <summary>Asserts that the scroll bar is taking up no room at all.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the scroll bar of {string} is hidden")]
	public async Task Then_the_scroll_bar_of_is_hidden(string name) =>
		(await ScrollBarVisibilityAsync(name).ConfigureAwait(false)).Should().Be(Visibility.Collapsed,
			"the scroll bar of \"{0}\" was asserted", name);

	// ------------------------------------------------------------- the wire

	/// <summary>Asserts how often a terminal handed its host something to send.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the InputEmitted of {string} was raised {int} times")]
	public void Then_the_InputEmitted_of_was_raised_times(string name, int times) =>
		AssertInputCount(name, times);

	/// <summary>Asserts that a terminal handed its host exactly one thing to send.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	[Then("the InputEmitted of {string} was raised once")]
	public void Then_the_InputEmitted_of_was_raised_once(string name) => AssertInputCount(name, 1);

	/// <summary>
	/// Asserts the bytes a terminal has handed its host, all of them, in order. A feature file
	/// writes the word ESC where an escape character belongs.
	/// </summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="expected">The input the scenario expects, as a feature file writes it.</param>
	[Then("the input emitted by {string} is {string}")]
	public void Then_the_input_emitted_by_is(string name, string expected)
	{
		var wanted = TerminalScripts.ReadSequence(GherkinValue.Unquote(expected));
		var actual = EmittedInput(name);

		TerminalScripts.DescribeSequence(actual).Should().Be(TerminalScripts.DescribeSequence(wanted),
			"the input \"{0}\" emitted was asserted", name);
	}

	/// <summary>Asserts what a terminal last said its session is called.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="expected">The title the scenario expects.</param>
	[Then("the title reported by {string} is {string}")]
	public void Then_the_title_reported_by_is(string name, string expected)
	{
		var title = TrafficOf(name).Title;

		title.Should().Be(GherkinValue.Unquote(expected),
			"the title \"{0}\" reported was asserted", name);
	}

	/// <summary>Asserts what text a terminal copied, which is what its selection covered.</summary>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="expected">The text the scenario expects.</param>
	[Then("the text copied from {string} is {string}")]
	public void Then_the_text_copied_from_is(string name, string expected)
	{
		var copied = TrafficOf(name).Copied;

		copied.Should().Be(GherkinValue.Unquote(expected),
			"the text copied from \"{0}\" was asserted", name);
	}

	// ------------------------------------------------------------ the cells

	/// <summary>Asserts that one cell of the grid is painted one colour throughout.</summary>
	/// <param name="column">The cell's column, counting from the left of the grid.</param>
	/// <param name="row">The cell's row, counting from the top of the grid.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="color">The colour it must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("cell {int}, {int} of {string} is uniformly {string}")]
	public async Task Then_cell_of_is_uniformly(int column, int row, string name, Color color) =>
		(await CellRegionAsync(column, row, name).ConfigureAwait(false)).IsUniformly(color);

	/// <summary>Asserts that a colour covers at least a share of one cell of the grid.</summary>
	/// <param name="column">The cell's column.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="percent">The share, as a percentage of the cell.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("cell {int}, {int} of {string} contains at least {float} percent {string}")]
	public async Task Then_cell_of_contains_at_least_percent(int column, int row, string name,
		float percent, Color color) =>
		(await CellRegionAsync(column, row, name).ConfigureAwait(false)).Contains(color, percent / 100.0);

	/// <summary>Asserts that a colour is nowhere in one cell of the grid.</summary>
	/// <param name="column">The cell's column.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("cell {int}, {int} of {string} does not contain {string}")]
	public async Task Then_cell_of_does_not_contain(int column, int row, string name, Color color) =>
		(await CellRegionAsync(column, row, name).ConfigureAwait(false)).DoesNotContain(color);

	/// <summary>Asserts that a whole row of the grid is painted one colour throughout.</summary>
	/// <param name="row">The row, counting from the top of the grid.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="color">The colour it must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of {string} is uniformly {string}")]
	public async Task Then_row_of_is_uniformly(int row, string name, Color color) =>
		(await RowRegionAsync(row, name).ConfigureAwait(false)).IsUniformly(color);

	/// <summary>Asserts that a colour covers at least a share of one row of the grid.</summary>
	/// <param name="row">The row.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="percent">The share, as a percentage of the row.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of {string} contains at least {float} percent {string}")]
	public async Task Then_row_of_contains_at_least_percent(int row, string name, float percent, Color color) =>
		(await RowRegionAsync(row, name).ConfigureAwait(false)).Contains(color, percent / 100.0);

	/// <summary>Asserts that a colour is nowhere in one row of the grid.</summary>
	/// <param name="row">The row.</param>
	/// <param name="name">The Gherkin name of the terminal.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of {string} does not contain {string}")]
	public async Task Then_row_of_does_not_contain(int row, string name, Color color) =>
		(await RowRegionAsync(row, name).ConfigureAwait(false)).DoesNotContain(color);

	// --------------------------------------------------------------- inner

	private const string GridKeyPrefix = "uireqs.terminalview.grid.";

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(TerminalKind, BuildATerminal);

		ElementFactory.RegisterProperty<TerminalControlElement>("BackgroundColor",
			(terminal, value) => terminal.BackgroundColor = ToSkia(Colors.Parse(value)));
		ElementFactory.RegisterProperty<TerminalControlElement>("ForegroundColor",
			(terminal, value) => terminal.ForegroundColor = ToSkia(Colors.Parse(value)));
		ElementFactory.RegisterProperty<TerminalControlElement>("SelectionColor",
			(terminal, value) => terminal.SelectionColor = ToSkia(Colors.Parse(value)));
		ElementFactory.RegisterProperty<TerminalControlElement>("TerminalFontSize",
			(terminal, value) => terminal.TerminalFontSize = (float) GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty<TerminalControlElement>("ConvertEol",
			(terminal, value) => terminal.ConvertEol = bool.Parse(GherkinValue.Unquote(value)));

		Colors.RegisterName(PaletteRed, FromThePalette(PaletteRedIndex));
		Colors.RegisterName(PaletteBrightRed, FromThePalette(PaletteBrightRedIndex));
	}

	private static FrameworkElement BuildATerminal()
	{
		var terminal = new TerminalControlElement();

		// The four wires an application owns. The control's name is read at the moment an event
		// arrives rather than now, because the factory names an element after it has built it.
		terminal.InputEmitted += data => Recorded(terminal, nameof(TerminalControlElement.InputEmitted),
			traffic => traffic.Input.Append(data));

		terminal.GridResized += (columns, rows) => Recorded(terminal, nameof(TerminalControlElement.GridResized),
			traffic => traffic.LastGrid = (columns, rows));

		terminal.TitleChanged += title => Recorded(terminal, nameof(TerminalControlElement.TitleChanged),
			traffic => traffic.Title = title);

		terminal.CopyRequested += text => Recorded(terminal, nameof(TerminalControlElement.CopyRequested),
			traffic => traffic.Copied = text);

		return terminal;
	}

	/// <summary>
	/// Counts one raising of one of a terminal's four wires and keeps what it carried. An
	/// element the factory has not named yet cannot be spoken about by a scenario, so an event
	/// from one is not recorded rather than thrown over.
	/// </summary>
	private static void Recorded(TerminalControlElement terminal, string eventName,
		Action<TerminalTraffic> carried)
	{
		var name = terminal.Name;
		if (string.IsNullOrEmpty(name))
		{
			return;
		}

		EventRecorder.Record(name, eventName);
		lock (TrafficLock)
		{
			carried(TrafficFor(name));
		}
	}

	private static Color FromThePalette(int index)
	{
		var color = TerminalPalette.DefaultAnsiColors[index];
		return Color.FromArgb(0xFF, color.Red, color.Green, color.Blue);
	}

	private static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);

	private static TerminalControlElement TerminalOf(string name) =>
		ElementRegistry.Resolve(name) as TerminalControlElement
		?? throw new NotSupportedException($"\"{name}\" is not a Terminal, so it shows no grid.");

	private static TerminalTraffic TrafficFor(string name)
	{
		if (!Traffic.TryGetValue(name, out var traffic))
		{
			traffic = new TerminalTraffic();
			Traffic[name] = traffic;
		}

		return traffic;
	}

	private static TerminalTraffic TrafficOf(string name)
	{
		lock (TrafficLock)
		{
			return TrafficFor(name);
		}
	}

	private static string EmittedInput(string name)
	{
		lock (TrafficLock)
		{
			return TrafficFor(name).Input.ToString();
		}
	}

	private static (int Columns, int Rows)? LastGrid(string name)
	{
		lock (TrafficLock)
		{
			return TrafficFor(name).LastGrid;
		}
	}

	private static void AssertInputCount(string name, int times) =>
		EventRecorder.Count(name, nameof(TerminalControlElement.InputEmitted)).Should().Be(times,
			"the InputEmitted of \"{0}\" was asserted; it emitted \"{1}\"",
			name, TerminalScripts.DescribeSequence(EmittedInput(name)));

	private static string GridKey(string gridName) =>
		string.Create(CultureInfo.InvariantCulture, $"{GridKeyPrefix}{gridName}");

	private static string Describe((int Columns, int Rows) grid) => string.Create(CultureInfo.InvariantCulture,
		$"{grid.Columns} columns by {grid.Rows} rows");

	private static async Task FeedAsync(string name, string script)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var terminal = TerminalOf(name);
			if (!terminal.IsLoaded)
			{
				throw new InvalidOperationException(
					$"\"{name}\" is not on the panel yet, and a terminal drops everything fed to it "
					+ "before it is loaded. A scenario shows the control and captures one frame "
					+ "before it feeds anything.");
			}

			terminal.Feed(script);
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<CellMetrics> MetricsAsync(string name)
	{
		var cell = default(CellMetrics);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var terminal = TerminalOf(name);
			cell = CellMetrics.Measure(terminal.TerminalFontFamily, terminal.TerminalFontSize);
		}).ConfigureAwait(false);

		return cell;
	}

	private static async Task<(int Columns, int Rows)> GridAsync(string name)
	{
		var grid = (0, 0);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var terminal = TerminalOf(name);
			grid = (terminal.Columns, terminal.Rows);
		}).ConfigureAwait(false);

		return grid;
	}

	private static async Task<(double Width, double Height)> CanvasSizeAsync(string name)
	{
		var size = (0.0, 0.0);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var terminal = TerminalOf(name);

			// The control's template is a two-column grid: the drawing surface in the star column
			// and the scroll bar in an Auto one, so the surface is narrower than the control
			// exactly when there is history to scroll through.
			var surface = VisualTreeSearch.FindDescendant<Microsoft.UI.Xaml.Controls.Canvas>(terminal)
				?? throw new InvalidOperationException(
					$"\"{name}\" has no drawing surface in its visual tree, so nothing has been laid out "
					+ "to look at.");

			size = (surface.ActualWidth, surface.ActualHeight);
		}).ConfigureAwait(false);

		return size;
	}

	private static async Task<Visibility> ScrollBarVisibilityAsync(string name)
	{
		var visibility = Visibility.Collapsed;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var terminal = TerminalOf(name);
			var bar = VisualTreeSearch.FindDescendant<ScrollBar>(terminal)
				?? throw new InvalidOperationException(
					$"\"{name}\" has no scroll bar in its visual tree.");

			visibility = bar.Visibility;
		}).ConfigureAwait(false);

		return visibility;
	}

	private (int Columns, int Rows) Remembered(string gridName) =>
		_scenarioContext.TryGetValue(GridKey(gridName), out var stored) && stored is ValueTuple<int, int> grid
			? grid
			: throw new InvalidOperationException(
				$"No grid was remembered as \"{gridName}\". A scenario remembers one with "
				+ "\"the grid of ... is remembered as ...\" before the step that compares against it.");

	private async Task<Region> CellRegionAsync(int column, int row, string name)
	{
		var cell = await MetricsAsync(name).ConfigureAwait(false);
		var left = (int) Math.Round(column * cell.Width);
		var top = (int) Math.Round(row * cell.Height);
		var rectangle = new DeviceRect(left, top,
			(int) Math.Round((column + 1) * cell.Width) - left,
			(int) Math.Round((row + 1) * cell.Height) - top);
		var description = string.Create(CultureInfo.InvariantCulture, $"cell {column}, {row} of \"{name}\"");

		return await ScenarioFrames.SubRegionAsync(_scenarioContext, name, rectangle, description, CellInset)
			.ConfigureAwait(false);
	}

	private async Task<Region> RowRegionAsync(int row, string name)
	{
		var cell = await MetricsAsync(name).ConfigureAwait(false);
		var canvas = await CanvasSizeAsync(name).ConfigureAwait(false);
		var top = (int) Math.Round(row * cell.Height);
		var rectangle = new DeviceRect(0, top,
			(int) Math.Round(canvas.Width),
			(int) Math.Round((row + 1) * cell.Height) - top);
		var description = string.Create(CultureInfo.InvariantCulture, $"row {row} of \"{name}\"");

		return await ScenarioFrames.SubRegionAsync(_scenarioContext, name, rectangle, description, CellInset)
			.ConfigureAwait(false);
	}

	/// <summary>What one terminal's four wires have carried this scenario.</summary>
	private sealed class TerminalTraffic
	{
		public StringBuilder Input { get; } = new();

		public string? Title { get; set; }

		public string? Copied { get; set; }

		public (int Columns, int Rows)? LastGrid { get; set; }
	}
}
