using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.TextLayout.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.UI.TextLayout;
using Reqnroll;
using SilverAssertions;
using SkiaSharp;
using Windows.UI;

namespace CodeBrix.Platform.UI.AddIn.TextLayout.UIReqs.Steps;

/// <summary>
/// The TextLayout group's vocabulary: the canvas the engine paints onto, the layout a scenario
/// builds, the way it is painted, and the facts the engine reports about it - line counts, line
/// metrics, glyph outlines, selection rectangles and hit-testing - each said in the same
/// sentence as the pixels it has to agree with.
/// <para>
/// Everything else a scenario says - showing the canvas, capturing a frame, what a region looks
/// like, where its ink sits - is the core harness's own vocabulary, reached through this
/// project's reqnroll.json binding assemblies. Nothing from the core project is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class TextLayoutSteps
{
	/// <summary>The name a feature file builds the painting surface with.</summary>
	public const string SurfaceKind = "TextLayoutSurface";

	/// <summary>
	/// The prerequisite name both feature files declare with a <c>@needs-textlayout</c> tag: the
	/// add-in assembly and the Skia canvas it paints onto. Every machine that can build this
	/// project has both, so nothing is ever skipped for it here - but a run whose add-in could
	/// not be loaded reports "skipped: the TextLayout add-in is not usable" instead of failing
	/// once per scenario over an element kind the factory does not know.
	/// </summary>
	public const string TextLayoutPrerequisite = "textlayout";

	/// <summary>How far a hit-test probe is placed beyond the painted ink, in layout units.</summary>
	public const int BeyondTheInk = 20;

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private static bool _engineWarm;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public TextLayoutSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's one noun, before the first scenario.
	/// <para>
	/// The add-in ships no XAML type at all, so the noun is the harness's own painting surface
	/// rather than an element of the package: a Skia canvas whose paint handler puts the layout
	/// on the panel. That is the only way an engine with no element of its own can be seen, and
	/// it is what a consumer of the package writes too.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_TextLayout_vocabulary()
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
				// Touching both types is what makes the guard below real: the element factory takes
				// a lambda, whose body would not be run - and so would not fail - until the first
				// scenario asked for a canvas.
				_ = typeof(TextLayoutEngine).FullName;
				_ = typeof(TextLayoutSurface).BaseType?.FullName;
				ElementFactory.RegisterKind(SurfaceKind, () => new TextLayoutSurface());
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in and its canvas are what these scenarios are about, so a machine that
				// cannot load them has no requirement to state - it has a report to make.
				Prerequisite.Missing(TextLayoutPrerequisite,
					$"the TextLayout add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	// ------------------------------------------------------------ the engine

	/// <summary>
	/// Lays a throwaway string out through the engine, in every weight these features use,
	/// before any scenario measures with it.
	/// <para>
	/// The text engine answers the first measurement through a face it has not loaded yet with
	/// an interim one and finishes loading the real face on a continuation nothing here can see,
	/// and a layout is measured once and never re-measured - so a scenario that laid out in that
	/// moment would assert about numbers that were never true. This is the core harness's own
	/// font warm-up (which lays a string out through a text block) followed by one real call
	/// into the add-in's engine per weight, awaited, on the UI thread. It is not a sleep.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once the engine has laid text out in every weight.</returns>
	[Given("the text engine is warm")]
	public async Task Given_the_text_engine_is_warm()
	{
		await FontWarmup.WarmAsync(TextSamples.ApplicationFont).ConfigureAwait(false);

		lock (RegistrationLock)
		{
			if (_engineWarm)
			{
				return;
			}

			_engineWarm = true;
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			foreach (var (sample, weight) in WarmupSamples())
			{
				using var warm = TextLayoutEngine.Layout(
					[new TextRunDescriptor(sample, TextSamples.ApplicationFont, 32f, weight)]);
				if (warm.Size.Width > 0f && warm.Size.Height > 0f)
				{
					continue;
				}

				// A font that measures to nothing is the silent disaster the family rule about
				// system fonts exists to prevent: say so here, where the cause is still in view.
				var measured = string.Create(CultureInfo.InvariantCulture,
					$"{warm.Size.Width} x {warm.Size.Height}");
				throw new InvalidOperationException(
					$"The engine laid \"{sample}\" out in {weight} to {measured}, so nothing would be "
					+ $"painted with it. Is \"{TextSamples.ApplicationFont}\" beside the test executable, "
					+ "with its .ttf.manifest, at the path the URI resolves to?");
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Lays one run of text out with the settings a table lists, and shows the result on the
	/// canvas the scenario is looking at.
	/// </summary>
	/// <param name="text">
	/// The text to lay out. A feature file cannot write a real line break inside a quoted value,
	/// so the two characters <c>\n</c> stand for one.
	/// </param>
	/// <param name="settings">
	/// A Setting/Value table. Font, FontSize, Weight, MaxWidth, Alignment, BaseDirection,
	/// LineHeight and MaxLines are the settings; anything else is a failure that lists them.
	/// </param>
	/// <returns>A task that completes once the layout is built and the canvas has repainted.</returns>
	[Given("the text {string} is laid out with:")]
	[When("the text {string} is laid out with:")]
	public async Task Given_the_text_is_laid_out_with(string text, DataTable settings)
	{
		var wanted = ReadSettings(settings);
		var content = UnescapeLineBreaks(GherkinValue.Unquote(text));

		await LayOutAsync(
			() => [new TextRunDescriptor(content, wanted.Font, wanted.FontSize, wanted.Weight)],
			wanted.Options).ConfigureAwait(false);
	}

	/// <summary>
	/// Lays a sequence of runs out at one size, and shows the result on the canvas. Runs are
	/// concatenated in order, so the text a later step talks about is every run's text in a row.
	/// </summary>
	/// <param name="fontSize">The em size every run is laid out at.</param>
	/// <param name="runs">A Text/Weight/Colour table, one row per run. A blank colour leaves the run in the ink colour.</param>
	/// <returns>A task that completes once the layout is built and the canvas has repainted.</returns>
	[Given("the runs are laid out at size {int} with:")]
	[When("the runs are laid out at size {int} with:")]
	public async Task Given_the_runs_are_laid_out_at_size_with(int fontSize, DataTable runs)
	{
		ArgumentNullException.ThrowIfNull(runs);

		var hasWeight = runs.Header.Contains("Weight", StringComparer.Ordinal);
		var hasColour = runs.Header.Contains("Colour", StringComparer.Ordinal);
		var rows = runs.Rows
			.Select(row => (
				Text: row["Text"],
				Weight: ReadWeight(hasWeight ? row["Weight"] : string.Empty),
				Colour: hasColour && !string.IsNullOrWhiteSpace(row["Colour"])
					? Colors.Parse(row["Colour"])
					: (Color?) null))
			.ToArray();

		await LayOutAsync(
			() => rows
				.Select(row => new TextRunDescriptor(
					UnescapeLineBreaks(row.Text), TextSamples.ApplicationFont, fontSize, row.Weight)
				{
					// ANDROID PORT (e): the Android harness's ColorMatch.ToSkia returns its PixelColor stand-in (the canvas
					// vocabulary carries no SkiaSharp, PORTING.txt (2)); the engine's run colour is a real SKColor, built
					// from the same four channels.
					Color = row.Colour is { } colour ? new SKColor(colour.R, colour.G, colour.B, colour.A) : null,
				})
				.ToArray(),
			new TextLayoutOptions()).ConfigureAwait(false);
	}

	/// <summary>Puts the layout's top left corner somewhere other than the canvas's own corner.</summary>
	/// <param name="x">How far right of the canvas's left edge the layout starts.</param>
	/// <param name="y">How far below the canvas's top edge the layout starts.</param>
	/// <returns>A task that completes once the canvas has repainted.</returns>
	[Given("the layout origin is {int}, {int}")]
	[When("the layout origin is {int}, {int}")]
	public async Task Given_the_layout_origin_is(int x, int y) =>
		await OnTheSurfaceAsync(surface => surface.Origin = new SKPoint(x, y)).ConfigureAwait(false);

	/// <summary>Says which stretch of the text the selection rectangles cover.</summary>
	/// <param name="start">The first text index of the range.</param>
	/// <param name="end">The text index the range stops before.</param>
	/// <returns>A task that completes once the canvas has repainted.</returns>
	[Given("the selection covers text {int} to {int}")]
	[When("the selection covers text {int} to {int}")]
	public async Task Given_the_selection_covers_text_to(int start, int end) =>
		await OnTheSurfaceAsync(surface =>
		{
			surface.SelectionStart = start;
			surface.SelectionLength = end - start;
		}).ConfigureAwait(false);

	/// <summary>Repaints the canvas through one of the add-in's three painting paths.</summary>
	/// <param name="mode">Blank, Draw, Outline, FirstOutline, AllOutlines or Selection.</param>
	/// <returns>A task that completes once the canvas has repainted.</returns>
	[Given("the layout is painted as {word}")]
	[When("the layout is painted as {word}")]
	public async Task When_the_layout_is_painted_as(string mode)
	{
		var wanted = GherkinValue.ToEnum<TextLayoutPaintMode>(mode);
		await OnTheSurfaceAsync(surface => surface.Mode = wanted).ConfigureAwait(false);
	}

	/// <summary>Remembers the size the engine measured, so a later layout can be compared with it.</summary>
	/// <param name="name">The name the scenario gives that measurement.</param>
	/// <returns>A task that completes once the measurement has been read.</returns>
	[When("the layout size is remembered as {string}")]
	public async Task When_the_layout_size_is_remembered_as(string name)
	{
		var size = SKSize.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() => size = TheLayout().Size).ConfigureAwait(false);
		_scenarioContext[SizeKey + name] = size;
	}

	// ----------------------------------------------------- facts of the layout

	/// <summary>Asserts how many lines the engine broke the text into.</summary>
	/// <param name="lines">The number of lines.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the layout has {int} line(s)")]
	public async Task Then_the_layout_has_lines(int lines) =>
		(await LayoutFactAsync(layout => layout.LineCount).ConfigureAwait(false))
			.Should().Be(lines, "the engine's line count was asserted");

	/// <summary>Asserts that the engine broke the text into at least so many lines.</summary>
	/// <param name="lines">The fewest lines that count.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the layout has at least {int} lines")]
	public async Task Then_the_layout_has_at_least_lines(int lines) =>
		(await LayoutFactAsync(layout => layout.LineCount).ConfigureAwait(false))
			.Should().BeGreaterThanOrEqualTo(lines, "the engine's line count was asserted");

	/// <summary>Asserts that one line begins below the bottom of another.</summary>
	/// <param name="lower">The line that must be underneath.</param>
	/// <param name="upper">The line it must be underneath.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("line {int} of the layout sits below line {int}")]
	public async Task Then_line_of_the_layout_sits_below_line(int lower, int upper)
	{
		var (below, above) = await LayoutFactAsync(layout =>
			(layout.GetLineMetrics(lower), layout.GetLineMetrics(upper))).ConfigureAwait(false);

		((double) below.Top).Should().BeGreaterThanOrEqualTo(above.Top + above.Height,
			"line {0} must start at or below the bottom of line {1}: line {1} is {2} high at {3}, and line {0} starts at {4}",
			lower, upper, Number(above.Height), Number(above.Top), Number(below.Top));
	}

	/// <summary>Asserts that the engine resolved the layout's base direction as right to left.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the layout reads right to left")]
	public async Task Then_the_layout_reads_right_to_left() =>
		(await LayoutFactAsync(layout => layout.IsBaseDirectionRightToLeft).ConfigureAwait(false))
			.Should().BeTrue("the scenario asked the engine for a right-to-left base direction");

	/// <summary>Asserts that the engine measured this layout wider than one it measured before.</summary>
	/// <param name="name">The name the earlier measurement was remembered under.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the layout is wider than the layout remembered as {string}")]
	public async Task Then_the_layout_is_wider_than_the_layout_remembered_as(string name)
	{
		var earlier = Remembered(name);
		var size = await LayoutFactAsync(layout => layout.Size).ConfigureAwait(false);

		((double) size.Width).Should().BeGreaterThan(earlier.Width,
			"the engine must measure this layout wider than \"{0}\": it is {1} wide where \"{0}\" was {2} wide",
			name, Number(size.Width), Number(earlier.Width));
	}

	/// <summary>Asserts that the engine measured this layout a multiple of the height of one it measured before.</summary>
	/// <param name="factor">How many times as tall it must be.</param>
	/// <param name="name">The name the earlier measurement was remembered under.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the layout is at least {float} times as tall as the layout remembered as {string}")]
	public async Task Then_the_layout_is_at_least_times_as_tall_as(float factor, string name)
	{
		var earlier = Remembered(name);
		var size = await LayoutFactAsync(layout => layout.Size).ConfigureAwait(false);

		((double) size.Height).Should().BeGreaterThanOrEqualTo(earlier.Height * factor,
			"the engine must measure this layout at least {0} times as tall as \"{1}\": it is {2} tall where \"{1}\" was {3} tall",
			Number(factor), name, Number(size.Height), Number(earlier.Height));
	}

	/// <summary>Asserts that a glyph the engine positioned starts at the layout's own left edge.</summary>
	/// <param name="glyph">The glyph's place in visual order, counted from zero.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("glyph {int} of the layout begins at the left edge of the layout")]
	public async Task Then_glyph_of_the_layout_begins_at_the_left_edge(int glyph)
	{
		var origin = await GlyphFactAsync(glyph, outline => outline.Origin).ConfigureAwait(false);

		((double) origin.X).Should().BeLessThanOrEqualTo(0.5,
			"glyph {0} of left-to-right text must be placed at the start of the line, but its origin is {1}",
			glyph, Number(origin.X));
	}

	/// <summary>Asserts that a glyph has nothing to draw - a space, or anything else with no outline.</summary>
	/// <param name="glyph">The glyph's place in visual order, counted from zero.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("glyph {int} of the layout has no outline")]
	public async Task Then_glyph_of_the_layout_has_no_outline(int glyph)
	{
		var hasOutline = await GlyphFactAsync(glyph,
			outline => outline.Path is { IsEmpty: false }).ConfigureAwait(false);

		hasOutline.Should().BeFalse("glyph {0} of the layout must have nothing to draw", glyph);
	}

	/// <summary>Asserts that a glyph moves the pen on, whether or not it draws anything.</summary>
	/// <param name="glyph">The glyph's place in visual order, counted from zero.</param>
	/// <param name="pixels">The advance it must be greater than.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("glyph {int} of the layout advances more than {int} pixels")]
	public async Task Then_glyph_of_the_layout_advances_more_than_pixels(int glyph, int pixels)
	{
		var advance = await GlyphFactAsync(glyph, outline => outline.Advance).ConfigureAwait(false);

		((double) advance).Should().BeGreaterThan(pixels,
			"glyph {0} of the layout must advance the pen: it advances {1}", glyph, Number(advance));
	}

	// -------------------------------------------- the pixels against the engine

	/// <summary>Asserts that what was painted is as wide as the engine said the text is.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="tolerance">How many pixels the two may differ by.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink width of {string} agrees with the layout width within {int} pixel(s)")]
	public async Task Then_the_ink_width_of_agrees_with_the_layout_width(string name, int tolerance)
	{
		var region = await RegionAsync(name).ConfigureAwait(false);
		var ink = region.InkBounds();
		var width = await LayoutFactAsync(layout => layout.Size.Width).ConfigureAwait(false);

		((double) Math.Abs(ink.Width - width)).Should().BeLessThanOrEqualTo(tolerance,
			"the painted ink of \"{0}\" must be as wide as the engine measured the text: the engine says {1} and the ink is {2} wide ({3})",
			name, Number(width), ink.Width, ink);
	}

	/// <summary>Asserts that what was painted is a multiple of the engine's own line height tall.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="factor">How many line heights tall the ink must be at least.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} is at least {float} line heights tall")]
	public async Task Then_the_ink_of_is_at_least_line_heights_tall(string name, float factor)
	{
		var region = await RegionAsync(name).ConfigureAwait(false);
		var ink = region.InkBounds();
		var lineHeight = await LayoutFactAsync(layout => layout.LineHeight).ConfigureAwait(false);

		((double) ink.Height).Should().BeGreaterThanOrEqualTo(lineHeight * factor,
			"the painted ink of \"{0}\" must be at least {1} line heights tall: a line is {2} high and the ink is {3} tall ({4})",
			name, Number(factor), Number(lineHeight), ink.Height, ink);
	}

	/// <summary>Asserts that everything painted lies inside the box the engine measured.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} lies inside the layout box")]
	public async Task Then_the_ink_of_lies_inside_the_layout_box(string name)
	{
		var region = await RegionAsync(name).ConfigureAwait(false);
		var ink = region.InkBounds();
		var box = await LayoutBoxAsync(name).ConfigureAwait(false);

		Outside(ink, box).Should().Be(0,
			"every pixel the engine painted must lie inside the box it measured: the box of \"{0}\" is {1} and its ink is {2}",
			name, box, ink);
	}

	/// <summary>Asserts that everything painted lies inside the rectangle the engine gives one character.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="textIndex">The text index whose rectangle it must lie in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} lies inside the rectangle of text index {int}")]
	public async Task Then_the_ink_of_lies_inside_the_rectangle_of_text_index(string name, int textIndex)
	{
		var region = await RegionAsync(name).ConfigureAwait(false);
		var ink = region.InkBounds();
		var cluster = await DeviceRectangleAsync(name,
			layout => layout.GetRectForIndex(textIndex)).ConfigureAwait(false);

		Outside(ink, cluster).Should().Be(0,
			"the glyph painted on \"{0}\" must land inside the rectangle the engine gives text index {1}: that rectangle is {2} and the ink is {3}",
			name, textIndex, cluster, ink);
	}

	/// <summary>Asserts that something was painted where the engine puts a stretch of the text.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="start">The first text index of the stretch.</param>
	/// <param name="end">The text index the stretch stops before.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} covering text {int} to {int} has ink")]
	public async Task Then_the_region_covering_text_has_ink(string name, int start, int end) =>
		(await RegionCoveringAsync(name, start, end).ConfigureAwait(false)).HasInk();

	/// <summary>Asserts that nothing was painted where the engine puts a stretch of the text.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="start">The first text index of the stretch.</param>
	/// <param name="end">The text index the stretch stops before.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} covering text {int} to {int} is blank")]
	public async Task Then_the_region_covering_text_is_blank(string name, int start, int end) =>
		(await RegionCoveringAsync(name, start, end).ConfigureAwait(false)).IsBlank();

	/// <summary>Asserts what colour was painted where the engine puts a stretch of the text.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="start">The first text index of the stretch.</param>
	/// <param name="end">The text index the stretch stops before.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} covering text {int} to {int} contains {string}")]
	public async Task Then_the_region_covering_text_contains(string name, int start, int end, Color color) =>
		(await RegionCoveringAsync(name, start, end).ConfigureAwait(false)).Contains(color);

	/// <summary>Asserts what colour was NOT painted where the engine puts a stretch of the text.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="start">The first text index of the stretch.</param>
	/// <param name="end">The text index the stretch stops before.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} covering text {int} to {int} does not contain {string}")]
	public async Task Then_the_region_covering_text_does_not_contain(string name, int start, int end, Color color) =>
		(await RegionCoveringAsync(name, start, end).ConfigureAwait(false)).DoesNotContain(color);

	/// <summary>
	/// Asserts that the engine put one stretch of the text to the right of another. In
	/// left-to-right text the later stretch is the one on the right; a right-to-left base
	/// direction turns that around, which is the only thing about it a person can see.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="start">The first text index of the stretch that must be on the right.</param>
	/// <param name="end">The text index that stretch stops before.</param>
	/// <param name="otherStart">The first text index of the stretch that must be on the left.</param>
	/// <param name="otherEnd">The text index that stretch stops before.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the region of {string} covering text {int} to {int} sits right of the text {int} to {int}")]
	public async Task Then_the_region_covering_text_sits_right_of_the_text(string name, int start, int end,
		int otherStart, int otherEnd)
	{
		var right = await DeviceRectangleAsync(name,
			layout => Union(layout.GetSelectionRects(start, end - start))).ConfigureAwait(false);
		var left = await DeviceRectangleAsync(name,
			layout => Union(layout.GetSelectionRects(otherStart, otherEnd - otherStart))).ConfigureAwait(false);

		right.X.Should().BeGreaterThanOrEqualTo(left.Right,
			"the engine must place text {0} to {1} of \"{2}\" to the right of text {3} to {4}: it puts them at {5} and {6}",
			start, end, name, otherStart, otherEnd, right, left);
	}

	// ------------------------------------------------------------ hit-testing

	/// <summary>
	/// Asserts that the engine reads the first character back from the place it painted it. The
	/// probe is taken from the painted ink, not from a number a scenario guessed.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ink of {string} starts at the first character of the layout")]
	public async Task Then_the_ink_of_starts_at_the_first_character(string name)
	{
		var probe = await ProbeAsync(name, beyond: false).ConfigureAwait(false);
		var index = await LayoutFactAsync(layout => layout.GetIndexAt(probe)).ConfigureAwait(false);

		index.Should().Be(0,
			"the leftmost painted pixel of \"{0}\", at {1} in layout coordinates, must belong to the first character",
			name, Point(probe));
	}

	/// <summary>Asserts that a point past the painted ink is not on the text at all.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("a point past the ink of {string} is outside the text")]
	public async Task Then_a_point_past_the_ink_of_is_outside_the_text(string name)
	{
		var probe = await ProbeAsync(name, beyond: true).ConfigureAwait(false);
		var index = await LayoutFactAsync(layout => layout.GetIndexAt(probe)).ConfigureAwait(false);

		index.Should().Be(-1,
			"a point {0} pixels past the painted ink of \"{1}\", at {2} in layout coordinates, is on no character",
			BeyondTheInk, name, Point(probe));
	}

	/// <summary>Asserts that the nearest character to a point past the ink is the end of the text.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the nearest index to a point past the ink of {string} is the end of the text")]
	public async Task Then_the_nearest_index_to_a_point_past_the_ink_is_the_end(string name)
	{
		var probe = await ProbeAsync(name, beyond: true).ConfigureAwait(false);
		var (index, length) = await LayoutFactAsync(layout =>
			(layout.GetNearestIndexAt(probe), layout.Text.Length)).ConfigureAwait(false);

		index.Should().Be(length,
			"a drag past the end of \"{0}\", at {1} in layout coordinates, must land on the end of the text",
			name, Point(probe));
	}

	// ----------------------------------------------------------------- inner

	private const string SizeKey = "uireqs.textlayout.size.";

	/// <summary>
	/// What the warm-up lays out: the harness's own warm-up string in both weights these
	/// features use, and the right-to-left sample, whose script is itemised through a different
	/// part of the same font and is worth having measured before a scenario measures it.
	/// </summary>
	private static IEnumerable<(string Sample, TextFontWeight Weight)> WarmupSamples() =>
	[
		(FontWarmup.WarmupText, TextFontWeight.Normal),
		(FontWarmup.WarmupText, TextFontWeight.Bold),
		(TextSamples.RightToLeft, TextFontWeight.Normal),
	];

	private Task<Region> RegionAsync(string elementName, string frameName = ScenarioFrames.CurrentFrameName) =>
		ScenarioFrames.RegionAsync(_scenarioContext, elementName, frameName);

	private SKSize Remembered(string name) =>
		_scenarioContext.TryGetValue(SizeKey + name, out var stored) && stored is SKSize size
			? size
			: throw new InvalidOperationException(
				$"The scenario never remembered a layout as \"{name}\". A scenario remembers one with "
				+ "\"When the layout size is remembered as ...\".");

	private static TextLayoutSurface TheSurface()
	{
		TextLayoutSurface? found = null;
		foreach (var name in ElementRegistry.Names)
		{
			if (!ElementRegistry.TryResolve(name, out var element) || element is not TextLayoutSurface surface)
			{
				continue;
			}

			if (found is not null)
			{
				throw new InvalidOperationException(
					"The scenario is showing more than one " + SurfaceKind + ", so \"the layout\" names "
					+ "no one canvas. A TextLayout scenario shows exactly one.");
			}

			found = surface;
		}

		return found ?? throw new InvalidOperationException(
			"The scenario is showing no " + SurfaceKind + ", so there is no canvas to lay text out on. "
			+ "A scenario shows one with \"Given the application shows a " + SurfaceKind + " named ...\".");
	}

	private static TextLayoutResult TheLayout() =>
		TheSurface().Layout ?? throw new InvalidOperationException(
			"The scenario has not laid any text out yet, so there is nothing to say about the layout. "
			+ "A scenario lays text out with \"the text ... is laid out with:\".");

	private static async Task OnTheSurfaceAsync(Action<TextLayoutSurface> change)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var surface = TheSurface();
			change(surface);
			surface.Invalidate();
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task LayOutAsync(Func<IReadOnlyList<TextRunDescriptor>> runs, TextLayoutOptions options) =>
		await OnTheSurfaceAsync(surface => surface.Layout = TextLayoutEngine.Layout(runs(), options))
			.ConfigureAwait(false);

	private static async Task<T> LayoutFactAsync<T>(Func<TextLayoutResult, T> read)
	{
		var value = default(T)!;
		await TestTargetFixture.RunOnUIThreadAsync(() => value = read(TheLayout())).ConfigureAwait(false);
		return value;
	}

	private static async Task<T> GlyphFactAsync<T>(int glyph, Func<GlyphOutline, T> read)
	{
		var value = default(T)!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var outlines = TheLayout().GetGlyphOutlines();
			try
			{
				if (glyph < 0 || glyph >= outlines.Count)
				{
					throw new ArgumentOutOfRangeException(nameof(glyph), glyph, string.Create(
						CultureInfo.InvariantCulture,
						$"The layout has {outlines.Count} glyphs, numbered from 0."));
				}

				value = read(outlines[glyph]);
			}
			finally
			{
				foreach (var outline in outlines)
				{
					outline.Dispose();
				}
			}
		}).ConfigureAwait(false);

		return value;
	}

	private async Task<Region> RegionCoveringAsync(string name, int start, int end)
	{
		var rectangle = await DeviceRectangleAsync(name, layout => Union(
			layout.GetSelectionRects(start, end - start))).ConfigureAwait(false);
		var frame = ScenarioFrames.Get(_scenarioContext, ScenarioFrames.CurrentFrameName);

		return new Region(frame, rectangle.Inset(DeviceRect.DefaultInset), string.Create(
			CultureInfo.InvariantCulture,
			$"the region of \"{name}\" covering text {start} to {end}"));
	}

	private static async Task<DeviceRect> LayoutBoxAsync(string name) =>
		await DeviceRectangleAsync(name, layout =>
			new SKRect(0f, 0f, layout.Size.Width, layout.Size.Height)).ConfigureAwait(false);

	/// <summary>
	/// Turns a rectangle in the layout's own coordinates into one in the frame's, by way of the
	/// canvas's place in the visual tree and the origin the layout was painted at.
	/// </summary>
	private static async Task<DeviceRect> DeviceRectangleAsync(string name, Func<TextLayoutResult, SKRect> read)
	{
		var canvas = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		var rectangle = SKRect.Empty;
		var origin = SKPoint.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			origin = TheSurface().Origin;
			rectangle = read(TheLayout());
		}).ConfigureAwait(false);

		var left = (int) Math.Floor(canvas.X + origin.X + rectangle.Left);
		var top = (int) Math.Floor(canvas.Y + origin.Y + rectangle.Top);
		var right = (int) Math.Ceiling(canvas.X + origin.X + rectangle.Right);
		var bottom = (int) Math.Ceiling(canvas.Y + origin.Y + rectangle.Bottom);

		// Nothing outside the canvas is the engine's doing, so the rectangle stops at its edge.
		return new DeviceRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)).Intersect(canvas);
	}

	private async Task<SKPoint> ProbeAsync(string name, bool beyond)
	{
		var canvas = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		var ink = (await RegionAsync(name).ConfigureAwait(false)).InkBounds();
		var origin = SKPoint.Empty;
		var middle = 0f;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var surface = TheSurface();
			origin = surface.Origin;
			var line = TheLayout().GetLineMetrics(0);
			middle = line.Top + (line.Height / 2f);
		}).ConfigureAwait(false);

		var x = beyond
			? ink.Right + BeyondTheInk
			: ink.X + 1;

		return new SKPoint(x - canvas.X - origin.X, middle);
	}

	private static SKRect Union(IReadOnlyList<SKRect> rectangles)
	{
		if (rectangles.Count == 0)
		{
			throw new InvalidOperationException(
				"The engine gives that stretch of the text no rectangle at all, so there is no region "
				+ "of the canvas it covers. Is the stretch empty, or outside the text?");
		}

		var union = rectangles[0];
		for (var index = 1; index < rectangles.Count; index++)
		{
			union = SKRect.Union(union, rectangles[index]);
		}

		return union;
	}

	private static int Outside(DeviceRect inner, DeviceRect outer)
	{
		var overhang = Math.Max(
			Math.Max(outer.X - inner.X, outer.Y - inner.Y),
			Math.Max(inner.Right - outer.Right, inner.Bottom - outer.Bottom));
		return Math.Max(0, overhang - DeviceRect.DefaultInset);
	}

	private static string UnescapeLineBreaks(string text) =>
		text.Replace(TextSamples.LineBreakEscape, "\n", StringComparison.Ordinal);

	private static string Number(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.##}");

	private static string Point(SKPoint point) => string.Create(CultureInfo.InvariantCulture,
		$"({point.X:0.##},{point.Y:0.##})");

	private static TextFontWeight ReadWeight(string value) => string.IsNullOrWhiteSpace(value)
		? TextFontWeight.Normal
		: GherkinValue.ToEnum<TextFontWeight>(value);

	private static (TextLayoutOptions Options, string Font, float FontSize, TextFontWeight Weight) ReadSettings(
		DataTable table)
	{
		ArgumentNullException.ThrowIfNull(table);

		var options = new TextLayoutOptions();
		var font = TextSamples.ApplicationFont;
		var fontSize = 32f;
		var weight = TextFontWeight.Normal;
		var setting = table.Header.First();
		var value = table.Header.Last();

		foreach (var row in table.Rows)
		{
			var text = GherkinValue.Unquote(row[value]);
			switch (row[setting].ToUpperInvariant())
			{
				case "FONT":
					font = text;
					break;
				case "FONTSIZE":
					fontSize = (float) GherkinValue.ToDouble(text);
					break;
				case "WEIGHT":
					weight = GherkinValue.ToEnum<TextFontWeight>(text);
					break;
				case "MAXWIDTH":
					options.MaxWidth = (float) GherkinValue.ToDouble(text);
					break;
				case "MAXLINES":
					options.MaxLines = (int) GherkinValue.ToDouble(text);
					break;
				case "ALIGNMENT":
					options.Alignment = GherkinValue.ToEnum<TextAlign>(text);
					break;
				case "BASEDIRECTION":
					options.BaseDirection = GherkinValue.ToEnum<TextDirection>(text);
					break;
				case "LINEHEIGHT":
					options.LineHeight = (float) GherkinValue.ToDouble(text);
					break;
				default:
					throw new NotSupportedException(
						$"A layout has no \"{row[setting]}\". Write Font, FontSize, Weight, MaxWidth, "
						+ "MaxLines, Alignment, BaseDirection or LineHeight.");
			}
		}

		return (options, font, fontSize, weight);
	}
}
