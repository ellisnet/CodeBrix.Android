using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.VideoPlayback.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
using VideoFixtures = CodeBrix.Platform.UI.AddIn.VideoPlayer.UIReqs.Support.VideoFixtures;
using VideoPlayerElement = CodeBrix.Platform.UI.VideoPlayer.Skia.VideoPlayer;

namespace CodeBrix.Platform.UI.AddIn.VideoPlayer.UIReqs.Steps;

/// <summary>
/// The VideoPlayer group's vocabulary: the player itself, the clip it opens, the transport a
/// person drives it with, and the two facts that are neither a pixel nor a plain property - that
/// a picture has reached the screen at all, and that the picture is now a particular colour.
/// <para>
/// Everything else a VideoPlayer scenario says - showing a sized cell, putting the player in it,
/// what its region looks like, how big it is, which events it raised - is the core harness's own
/// vocabulary, reached through this project's reqnroll.json binding assemblies. Nothing from the
/// core project is duplicated here.
/// </para>
/// <para>
/// Two things about a video player shape every step below. A decoded picture arrives on the
/// wall clock, and there is no "one frame has been presented" event to await, so every claim
/// about the picture is a BOUNDED POLL over fresh frames rather than a single capture at a
/// computed moment - and never a sleep. And the render path is chosen once, before a source is
/// opened, so the element factory builds every player on the processor path: that is the answer
/// that does not depend on which machine the scenarios run on, and a scenario that wants
/// otherwise says so in its arrange block, before its clip is opened.
/// </para>
/// </summary>
[Binding]
public sealed class VideoPlayerSteps
{
	/// <summary>The name a feature file builds the player with.</summary>
	public const string VideoPlayerKind = "VideoPlayer";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-videoplayer</c> tag:
	/// the add-in assembly and its playback engine. A machine that cannot load them reports
	/// skipped scenarios with a reason instead of failing over an element kind the factory
	/// does not know.
	/// </summary>
	public const string VideoPlayerPrerequisite = "videoplayer";

	/// <summary>
	/// The prerequisite name the one scenario that plays a clip WITH a soundtrack declares with a
	/// <c>@needs-audio-device</c> tag. Every fixture is digital silence and every player is muted
	/// at volume zero, so nothing is ever heard; but a clip that carries an audio track opens the
	/// output device, and a machine with no sound server has nothing to open.
	/// </summary>
	public const string AudioDevicePrerequisite = "audio-device";

	/// <summary>How long the panel is left alone between two looks at a picture that is still arriving.</summary>
	public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	private const string RefusalKey = "uireqs.video.refusal";
	private const string PositionMarkKey = "uireqs.video.position.";

	private static readonly object RegistrationLock = new();

	private static readonly List<VideoPlayerElement> OpenedPlayers = [];

	private static readonly Dictionary<string, string> LastFailures = new(StringComparer.Ordinal);

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	private readonly IReqnrollOutputHelper _outputHelper;

	/// <summary>Reqnroll builds one of these per scenario and injects the contexts it asks for.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	/// <param name="outputHelper">Where a step's own measurements go.</param>
	public VideoPlayerSteps(ScenarioContext scenarioContext, IReqnrollOutputHelper outputHelper)
	{
		_scenarioContext = scenarioContext;
		_outputHelper = outputHelper;
	}

