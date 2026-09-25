using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Reqnroll;
using SilverAssertions;
using Panel = Microsoft.UI.Xaml.Controls.Panel;

namespace CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Steps;

/// <summary>
/// The Graphics3DGL group's vocabulary: the fixture elements a feature file may show, the
/// colours they clear and draw with, the completion signal a scenario waits on before it looks
/// at a frame, and the facts only this add-in can be asked - whether OpenGL came up, what its
/// status is and why it failed when it did.
/// <para>
/// Everything else a scenario here says - showing an element, putting one inside another, where
/// something sits, how big it is, what colour a region is, what changed between two frames - is
/// the core harness's own vocabulary, reached through this project's reqnroll.json binding
/// assemblies. Nothing from the core project is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class Graphics3DGLSteps
{
	/// <summary>The name a feature file shows the ordinary drawing element with.</summary>
	public const string TriangleCanvasKind = "TriangleCanvas";

	/// <summary>The name a feature file shows the element whose shaders cannot compile with.</summary>
	public const string BrokenShaderCanvasKind = "BrokenShaderCanvas";

	/// <summary>The name a feature file shows the GPU-Skia element with.</summary>
	public const string SkiaGlCanvasKind = "SkiaGlCanvas";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-graphics3dgl</c> tag:
	/// the add-in assembly itself. Every machine that can build this project has it, so nothing
	/// is ever skipped for it here.
	/// </summary>
	public const string AddInPrerequisite = "graphics3dgl";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-egl</c> tag: the
	/// off-screen OpenGL the panel's own host provides. It is the one thing these requirements
	/// need from the MACHINE rather than from the framework, and it is the one that another
	/// machine may not have - a build box with no <c>libEGL</c> and no software rasteriser
	/// reports every scenario as skipped with the reason, instead of failing them all with
	/// "the region is blank".
	/// </summary>
	public const string EglPrerequisite = "egl";

	/// <summary>
	/// How long a GL canvas is given to reach a render count. The first one in a process pays
	/// for opening the device, choosing a config and creating the context, and a machine with no
	/// GPU does all of that in a software rasteriser - so the budget is generous, and no
	/// scenario here makes any claim at all about how long a frame takes.
	/// </summary>
	public static readonly TimeSpan RenderBudget = TimeSpan.FromSeconds(20);

	private const string NotedCountKey = "uireqs.graphics3dgl.renderCount.";

	private const string ParentKey = "uireqs.graphics3dgl.parent.";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public Graphics3DGLSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario, and looks for
	/// the off-screen OpenGL these requirements need from the machine.
	/// <para>
	/// An add-in registers from its OWN hook rather than through a partial method of the core
	/// factory: a partial method admits exactly one implementation, and every add-in adds to the
	/// same two tables. Every property name here is control-specific ("ClearColor",
	/// "TriangleColor", "DrawTriangle"), so this group takes no universal word away from anything.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_Graphics3DGL_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			LookForOffScreenOpenGL();

			try
			{
				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(AddInPrerequisite,
					$"the Graphics3DGL add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	// --------------------------------------------------- drawing, and the signal

	/// <summary>
	/// Waits until a GL canvas has been asked to draw at least so many times, and then captures
	/// ONE more frame - so that the frame the scenario goes on to look at is a frame drawn after
	/// the drawing it is about.
	/// <para>
	/// This is the completion signal these requirements are built on, and it exists because the
	/// picture a person sees is filled in OUTSIDE the recording. The element's visual asks for a
	/// render as it is PAINTED, and the render writes into the bitmap the element's background
	/// shows; the paint that is going on at that moment therefore draws the bitmap as it was
	/// BEFORE, and the new picture reaches the panel one frame later. The harness's own two-pass
	/// handshake usually hides that - but not always, and a canvas put inside a container that is
	/// already laid out is a case where it does not: the first frame captured after it appears
	/// shows the canvas empty. So the wait is "draw, then take one more frame", never just
	/// "draw"; a scenario that says "has rendered at least 1 frames" before it captures is
	/// looking at the picture and not at the blank that preceded it.
	/// </para>
	/// <para>
	/// It is a signal with a budget, never a wait for a length of time: a canvas that never draws
	/// fails with the count it reached and what the element says about its own OpenGL.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="frames">The fewest draws the scenario needs to have happened.</param>
	/// <returns>A task that completes once the element has drawn that often and been shown.</returns>
	[Given("the GL canvas {string} has rendered at least {int} frames")]
	[When("the GL canvas {string} has rendered at least {int} frames")]
	[Then("the GL canvas {string} has rendered at least {int} frames")]
	public async Task The_GL_canvas_has_rendered_at_least_frames(string name, int frames)
	{
		var reached = await Poll.UntilAsync(
			async () =>
			{
				await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName)
					.ConfigureAwait(false);
				return await RenderCountAsync(name).ConfigureAwait(false) >= frames;
			},
			RenderBudget).ConfigureAwait(false);

		var actual = await RenderCountAsync(name).ConfigureAwait(false);
		var state = await StateOfAsync(name).ConfigureAwait(false);

		reached.Should().BeTrue(
			"\"{0}\" must have been asked to draw at least {1} time(s) within {2}; it has drawn {3} time(s) "
			+ "and reports {4}{5}",
			name, frames, RenderBudget, actual, state.Status, ReasonSuffix(state));

		// The drain: one frame past the drawing, so what was drawn is what is on the panel.
		await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Asks a GL canvas to draw again, notes how often it had drawn before, and waits until it
	/// has drawn once more and that drawing has reached the panel. This is the ONLY way a
	/// scenario here provokes a redraw: a fixture element never invalidates itself, because an
	/// element that does is dirty forever and no frame the harness asks for would ever arrive.
	/// <para>
	/// The wait is the same "draw, then take one more frame" the render-count step makes, and for
	/// the same reason: the paint that asks for the render draws the picture that came before it.
	/// Waiting here is what lets a feature file say "it is invalidated" and then look at a frame,
	/// which is how a person would describe it.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the element has drawn again and the panel shows it.</returns>
	[Given("{string} is invalidated")]
	[When("{string} is invalidated")]
	public async Task When_is_invalidated(string name)
	{
		var noted = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = FixtureOf(name);
			noted = canvas.RenderCount;
			canvas.Invalidate();
		}).ConfigureAwait(false);

		_scenarioContext[NotedCountKey + name] = noted;

		var drewAgain = await Poll.UntilAsync(
			async () =>
			{
				await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName)
					.ConfigureAwait(false);
				return await RenderCountAsync(name).ConfigureAwait(false) > noted;
			},
			RenderBudget).ConfigureAwait(false);

		var state = await StateOfAsync(name).ConfigureAwait(false);
		drewAgain.Should().BeTrue(
			"\"{0}\" must draw again within {1} of being invalidated; it had drawn {2} time(s) and "
			+ "reports {3}{4}",
			name, RenderBudget, noted, state.Status, ReasonSuffix(state));

		await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Remembers how often a GL canvas has drawn, so that a later step can say whether it drew
	/// again. A scenario about a canvas that must NOT redraw notes the count with this and asks
	/// afterwards; a scenario about one that must redraw gets the same note for free from the
	/// step that invalidates it.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the count has been read.</returns>
	[Given("the render count of {string} is noted")]
	[When("the render count of {string} is noted")]
	public async Task The_render_count_of_is_noted(string name) =>
		_scenarioContext[NotedCountKey + name] = await RenderCountAsync(name).ConfigureAwait(false);

	/// <summary>
	/// Asserts that a GL canvas has drawn exactly so many more times than when the count was
	/// noted. One invalidation means one draw: the element's own contract is that
	/// <c>Invalidate</c> produces one frame and that nothing else produces any.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="growth">How many more draws the requirement allows and demands.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the render count of {string} has grown by exactly {int}")]
	public async Task Then_the_render_count_of_has_grown_by_exactly(string name, int growth)
	{
		var noted = NotedCount(name);
		var actual = await RenderCountAsync(name).ConfigureAwait(false);

		(actual - noted).Should().Be(growth,
			"\"{0}\" must have drawn exactly {1} more time(s) since the count was noted at {2}",
			name, growth, noted);
	}

	/// <summary>
	/// Asserts that a GL canvas has not drawn again since the count was noted. A canvas nobody
	/// invalidated keeps the picture it last produced, and it produces nothing new to keep it.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the render count of {string} has not grown")]
	public async Task Then_the_render_count_of_has_not_grown(string name)
	{
		var noted = NotedCount(name);
		var actual = await RenderCountAsync(name).ConfigureAwait(false);

		actual.Should().Be(noted,
			"\"{0}\" must not have drawn again since the count was noted at {1}", name, noted);
	}

	/// <summary>
	/// Asserts how often a GL canvas has set its OpenGL resources up and given them back. This
	/// is what says a canvas that left the tree really did release what it held, and that one
	/// that came back really was set up again.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="initCount">How many times it must have been set up.</param>
	/// <param name="destroyCount">How many times it must have given its resources back.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GL canvas {string} has been set up {int} time(s) and torn down {int} time(s)")]
	public static async Task Then_the_GL_canvas_has_been_set_up_and_torn_down(string name, int initCount,
		int destroyCount)
	{
		var initialised = 0;
		var destroyed = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = FixtureOf(name);
			initialised = canvas.InitCount;
			destroyed = canvas.DestroyCount;
		}).ConfigureAwait(false);

		Describe(initialised, destroyed).Should().Be(Describe(initCount, destroyCount),
			"\"{0}\" must have set its OpenGL resources up and given them back that often", name);
	}

	/// <summary>
	/// Takes a GL canvas out of the visual tree without forgetting its name, which is what makes
	/// the unloaded state something a scenario can ask about at all: the harness's own way of
	/// emptying the panel forgets every element along with it, and an element nobody can name is
	/// an element nobody can question.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the element is out of the tree and the panel has settled.</returns>
	[Given("the GL canvas {string} is taken out of the tree")]
	[When("the GL canvas {string} is taken out of the tree")]
	public async Task When_the_GL_canvas_is_taken_out_of_the_tree(string name)
	{
		Panel parent = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			parent = element.Parent as Panel ?? VirtualApplication.Instance.Root;
			parent.Children.Remove(element);
			VirtualApplication.Instance.Root.UpdateLayout();
		}).ConfigureAwait(false);

		_scenarioContext[ParentKey + name] = parent;
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Puts a GL canvas back where it was taken from. What the element does the second time it
	/// is loaded is a requirement of its own: it has to set OpenGL up again, having given the
	/// first set of resources back.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the element is in the tree again and the panel has settled.</returns>
	[When("the GL canvas {string} is put back into the tree")]
	public async Task When_the_GL_canvas_is_put_back_into_the_tree(string name)
	{
		if (!_scenarioContext.TryGetValue(ParentKey + name, out var stored) || stored is not Panel parent)
		{
			throw new InvalidOperationException(
				$"\"{name}\" was never taken out of the tree, so there is nowhere to put it back.");
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			parent.Children.Add(ElementRegistry.Resolve(name));
			VirtualApplication.Instance.Root.UpdateLayout();
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ------------------------------------------------------- what OpenGL reports

	/// <summary>
	/// Asserts what the element says about its own OpenGL: <c>true</c> once the context is up
	/// and the subclass has been set up, <c>false</c> when either failed, and <c>null</c> while
	/// the element is not in the tree, because the state is only meaningful while it is loaded.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="expected">"true", "false" or "null".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("IsGLInitialized of {string} is {string}")]
	public static async Task Then_IsGLInitialized_of_is(string name, string expected)
	{
		bool? actual = null;
		await TestTargetFixture.RunOnUIThreadAsync(() => actual = CanvasOf(name).IsGLInitialized)
			.ConfigureAwait(false);

		Describe(actual).Should().Be(Expected(expected),
			"\"{0}\" must report that OpenGL initialisation is {1}", name, Expected(expected));
	}

	/// <summary>
	/// Asserts the OpenGL initialisation status the element reports, by the name the add-in's
	/// own enumeration spells it with.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="expected">NotYetInitialized, Initializing, Initialized or InitializationFailed.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GL initialisation status of {string} is {string}")]
	public static async Task Then_the_GL_initialisation_status_of_is(string name, string expected)
	{
		var wanted = GherkinValue.ToEnum<GLInitializationStatus>(expected);
		var state = await StateOfAsync(name).ConfigureAwait(false);

		state.Status.Should().Be(wanted,
			"\"{0}\" must report the OpenGL status {1}{2}", name, wanted, ReasonSuffix(state));
	}

	/// <summary>
	/// Asserts that the element has no failure to report. A canvas that came up has nothing to
	/// explain, and the add-in's own contract is that a reason exists if and only if the status
	/// is a failure.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GL initialisation of {string} reports no failed reason")]
	public static async Task Then_the_GL_initialisation_of_reports_no_failed_reason(string name)
	{
		var state = await StateOfAsync(name).ConfigureAwait(false);

		(state.FailedReason ?? string.Empty).Should().BeEmpty(
			"\"{0}\" must have nothing to explain about its OpenGL initialisation", name);
	}

	/// <summary>
	/// Asserts that the element explains, in words, why it cannot render. This is the whole
	/// point of the failure path: it is silent on the panel, so a blank canvas is only
	/// acceptable when the element can also say what went wrong.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GL initialisation of {string} reports a failed reason")]
	public static async Task Then_the_GL_initialisation_of_reports_a_failed_reason(string name)
	{
		var state = await StateOfAsync(name).ConfigureAwait(false);

		(state.FailedReason ?? string.Empty).Should().NotBeEmpty(
			"\"{0}\" must say why it cannot render, because nothing on the panel says it for it", name);
	}

	/// <summary>
	/// Asserts what the GPU-Skia element says about its own OpenGL: <c>true</c> once the
	/// off-screen context and its Skia context are up, <c>false</c> when either failed, and
	/// <c>null</c> while the element is not in the tree.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="expected">"true", "false" or "null".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("IsGpuInitialized of {string} is {string}")]
	public static async Task Then_IsGpuInitialized_of_is(string name, string expected)
	{
		bool? actual = null;
		await TestTargetFixture.RunOnUIThreadAsync(() => actual = SkiaCanvasOf(name).IsGpuInitialized)
			.ConfigureAwait(false);

		Describe(actual).Should().Be(Expected(expected),
			"\"{0}\" must report that its GPU drawing is {1}", name, Expected(expected));
	}

	/// <summary>
	/// Asserts the size of the surface the GPU-Skia element was handed the last time it painted.
	/// This is the fact that says the element draws at the size the layout arranged it at, in
	/// the same device pixels every other measurement in the harness is made in.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="width">The expected width, in device pixels.</param>
	/// <param name="height">The expected height, in device pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GPU surface of {string} was {int} by {int}")]
	public static async Task Then_the_GPU_surface_of_was_by(string name, int width, int height)
	{
		var actualWidth = 0;
		var actualHeight = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var canvas = SkiaCanvasOf(name);
			actualWidth = canvas.SurfaceWidth;
			actualHeight = canvas.SurfaceHeight;
		}).ConfigureAwait(false);

		Size(actualWidth, actualHeight).Should().Be(Size(width, height),
			"the surface \"{0}\" was given to paint on was asserted", name);
	}

	/// <summary>
	/// Asserts that the GPU-Skia element has painted at least so many times. This is the fact
	/// that says there is a picture to look at, exactly as the render count is for a
	/// GLCanvasElement.
	/// </summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="times">The fewest paints the requirement allows.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the GPU canvas {string} has painted at least {int} times")]
	public static async Task Then_the_GPU_canvas_has_painted_at_least_times(string name, int times)
	{
		var actual = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() => actual = SkiaCanvasOf(name).PaintCount)
			.ConfigureAwait(false);

		actual.Should().BeGreaterThanOrEqualTo(times,
			"\"{0}\" must have painted at least {1} time(s)", name, times);
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(TriangleCanvasKind, () => new TriangleCanvas());
		ElementFactory.RegisterKind(BrokenShaderCanvasKind, () => new BrokenShaderCanvas());
		ElementFactory.RegisterKind(SkiaGlCanvasKind, () => new SkiaGlCanvas());

		ElementFactory.RegisterProperty<GLCanvasFixture>("ClearColor",
			(canvas, value) => canvas.SetClearColor(value));
		ElementFactory.RegisterProperty<GLCanvasFixture>("TriangleColor",
			(canvas, value) => canvas.SetTriangleColor(value));
		ElementFactory.RegisterProperty<GLCanvasFixture>("DrawTriangle",
			(canvas, value) => canvas.SetDrawTriangle(value));

		// The GPU-Skia element is not a GLCanvasElement, so "ClearColor" on one of these is a
		// different registration of the same word for an unrelated type - which is exactly what
		// a typed setter is for.
		ElementFactory.RegisterProperty<SkiaGlCanvas>("ClearColor",
			(canvas, value) => canvas.SetClearColor(value));
		ElementFactory.RegisterProperty<SkiaGlCanvas>("MarkColor",
			(canvas, value) => canvas.SetMarkColor(value));
		ElementFactory.RegisterProperty<SkiaGlCanvas>("CircleColor",
			(canvas, value) => canvas.SetCircleColor(value));
	}

	/// <summary>
	/// Looks for the one system library every route to an off-screen OpenGL context goes
	/// through, and records it as missing when it is. The panel's host opens a DRM render node
	/// first and falls back to the EGL surfaceless platform, and BOTH of those are EGL - so a
	/// machine without <c>libEGL</c> has no route at all, and saying so is better than leaving a
	/// scenario to discover it as a blank rectangle. A machine that has the library but no
	/// device still reports the real reason, because every scenario that claims ink also asks
	/// what the element says about its own initialisation.
	/// </summary>
	// ANDROID PORT (d): the EGL of an Android device is the system library libEGL.so (part of every Android image),
	// not the Debian libEGL.so.1; the Android Graphics3DGL add-in creates its OpenGL ES 3 context through it.
	private static void LookForOffScreenOpenGL()
	{
		if (NativeLibrary.TryLoad("libEGL.so", out _))
		{
			return;
		}

		Prerequisite.Missing(EglPrerequisite,
			"libEGL.so could not be loaded, so the Android Graphics3DGL add-in cannot create an off-screen "
			+ "OpenGL ES context");
	}

	private static async Task<GLInitializationState> StateOfAsync(string name)
	{
		GLInitializationState state = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() => state = CanvasOf(name).GetGLInitializationState())
			.ConfigureAwait(false);
		return state;
	}

	private static string ReasonSuffix(GLInitializationState state) =>
		string.IsNullOrEmpty(state.FailedReason) ? string.Empty : $" (it reported: {state.FailedReason})";

	private static string Describe(bool? value) =>
		value is null ? "null" : value.Value ? "true" : "false";

	private static string Expected(string text)
	{
		var value = GherkinValue.Unquote(text).ToUpperInvariant();
		return value switch
		{
			"TRUE" => "true",
			"FALSE" => "false",
			"NULL" => "null",
			_ => throw new FormatException(
				$"\"{text}\" is not something IsGLInitialized can be. Write true, false or null."),
		};
	}

	private static async Task<int> RenderCountAsync(string name)
	{
		var count = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() => count = FixtureOf(name).RenderCount).ConfigureAwait(false);
		return count;
	}

	private int NotedCount(string name)
	{
		if (_scenarioContext.TryGetValue(NotedCountKey + name, out var stored) && stored is int noted)
		{
			return noted;
		}

		throw new InvalidOperationException(
			$"The scenario never noted how often \"{name}\" had drawn, so there is nothing to compare "
			+ $"with. Write \"the render count of \"{name}\" is noted\" - or invalidate it, which notes "
			+ "the count on the way past - before asking whether it has drawn again.");
	}

	private static string Describe(int initCount, int destroyCount) => string.Create(CultureInfo.InvariantCulture,
		$"set up {initCount}, torn down {destroyCount}");

	private static string Size(int width, int height) => string.Create(CultureInfo.InvariantCulture,
		$"{width} x {height}");

	private static SkiaGlCanvas SkiaCanvasOf(string name) =>
		ElementRegistry.Resolve(name) as SkiaGlCanvas
		?? throw new NotSupportedException(
			$"\"{name}\" is not a {SkiaGlCanvasKind}, so it paints no GPU surface of its own.");

	private static GLCanvasFixture FixtureOf(string name) =>
		ElementRegistry.Resolve(name) as GLCanvasFixture
		?? throw new NotSupportedException(
			$"\"{name}\" is not a {TriangleCanvasKind}, so it counts nothing about its own drawing.");

	private static GLCanvasElement CanvasOf(string name) =>
		ElementRegistry.Resolve(name) as GLCanvasElement
		?? throw new NotSupportedException(
			$"\"{name}\" is not a {TriangleCanvasKind}, so it has no OpenGL state of its own.");
}
