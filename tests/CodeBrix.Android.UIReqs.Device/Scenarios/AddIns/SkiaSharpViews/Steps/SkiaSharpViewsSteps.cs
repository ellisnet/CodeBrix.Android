using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.SkiaSharpViews.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;
using SilverAssertions;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI;

namespace CodeBrix.Platform.UI.AddIn.SkiaSharpViews.UIReqs.Steps;

/// <summary>
/// The SkiaSharpViews group's vocabulary: the two elements the add-in ships, what a scenario
/// tells their paint handler to draw, and the few facts about a paint that no pixel can state -
/// how big the surface was, how often the handler ran, and where a pointer press landed in the
/// element's own coordinates.
/// <para>
/// Everything else these scenarios say - showing an element, changing a property, capturing a
/// frame, tapping, and every claim about what a region looks like - is the core harness's own
/// vocabulary, reached through this project's reqnroll.json binding assemblies. Nothing from
/// the core project is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class SkiaSharpViewsSteps
{
	/// <summary>The name a feature file builds a painting canvas with.</summary>
	public const string SkiaCanvasKind = "SkiaCanvas";

	/// <summary>The name a feature file builds the placeholder swap-chain element with.</summary>
	public const string SkiaSwapChainPanelKind = "SkiaSwapChainPanel";

	/// <summary>The property a feature file sets to say what the paint handler draws.</summary>
	public const string PaintColorProperty = "PaintColor";

	/// <summary>
	/// The prerequisite name every feature file of this group declares with a
	/// <c>@needs-skiasharpviews</c> tag: the add-in assembly and the Skia library under it.
	/// Every machine that can build this project has both, so nothing is skipped here - but a
	/// run whose add-in could not be loaded reports the scenarios as skipped with a reason,
	/// instead of failing over an element kind the factory does not know.
	/// </summary>
	public const string SkiaSharpViewsPrerequisite = "skiasharpviews";

	private const string ConstructionFailureKey = "uireqs.skiasharpviews.constructionFailure";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public SkiaSharpViewsSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's two nouns and the one property that says what
	/// the paint handler draws, before the first scenario.
	/// <para>
	/// "PaintColor" goes in as a TYPED setter: it means something only to this add-in's canvas,
	/// and a typed registration keeps it from being offered on every element in the assembly.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_SkiaSharpViews_vocabulary()
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
				Prerequisite.Missing(SkiaSharpViewsPrerequisite,
					$"the SkiaSharpViews add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	/// <summary>
	/// Puts the swap-chain element's refusal back the way the process found it. The opt-out is
	/// a static of the whole process and a run is one process for the whole assembly, so a
	/// scenario that turns the refusal off would otherwise turn it off for every scenario after
	/// it.
	/// </summary>
	[AfterScenario(Order = 0)]
	public static void Restore_the_swap_chain_panel_refusal() => SKSwapChainPanel.RaiseOnUnsupported = true;

	// ------------------------------------------------------- what the handler draws

	/// <summary>Repaints an element, which is the only thing that repaints one.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the paint has run and the UI thread is idle.</returns>
	[When("{string} is invalidated")]
	public async Task When_is_invalidated(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			switch (ElementRegistry.Resolve(name))
			{
				case PaintedCanvas canvas:
					canvas.Invalidate();
					break;
				case ProbeSwapChainPanel panel:
					panel.Invalidate();
					break;
				default:
					throw new NotSupportedException(
						$"\"{name}\" is neither a {SkiaCanvasKind} nor a {SkiaSwapChainPanelKind}, "
						+ "so there is nothing to invalidate.");
			}
		}).ConfigureAwait(false);

		// The canvas paints inside Invalidate(); the swap-chain element posts its no-op to the
		// dispatcher. Draining covers both, so the next captured frame shows whatever happened.
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Stops the handler clearing the surface before it draws, so that the scenario can show
	/// what the reused pixel buffer still holds.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[Given("the handler of {string} no longer clears the surface")]
	[When("the handler of {string} no longer clears the surface")]
	public Task Given_the_handler_of_no_longer_clears_the_surface(string name) =>
		TestTargetFixture.RunOnUIThreadAsync(() => CanvasOf(name).ClearsTheSurfaceFirst = false);

	/// <summary>Tells the handler to draw one square and nothing else.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="width">The square's width in surface pixels.</param>
	/// <param name="height">The square's height in surface pixels.</param>
	/// <param name="color">The colour to paint it.</param>
	/// <param name="x">Where its left edge is, in surface pixels.</param>
	/// <param name="y">Where its top edge is, in surface pixels.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[When("the handler of {string} draws only a {int} by {int} square of {string} at {int}, {int}")]
	public Task When_the_handler_of_draws_only_a_square(string name, int width, int height, Color color,
		int x, int y) =>
		TestTargetFixture.RunOnUIThreadAsync(() =>
			CanvasOf(name).PaintOnlyASquare(width, height, x, y, color));

	/// <summary>
	/// Tells the handler to draw a square, centred where the pointer was last pressed, over
	/// whatever else it already draws. The point comes from the element itself, so the scenario
	/// is showing the coordinates the element reported rather than coordinates it chose.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="width">The square's width in surface pixels.</param>
	/// <param name="height">The square's height in surface pixels.</param>
	/// <param name="color">The colour to paint it.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[When("the handler of {string} also draws a {int} by {int} square of {string} where the pointer was pressed")]
	public Task When_the_handler_of_also_draws_a_square_where_the_pointer_was_pressed(string name,
		int width, int height, Color color) =>
		TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = CanvasOf(name);
			var pressed = canvas.LastPointerPosition
				?? throw new InvalidOperationException(
					$"Nothing has pressed \"{name}\", so there is no point to draw at.");

			canvas.AlsoPaintASquare(width, height,
				(int) Math.Round(pressed.X) - (width / 2),
				(int) Math.Round(pressed.Y) - (height / 2),
				color);
		});

	/// <summary>Tells the handler to paint the whole surface as a gradient.</summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="axis">"horizontal" or "vertical".</param>
	/// <param name="start">The colour at the left, or top, edge.</param>
	/// <param name="end">The colour at the right, or bottom, edge.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	[Given("the handler of {string} paints a {word} gradient from {string} to {string}")]
	[When("the handler of {string} paints a {word} gradient from {string} to {string}")]
	public Task When_the_handler_of_paints_a_gradient(string name, string axis, Color start, Color end)
	{
		var vertical = GherkinValue.ToEnum<GradientAxis>(axis) == GradientAxis.Vertical;
		return TestTargetFixture.RunOnUIThreadAsync(() => CanvasOf(name).PaintGradient(start, end, vertical));
	}

	// ------------------------------------------------------------ facts about a paint

	/// <summary>
	/// Asserts how big the surface was that the handler last drew on. The handler is handed a
	/// surface, not the element, so this is the one thing about a paint that the panel cannot
	/// show on its own.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="width">The surface's expected width in pixels.</param>
	/// <param name="height">The surface's expected height in pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the surface the handler of {string} painted was {int} by {int} pixels")]
	public async Task Then_the_surface_the_handler_of_painted_was_by_pixels(string name, int width, int height)
	{
		var painted = 0;
		var actualWidth = 0;
		var actualHeight = 0;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = CanvasOf(name);
			painted = canvas.PaintCount;
			actualWidth = canvas.PaintedSurfaceSize.Width;
			actualHeight = canvas.PaintedSurfaceSize.Height;
		}).ConfigureAwait(false);

		painted.Should().BeGreaterThan(0, "the handler of \"{0}\" must have painted at least once", name);
		Size(actualWidth, actualHeight).Should().Be(Size(width, height),
			"the surface the handler of \"{0}\" painted was asserted", name);
	}

	/// <summary>Asserts the size an element reports for the surface it last painted.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="width">The expected width in pixels.</param>
	/// <param name="height">The expected height in pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the canvas size of {string} is {int} by {int} pixels")]
	public async Task Then_the_canvas_size_of_is_by_pixels(string name, int width, int height)
	{
		var size = await CanvasSizeAsync(name).ConfigureAwait(false);

		Size((int) Math.Round(size.Width), (int) Math.Round(size.Height)).Should().Be(Size(width, height),
			"the CanvasSize of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that an element reports no painted surface at all.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the canvas size of {string} is empty")]
	public async Task Then_the_canvas_size_of_is_empty(string name)
	{
		var size = await CanvasSizeAsync(name).ConfigureAwait(false);

		Size((int) Math.Round(size.Width), (int) Math.Round(size.Height)).Should().Be(Size(0, 0),
			"the CanvasSize of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that an element has no graphics context to draw with.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the graphics context of {string} is none")]
	public async Task Then_the_graphics_context_of_is_none(string name)
	{
		var described = string.Empty;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var context = SwapChainPanelOf(name).GRContext;
			described = context is null ? "none" : context.ToString() ?? "one";
		}).ConfigureAwait(false);

		described.Should().Be("none", "\"{0}\" is not backed by a graphics device on this head", name);
	}

	/// <summary>
	/// Asserts where a pointer press landed, in the element's own coordinates - the coordinate
	/// system the paint handler draws in.
	/// </summary>
	/// <param name="name">The Gherkin name of the canvas.</param>
	/// <param name="x">How far right of the element's left edge the press must have been.</param>
	/// <param name="y">How far below the element's top edge the press must have been.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the pointer of {string} was last pressed at {int}, {int}")]
	public async Task Then_the_pointer_of_was_last_pressed_at(string name, int x, int y)
	{
		var pressed = string.Empty;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var position = CanvasOf(name).LastPointerPosition;
			pressed = position is null
				? "nothing"
				: Point((int) Math.Round(position.Value.X), (int) Math.Round(position.Value.Y));
		}).ConfigureAwait(false);

		pressed.Should().Be(Point(x, y), "where the pointer pressed \"{0}\" was asserted", name);
	}

	// ------------------------------------------------- the swap-chain placeholder

	/// <summary>
	/// Leaves the swap-chain element's refusal as the framework ships it, which is what an
	/// application that has done nothing about it gets.
	/// </summary>
	[Given("the application has not opted out of the SKSwapChainPanel refusal")]
	public void Given_the_application_has_not_opted_out() => SKSwapChainPanel.RaiseOnUnsupported = true;

	/// <summary>Opts the whole application out of the swap-chain element's refusal.</summary>
	[Given("the application has opted out of the SKSwapChainPanel refusal")]
	public void Given_the_application_has_opted_out() => SKSwapChainPanel.RaiseOnUnsupported = false;

	/// <summary>
	/// Tries to build an SKSwapChainPanel and remembers how that went, so that the next step
	/// can state what the framework did about it.
	/// </summary>
	/// <returns>A task that completes once the attempt has been made.</returns>
	[When("an SKSwapChainPanel is constructed")]
	public async Task When_an_SKSwapChainPanel_is_constructed()
	{
		Exception? failure = null;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			try
			{
				_ = new ProbeSwapChainPanel();
			}
			catch (Exception thrown) when (thrown is NotSupportedException or NotImplementedException)
			{
				failure = thrown;
			}
		}).ConfigureAwait(false);

		// ANDROID PORT (a): the device's ScenarioContext shim is a Dictionary<string, object> with NRT annotations
		// (Reqnroll's is oblivious), so storing a possibly-null value needs the null-forgiving operator.
		_scenarioContext[ConstructionFailureKey] = failure!;
	}

	/// <summary>Asserts that the attempt to build the element was refused, and how.</summary>
	/// <param name="reason">The words the refusal must carry.</param>
	[Then("the construction was refused with {string}")]
	public void Then_the_construction_was_refused_with(string reason)
	{
		var stored = _scenarioContext.TryGetValue(ConstructionFailureKey, out var value) ? value : null;
		var failure = stored as Exception;

		(failure is NotSupportedException).Should().BeTrue(
			"the construction must be refused with a NotSupportedException, but it {0}",
			failure is null ? "was allowed" : "threw a " + failure.GetType().Name);
		failure!.Message.Should().Contain(reason, "the refusal must say why it refused");
	}

	// --------------------------------------------------------------------- inner

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(SkiaCanvasKind, () => new PaintedCanvas());
		ElementFactory.RegisterKind(SkiaSwapChainPanelKind, () => new ProbeSwapChainPanel());

		ElementFactory.RegisterProperty<PaintedCanvas>(PaintColorProperty,
			(canvas, value) => canvas.PaintBands(ParseColors(value)));
	}

	private static IReadOnlyList<Color> ParseColors(string value)
	{
		var parts = value.Split(',');
		var colors = new List<Color>(parts.Length);

		foreach (var part in parts)
		{
			colors.Add(Colors.Parse(part.Trim()));
		}

		return colors;
	}

	private static async Task<SKSize> CanvasSizeAsync(string name)
	{
		var size = SKSize.Empty;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			size = ElementRegistry.Resolve(name) switch
			{
				PaintedCanvas canvas => canvas.CanvasSize,
				ProbeSwapChainPanel panel => panel.CanvasSize,
				_ => throw new NotSupportedException(
					$"\"{name}\" is neither a {SkiaCanvasKind} nor a {SkiaSwapChainPanelKind}, "
					+ "so it reports no canvas size."),
			};
		}).ConfigureAwait(false);

		return size;
	}

	private static PaintedCanvas CanvasOf(string name) =>
		ElementRegistry.Resolve(name) as PaintedCanvas
		?? throw new NotSupportedException(
			$"\"{name}\" is not a {SkiaCanvasKind}, so it has no paint handler to tell what to draw.");

	private static ProbeSwapChainPanel SwapChainPanelOf(string name) =>
		ElementRegistry.Resolve(name) as ProbeSwapChainPanel
		?? throw new NotSupportedException($"\"{name}\" is not a {SkiaSwapChainPanelKind}.");

	private static string Size(int width, int height) => string.Create(CultureInfo.InvariantCulture,
		$"{width} by {height}");

	private static string Point(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"{x}, {y}");
}