	// ------------------------------------------------------------ registration

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// Every property here is registered as a TYPED setter: Source, Stretch and Volume are
	/// universal words that mean something on other elements too, and a typed setter teaches the
	/// word to this control without taking it away from anything else.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_VideoPlayer_vocabulary()
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
				// The add-in and its playback engine are what these scenarios are about, so a
				// machine that cannot load them has no requirement to state - it has a report
				// to make.
				Prerequisite.Missing(VideoPlayerPrerequisite,
					$"the VideoPlayer add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}

			if (!SoundDeviceLooksPresent())
			{
				Prerequisite.Missing(AudioDevicePrerequisite,
					"this machine has no sound card and no sound server socket, so a clip that carries an "
					+ "audio track has no output device to open");
			}
		}
	}

	/// <summary>
	/// Releases every player a scenario opened: the decode threads, the composition surface and
	/// the soundtrack's device with them.
	/// <para>
	/// The core's own reset empties the root, which only takes the element off the tree - and
	/// leaving the tree merely PAUSES a player. A player left behind would keep its threads and
	/// its device for the rest of the run, so this hook runs before that reset and closes them.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once every player of the scenario is closed.</returns>
	[AfterScenario(Order = 0)]
	public static async Task Close_the_players_the_scenario_opened()
	{
		VideoPlayerElement[] players;
		lock (RegistrationLock)
		{
			players = [.. OpenedPlayers];
			OpenedPlayers.Clear();
			LastFailures.Clear();
		}

		if (players.Length == 0 || !TestTargetFixture.IsLaunched)
		{
			return;
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			foreach (var player in players)
			{
				player.Close();
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ----------------------------------------------------------- the clip

	/// <summary>
	/// Opens one of the scenarios' clips in a player. The duration is valid the moment the
	/// source setter returns, so this step says so straight away: a clip that did not open has
	/// no requirement left to state, and the player's own failure message is what explains it.
	/// </summary>
	/// <param name="clip">The file name of the clip, as <see cref="VideoFixtures"/> knows it.</param>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the container has been read.</returns>
	[Given("the clip {string} is opened in {string}")]
	[When("the clip {string} is opened in {string}")]
	public async Task Given_the_clip_is_opened_in(string clip, string name)
	{
		var player = PlayerOf(name);
		var path = VideoFixtures.Require(GherkinValue.Unquote(clip));

		await TestTargetFixture.RunOnUIThreadAsync(() => player.Source = path).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		var duration = await ReadAsync(player, one => one.DurationSeconds).ConfigureAwait(false);

		duration.Should().BeGreaterThan(0.0,
			"the clip \"{0}\" must open in \"{1}\" and state how long it is; the player reported {2}",
			clip, name, FailureOf(name));
	}

	// -------------------------------------------------------- the transport

	/// <summary>Starts or resumes playback. A Given as well as a When: a scenario about pausing
	/// has to be playing before it can pause.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once playback has been asked for and the UI thread is idle.</returns>
	[Given("the video {string} is played")]
	[When("the video {string} is played")]
	public async Task When_the_video_is_played(string name)
	{
		var player = PlayerOf(name);
		await TestTargetFixture.RunOnUIThreadAsync(player.Play).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Pauses playback, which leaves the picture on screen.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the pause has been applied and the UI thread is idle.</returns>
	[When("the video {string} is paused")]
	public async Task When_the_video_is_paused(string name)
	{
		var player = PlayerOf(name);
		await TestTargetFixture.RunOnUIThreadAsync(player.Pause).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Stops playback, which rewinds to the beginning.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the stop has been applied and the UI thread is idle.</returns>
	[When("the video {string} is stopped")]
	public async Task When_the_video_is_stopped(string name)
	{
		var player = PlayerOf(name);
		await TestTargetFixture.RunOnUIThreadAsync(player.Stop).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Jumps to a timecode. This is the immediate seek a transport button makes, not the
	/// debounced one a dragged scrubber makes by writing the position.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The timecode to jump to.</param>
	/// <returns>A task that completes once the seek has been asked for and the UI thread is idle.</returns>
	[Given("the video {string} is sought to {float} seconds")]
	[When("the video {string} is sought to {float} seconds")]
	public async Task When_the_video_is_sought_to_seconds(string name, float seconds)
	{
		var player = PlayerOf(name);
		await TestTargetFixture.RunOnUIThreadAsync(() => player.Seek(TimeSpan.FromSeconds(seconds)))
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Leaves a player that is NOT playing alone for a while, which is the requirement itself
	/// rather than a wait for something to happen: a frozen picture is a claim about a stretch of
	/// time in which nothing may change, and there is no signal for nothing happening.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long to leave it alone.</param>
	/// <returns>A task that completes once that long has passed and the UI thread is idle.</returns>
	[When("the paused video {string} is left alone for {int} milliseconds")]
	public async Task When_the_paused_video_is_left_alone_for_milliseconds(string name, int milliseconds)
	{
		var playing = await ReadAsync(PlayerOf(name), one => one.IsPlaying).ConfigureAwait(false);

		playing.Should().BeFalse(
			"\"{0}\" must not be playing when the scenario leaves it alone: this step is about what does "
			+ "NOT change while nothing is running",
			name);

		await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Waits until playback has passed a timecode, which is the signal that pixels and the clock
	/// are both moving. A scenario that wants to look at the second half of a clip says this
	/// rather than resting for a computed number of milliseconds.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The timecode playback has to pass.</param>
	/// <param name="milliseconds">How long it has to pass it in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[When("{string} has played past {float} seconds within {int} milliseconds")]
	[Then("{string} has played past {float} seconds within {int} milliseconds")]
	public async Task Then_has_played_past_seconds_within_milliseconds(string name, float seconds, int milliseconds)
	{
		var player = PlayerOf(name);
		var budget = TimeSpan.FromMilliseconds(milliseconds);

		var reached = await Poll.UntilAsync(
			async () => await ReadAsync(player, one => one.PositionSeconds).ConfigureAwait(false) >= seconds,
			budget,
			PollInterval).ConfigureAwait(false);

		var position = await ReadAsync(player, one => one.PositionSeconds).ConfigureAwait(false);

		reached.Should().BeTrue(
			"\"{0}\" must play past {1} seconds within {2}; it reached {3} and {4}",
			name, seconds, budget, position, FailureOf(name));

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// -------------------------------------------------- the picture arriving

	/// <summary>
	/// Waits until a player has put a picture on its surface at all. This is the completion
	/// signal a video element does not raise: the decoder posts a frame, the driver composes it
	/// and hands it to the surface, and the picture the surface is holding is what a screenshot
	/// would return. Everything else a scenario says about the picture is said after this.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long the first picture has to arrive in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("{string} has presented a frame within {int} milliseconds")]
	[When("{string} has presented a frame within {int} milliseconds")]
	[Then("{string} has presented a frame within {int} milliseconds")]
	public async Task Then_has_presented_a_frame_within_milliseconds(string name, int milliseconds)
	{
		var player = PlayerOf(name);
		var budget = TimeSpan.FromMilliseconds(milliseconds);
		var waited = Stopwatch.StartNew();

		var presented = await Poll.UntilAsync(
			() => HasPresentedAsync(player),
			budget,
			PollInterval).ConfigureAwait(false);

		waited.Stop();
		var statistics = await ReadAsync(player, one => FormattableString.Invariant($"{one.FrameStatistics}"))
			.ConfigureAwait(false);

		// The latency a person would feel between asking for a clip and seeing it. It is written
		// out rather than asserted on: how long a decoder takes is a fact about the machine, and
		// the requirement is the budget, which the assertion below states.
		_outputHelper.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"first picture of \"{name}\" was on the surface after {waited.ElapsedMilliseconds} ms ({statistics})"));

		presented.Should().BeTrue(
			"\"{0}\" must present a picture within {1}; the presenter reported {2} and {3}",
			name, budget, statistics, FailureOf(name));

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Waits until a player's picture has reached the PANEL - which is a later moment than the
	/// picture reaching the player. The surface suppresses live presents for half a second after
	/// the last size change (what a resize drag needs, so that a backlog of full-size blits never
	/// piles up), so the first picture of a clip is on the surface well before it is painted; a
	/// scenario that measures where the picture is, rather than what colour it is, waits here.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long the picture has to reach the panel in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("{string} is showing a picture within {int} milliseconds")]
	[When("{string} is showing a picture within {int} milliseconds")]
	[Then("{string} is showing a picture within {int} milliseconds")]
	public async Task Then_is_showing_a_picture_within_milliseconds(string name, int milliseconds)
	{
		await Poll.UntilTheRegionShowsAsync(
			_scenarioContext,
			name,
			region => CanvasAssert.InkFraction(region) >= CanvasAssert.HasInkFraction,
			TimeSpan.FromMilliseconds(milliseconds),
			PollInterval).ConfigureAwait(false);

		// Either the picture reached the panel or the budget is gone; stating the requirement now
		// is what puts the region's real colours into the report.
		(await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false)).HasInk();
	}

	// ------------------------------------------------------ what the tree says

	/// <summary>Asserts how long a player says its clip is.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The length in seconds.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the DurationSeconds of {string} is about {float}")]
	public async Task Then_the_DurationSeconds_of_is_about(string name, float seconds) =>
		(await ReadAsync(PlayerOf(name), one => one.DurationSeconds).ConfigureAwait(false))
			.Should().BeApproximately(seconds, 0.1, "the DurationSeconds of \"{0}\" was asserted", name);

	/// <summary>
	/// Asserts where playback is, as a BAND. A position is a moving number read at a moment of
	/// its own choosing, so a requirement about one says which stretch of the clip it is in and
	/// never an exact value.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="lower">The earliest timecode that satisfies the requirement.</param>
	/// <param name="upper">The latest timecode that satisfies the requirement.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the PositionSeconds of {string} is between {float} and {float}")]
	public async Task Then_the_PositionSeconds_of_is_between(string name, float lower, float upper)
	{
		var position = await ReadAsync(PlayerOf(name), one => one.PositionSeconds).ConfigureAwait(false);

		position.Should().BeGreaterThanOrEqualTo(lower,
			"the PositionSeconds of \"{0}\" was asserted to be between {1} and {2}", name, lower, upper);
		position.Should().BeLessThanOrEqualTo(upper,
			"the PositionSeconds of \"{0}\" was asserted to be between {1} and {2}", name, lower, upper);
	}

	/// <summary>Asserts that playback is at (or very near) the beginning of the clip.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The timecode it must be before.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the PositionSeconds of {string} is less than {float}")]
	public async Task Then_the_PositionSeconds_of_is_less_than(string name, float seconds) =>
		(await ReadAsync(PlayerOf(name), one => one.PositionSeconds).ConfigureAwait(false))
			.Should().BeLessThan(seconds, "the PositionSeconds of \"{0}\" was asserted", name);

	/// <summary>
	/// Remembers where playback is, so that a later step can say it has not moved. A Given as
	/// well as a When: remembering is arranging.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the position has been read.</returns>
	[Given("the position of {string} is remembered")]
	[When("the position of {string} is remembered")]
	public async Task When_the_position_of_is_remembered(string name) =>
		_scenarioContext[PositionMarkKey + name] =
			await ReadAsync(PlayerOf(name), one => one.PositionSeconds).ConfigureAwait(false);

	/// <summary>Asserts that playback has stayed where it was remembered.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">How far it is allowed to have moved.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the PositionSeconds of {string} has not moved by more than {float} seconds")]
	public async Task Then_the_PositionSeconds_of_has_not_moved(string name, float seconds)
	{
		if (!_scenarioContext.TryGetValue(PositionMarkKey + name, out var stored) || stored is not double remembered)
		{
			throw new InvalidOperationException(
				$"The scenario never remembered where \"{name}\" was. Write \"the position of \"{name}\" is "
				+ "remembered\" before the step that says it has not moved.");
		}

		var position = await ReadAsync(PlayerOf(name), one => one.PositionSeconds).ConfigureAwait(false);

		Math.Abs(position - remembered).Should().BeLessThanOrEqualTo(seconds,
			"the position of \"{0}\" must stay where it was: it was {1} and it is {2}",
			name, remembered, position);
	}

	/// <summary>Asserts that a player is playing.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the video {string} is playing")]
	public async Task Then_the_video_is_playing(string name) =>
		(await ReadAsync(PlayerOf(name), one => one.IsPlaying).ConfigureAwait(false))
			.Should().BeTrue("the IsPlaying of \"{0}\" was asserted", name);

	/// <summary>Asserts that a player is not playing.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the video {string} is not playing")]
	public async Task Then_the_video_is_not_playing(string name) =>
		(await ReadAsync(PlayerOf(name), one => one.IsPlaying).ConfigureAwait(false))
			.Should().BeFalse("the IsPlaying of \"{0}\" was asserted", name);

	/// <summary>Asserts that a player's soundtrack is silenced.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the soundtrack of {string} is silent")]
	public async Task Then_the_soundtrack_of_is_silent(string name)
	{
		var player = PlayerOf(name);
		var muted = await ReadAsync(player, one => one.IsMuted).ConfigureAwait(false);
		var volume = await ReadAsync(player, one => one.Volume).ConfigureAwait(false);

		muted.Should().BeTrue("the IsMuted of \"{0}\" was asserted", name);
		volume.Should().BeApproximately(0.0, 0.001, "the Volume of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts which render path is actually running.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="backend">Gpu or Cpu.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ActiveRenderPath of {string} is {string}")]
	public async Task Then_the_ActiveRenderPath_of_is(string name, string backend) =>
		(await ReadAsync(PlayerOf(name), one => one.ActiveRenderPath).ConfigureAwait(false))
			.Should().Be(GherkinValue.ToEnum<VideoRenderBackend>(backend),
				"the ActiveRenderPath of \"{0}\" was asserted", name);

	/// <summary>
	/// Asserts that a player left to choose for itself came up on one of the two paths. Which
	/// one it is is a fact about the MACHINE - whether an off-screen graphics context could be
	/// created - so the requirement is that the player settled on a real path and says which,
	/// not that it settled on a particular one.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ActiveRenderPath of {string} is Gpu or Cpu")]
	public async Task Then_the_ActiveRenderPath_of_is_Gpu_or_Cpu(string name)
	{
		var backend = await ReadAsync(PlayerOf(name), one => one.ActiveRenderPath).ConfigureAwait(false);

		// Which one it is is what this scenario cannot assert and a reader still wants to know.
		_outputHelper.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"\"{name}\" left to choose came up on the {backend} path"));

		Enum.IsDefined(backend).Should().BeTrue(
			"the ActiveRenderPath of \"{0}\" must be one of the paths the engine offers; it is {1}",
			name, backend);
	}

	/// <summary>Asserts which render path a player was asked for.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="path">GpuAuto, GpuNoFallback or Cpu.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the RenderPath of {string} is still {string}")]
	public async Task Then_the_RenderPath_of_is_still(string name, string path) =>
		(await ReadAsync(PlayerOf(name), one => one.RenderPath).ConfigureAwait(false))
			.Should().Be(GherkinValue.ToEnum<VideoRenderPath>(path),
				"the RenderPath of \"{0}\" was asserted", name);

	/// <summary>
	/// Tries to change the render path of a player that has a clip open, and remembers what
	/// happened. The path is chosen before anything is opened - the presenter's surface, its
	/// shaders and the graphics context are all built around it - so this is the attempt a
	/// requirement says must be refused.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="path">The path the scenario tries to move to.</param>
	/// <returns>A task that completes once the attempt has been made.</returns>
	[When("the RenderPath of {string} is changed to {string} with the clip open")]
	public async Task When_the_RenderPath_of_is_changed_with_the_clip_open(string name, string path)
	{
		var player = PlayerOf(name);
		var wanted = GherkinValue.ToEnum<VideoRenderPath>(path);
		Exception? refusal = null;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			try
			{
				player.RenderPath = wanted;
			}
			catch (Exception attempt)
			{
				refusal = attempt;
			}
		}).ConfigureAwait(false);

		_scenarioContext[RefusalKey] = refusal!; // ANDROID PORT (a): the device ScenarioContext shim is NRT-annotated
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts that the attempted render-path change was refused, with a message.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	[Then("the change to the RenderPath of {string} was refused")]
	public void Then_the_change_to_the_RenderPath_of_was_refused(string name)
	{
		var refusal = _scenarioContext.TryGetValue(RefusalKey, out var stored) ? stored as Exception : null;

		(refusal is InvalidOperationException).Should().BeTrue(
			"changing the RenderPath of \"{0}\" while its clip is open must be refused; the attempt {1}",
			name,
			refusal is null ? "was accepted" : "threw " + refusal.GetType().Name);

		refusal!.Message.Should().NotBe(string.Empty,
			"the refusal must say what the rule is and what to do about it");
	}

	/// <summary>Asserts that a player's own failure message says something.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	[Then("the failure of {string} explains what went wrong")]
	public void Then_the_failure_of_explains_what_went_wrong(string name)
	{
		var message = FailureMessageOf(name);

		message.Should().NotBe(string.Empty,
			"\"{0}\" must explain a failure it reports; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));
	}

	// ----------------------------------------------------- the bound scrubber

	/// <summary>
	/// Puts a Slider in a container and binds it to a player exactly the way an application's
	/// markup does: its Maximum follows the clip's length and its Value follows - and drives -
	/// the timecode. The transport is the audio player element's, member for member, so this is
	/// the same markup either kind of player is driven by, which is the add-in's own claim.
	/// </summary>
	/// <param name="parentName">The Gherkin name of the container the scrubber goes in.</param>
	/// <param name="sliderName">The name the scenario will refer to the scrubber by.</param>
	/// <param name="playerName">The Gherkin name of the player it follows.</param>
	/// <returns>A task that completes once the scrubber is in the tree and laid out.</returns>
	[Given("the layout {string} holds a Slider named {string} bound to {string}")]
	public async Task Given_the_layout_holds_a_Slider_bound_to(string parentName, string sliderName, string playerName)
	{
		var player = PlayerOf(playerName);
		var slider = await ElementFactory.CreateAsync(VideoSliderKind, sliderName, new[]
		{
			new KeyValuePair<string, string>("Width", "800"),
			new KeyValuePair<string, string>("Minimum", "0"),

			// A tenth of a second, as an application's own scrubber markup sets it: the default
			// step is a whole unit, which on a slider whose track is a two-second clip would put
			// the thumb in one of three places.
			new KeyValuePair<string, string>("StepFrequency", "0.1"),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			slider.SetBinding(RangeBase.MaximumProperty, new Binding
			{
				Source = player,
				Path = new PropertyPath(nameof(VideoPlayerElement.DurationSeconds)),
				Mode = BindingMode.OneWay,
			});
			slider.SetBinding(RangeBase.ValueProperty, new Binding
			{
				Source = player,
				Path = new PropertyPath(nameof(VideoPlayerElement.PositionSeconds)),
				Mode = BindingMode.TwoWay,
			});

			if (ElementRegistry.Resolve(parentName) is not Panel parent)
			{
				throw new NotSupportedException(
					$"\"{parentName}\" cannot hold a scrubber: write the name of a panel.");
			}

			parent.Children.Add(slider);
			VirtualApplication.Instance.Root.UpdateLayout();
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// --------------------------------------------------------------- inner

	private const string VideoSliderKind = "Slider";

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(VideoPlayerKind, BuildVideoPlayer);

		ElementFactory.RegisterProperty<VideoPlayerElement>("Source",
			(player, value) => player.Source = VideoFixtures.PathOf(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("Stretch",
			(player, value) => player.Stretch = GherkinValue.ToEnum<Stretch>(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("RenderPath",
			(player, value) => player.RenderPath = GherkinValue.ToEnum<VideoRenderPath>(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("IsLooping",
			(player, value) => player.IsLooping = ReadBoolean(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("AutoPlay",
			(player, value) => player.AutoPlay = ReadBoolean(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("IsMuted",
			(player, value) => player.IsMuted = ReadBoolean(value));
		ElementFactory.RegisterProperty<VideoPlayerElement>("Volume",
			(player, value) => player.Volume = GherkinValue.ToDouble(value));
	}

	// Every player is built silent, on the processor path, and with a position that refreshes
	// often enough for a bound scrubber to be seen moving. The render path in particular is a
	// CONSTRUCTION-time decision: it may not be changed once a source is open, and GpuAuto's
	// answer depends on whether the machine could give the element an off-screen graphics
	// context - which is a fact about the machine, not about the requirement.
	private static FrameworkElement BuildVideoPlayer()
	{
		var player = new VideoPlayerElement
		{
			RenderPath = VideoRenderPath.Cpu,
			IsMuted = true,
			Volume = 0.0,
			PositionUpdateInterval = TimeSpan.FromMilliseconds(50),
		};

		// The element factory names the element immediately after building it, so the name is
		// there by the time any of these can be raised.
		player.MediaOpened += (sender, _) => RecordEvent(sender, "MediaOpened");
		player.PlaybackEnded += (sender, _) => RecordEvent(sender, "PlaybackEnded");
		player.RenderPathChanged += (sender, _) => RecordEvent(sender, "RenderPathChanged");
		player.ChapterChanged += (sender, _) => RecordEvent(sender, "ChapterChanged");
		player.MediaFailed += (sender, failure) =>
		{
			RecordEvent(sender, "MediaFailed");
			if (sender is VideoPlayerElement failed && !string.IsNullOrEmpty(failed.Name))
			{
				lock (RegistrationLock)
				{
					LastFailures[failed.Name] = failure.Message;
				}
			}
		};

		lock (RegistrationLock)
		{
			OpenedPlayers.Add(player);
		}

		return player;
	}

	private static void RecordEvent(object? sender, string eventName)
	{
		if (sender is FrameworkElement element && !string.IsNullOrEmpty(element.Name))
		{
			EventRecorder.Record(element.Name, eventName);
		}
	}

	private static VideoPlayerElement PlayerOf(string name) =>
		ElementRegistry.Resolve(name) as VideoPlayerElement
		?? throw new NotSupportedException(
			$"\"{name}\" is not a VideoPlayer, so it has no clip, no transport and no picture.");

	private static async Task<TValue> ReadAsync<TValue>(VideoPlayerElement player, Func<VideoPlayerElement, TValue> read)
	{
		var value = default(TValue)!;
		await TestTargetFixture.RunOnUIThreadAsync(() => value = read(player)).ConfigureAwait(false);
		return value;
	}

	// The picture the surface element is holding is what a screenshot would return, and it is
	// null until the first composed frame reaches it - which makes it the completion signal the
	// element does not raise. The copy it hands back belongs to the caller.
	private static async Task<bool> HasPresentedAsync(VideoPlayerElement player)
	{
		var presented = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			using var picture = player.CapturePresentedFrame();
			presented = picture is not null;
		}).ConfigureAwait(false);

		return presented;
	}

	private static string FailureOf(string name)
	{
		var message = FailureMessageOf(name);
		return message.Length == 0 ? "no failure" : "the failure \"" + message + "\"";
	}

	private static string FailureMessageOf(string name)
	{
		lock (RegistrationLock)
		{
			return LastFailures.TryGetValue(name, out var message) ? message : string.Empty;
		}
	}

	private static bool ReadBoolean(string value) =>
		bool.Parse(GherkinValue.Unquote(value));

	// The one machine fact these scenarios depend on: a clip that carries an audio track opens
	// an output device. Nothing is ever heard (every fixture is digital silence and every player
	// is muted at volume zero), but a machine with no sound server has nothing to open at all,
	// and saying so is better than a blank region.
	private static bool SoundDeviceLooksPresent()
	{
		// ANDROID PORT (d): the device's audio service lists its output devices (the Linux probe below reads /proc/asound
		// and the sound-server sockets, which an Android app cannot see).
		if (OperatingSystem.IsAndroid())
		{
			var audio = (global::Android.Media.AudioManager?)global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.AudioService);
			return audio?.GetDevices(global::Android.Media.GetDevicesTargets.Outputs)?.Length > 0;
		}

		try
		{
			if (File.Exists("/proc/asound/cards")
				&& File.ReadAllText("/proc/asound/cards").Trim().Length > 0)
			{
				return true;
			}

			var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
			return runtimeDirectory is { Length: > 0 }
				&& (File.Exists(Path.Combine(runtimeDirectory, "pulse", "native"))
					|| File.Exists(Path.Combine(runtimeDirectory, "pipewire-0")));
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}
