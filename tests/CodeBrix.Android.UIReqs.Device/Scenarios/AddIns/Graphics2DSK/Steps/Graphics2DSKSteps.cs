using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.Graphics2DSK.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.WinUI.Graphics2DSK;
using Reqnroll;
using SilverAssertions;

namespace CodeBrix.Platform.UI.AddIn.Graphics2DSK.UIReqs.Steps;

/// <summary>
/// The Graphics2DSK group's vocabulary: the two fixture elements a feature file may show, the
/// colours they paint, and the three things only this add-in can be asked - to draw again, how
/// often it has drawn, and what area it was handed when it did.
/// <para>
/// Everything else a scenario here says - showing an element, putting one inside another, where
/// something sits, how big it is, what colour a region is, what changed between two frames - is
/// the core harness's own vocabulary, reached through this project's reqnroll.json binding
/// assemblies. Nothing from the core project is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class Graphics2DSKSteps
{
	/// <summary>The name a feature file shows a plain drawing element with.</summary>
	public const string FillCanvasKind = "FillCanvas";

	/// <summary>The name a feature file shows the element that draws outside its area with.</summary>
	public const string ClipProbeCanvasKind = "ClipProbeCanvas";

	/// <summary>
	/// The prerequisite name both feature files declare with a <c>@needs-graphics2dsk</c> tag:
	/// the drawing element itself. It needs no system library and no device - only the
	/// compositor factory the framework's own Application registers - so nothing is ever skipped
	/// for it on a machine that can run the harness at all. A machine where the element reports
	/// that it is not supported gets twelve skips that say so, instead of twelve failures about
	/// an element kind the factory refused to build.
	/// </summary>
	public const string Graphics2DSKPrerequisite = "graphics2dsk";

	private const string NotedCountKey = "uireqs.graphics2dsk.renderCount.";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public Graphics2DSKSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// An add-in registers from its OWN hook rather than through a partial method of the core
	/// factory: a partial method admits exactly one implementation, and every add-in adds to the
	/// same two tables. "Fill" goes in as a TYPED setter, so that the word goes on meaning a
	/// Shape's Fill, a Border's Background and a Panel's Background everywhere else and means
	/// the drawing this element paints only on one of these fixture elements - which are none of
	/// those things, so the core harness's general setter could not apply anything to them.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_Graphics2DSK_vocabulary()
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
				if (!SKCanvasElement.IsSupportedOnCurrentPlatform())
				{
					// The element's own answer, which is exactly whether the framework registered
					// the compositor factory it draws through. Nothing a scenario could say would
					// be true on a machine that answers no.
					Prerequisite.Missing(Graphics2DSKPrerequisite,
						"the drawing element reports that it is not supported on this platform, so "
						+ "nothing it would draw can be shown");
					return;
				}

				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(Graphics2DSKPrerequisite,
					$"the Graphics2DSK add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(FillCanvasKind, () => new FillCanvas());
		ElementFactory.RegisterKind(ClipProbeCanvasKind, () => new ClipProbeCanvas());

		ElementFactory.RegisterProperty<SKCanvasFixture>("Fill", (canvas, value) => canvas.SetFill(value));
	}

	/// <summary>
	/// Asks an element to draw itself again, having first noted how often it has drawn so far.
	/// This is the ONLY way a scenario here provokes a repaint: a fixture element never
	/// invalidates itself, because an element that does is dirty forever and no frame the
	/// harness asks for would ever arrive.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the UI thread has marked the element for redrawing.</returns>
	[When("{string} is invalidated")]
	public async Task When_is_invalidated(string name)
	{
		var noted = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = CanvasOf(name);
			noted = canvas.RenderCount;
			canvas.Invalidate();
		}).ConfigureAwait(false);

		_scenarioContext[NotedCountKey + name] = noted;
	}

	/// <summary>
	/// Asserts that an element has drawn itself at least so many times. Counts are always "at
	/// least": any recompose of the tree repaints the element, so an exact number would be a
	/// statement about the compositor's scheduling rather than about the requirement.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="times">The fewest draws the requirement allows.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the render count of {string} is at least {int}")]
	public async Task Then_the_render_count_of_is_at_least(string name, int times)
	{
		var actual = await RenderCountAsync(name).ConfigureAwait(false);

		actual.Should().BeGreaterThanOrEqualTo(times,
			"\"{0}\" must have drawn itself at least {1} time(s)", name, times);
	}

	/// <summary>
	/// Asserts that an element has drawn itself again since it was invalidated. The comparison
	/// is against the count the invalidating step noted, so it survives any repaint the
	/// compositor did for reasons of its own before then.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the render count of {string} has grown")]
	public async Task Then_the_render_count_of_has_grown(string name)
	{
		if (!_scenarioContext.TryGetValue(NotedCountKey + name, out var stored) || stored is not int noted)
		{
			throw new InvalidOperationException(
				$"The scenario never invalidated \"{name}\", so there is no earlier render count to "
				+ "compare with. Write \"When \"" + name + "\" is invalidated\" before asking whether "
				+ "its render count has grown.");
		}

		var actual = await RenderCountAsync(name).ConfigureAwait(false);

		actual.Should().BeGreaterThan(noted,
			"\"{0}\" must have drawn itself again after it was invalidated; it had drawn {1} time(s) then",
			name, noted);
	}

	/// <summary>
	/// Asserts the area the framework handed the element the last time it drew. This is the fact
	/// that says the element draws in the size the layout arranged it at, in the same device
	/// pixels every other measurement in the harness is made in.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="width">The expected width, in device pixels.</param>
	/// <param name="height">The expected height, in device pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the drawing area of {string} was {int} by {int}")]
	public async Task Then_the_drawing_area_of_was_by(string name, int width, int height)
	{
		var area = Windows.Foundation.Size.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() => area = CanvasOf(name).DrawnArea).ConfigureAwait(false);

		Describe(area.Width, area.Height).Should().Be(Describe(width, height),
			"the area \"{0}\" was given to draw in was asserted", name);
	}

	private static string Describe(double width, double height) => string.Create(CultureInfo.InvariantCulture,
		$"{Math.Round(width, MidpointRounding.AwayFromZero)} x {Math.Round(height, MidpointRounding.AwayFromZero)}");

	private static async Task<int> RenderCountAsync(string name)
	{
		var count = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() => count = CanvasOf(name).RenderCount).ConfigureAwait(false);
		return count;
	}

	private static SKCanvasFixture CanvasOf(string name) =>
		ElementRegistry.Resolve(name) as SKCanvasFixture
		?? throw new NotSupportedException(
			$"\"{name}\" is not a {FillCanvasKind} or a {ClipProbeCanvasKind}, so it draws nothing of "
			+ "its own and has no render count.");
}
