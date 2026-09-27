using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AddIn.Lottie.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using Colors = CodeBrix.Platform.UI.Core.UIReqs.Support.Colors;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;

namespace CodeBrix.Platform.UI.AddIn.Lottie.UIReqs.Steps;

/// <summary>
/// The animation group's vocabulary: the player a scenario puts an animation into, the
/// settings that belong to the animation SOURCE rather than to the player, and the handful of
/// things the core harness cannot say - that an animation has finished loading, how long it
/// lasts, whether it is playing, and the four commands that drive it.
/// <para>
/// Everything else an animation scenario says - showing the player, sizing it, what its region
/// looks like, whether two frames differ - is the core harness's own vocabulary, reached
/// through this project's reqnroll.json binding assemblies. Nothing from the core project is
/// duplicated here.
/// </para>
/// <para>
/// There is no sleep in this class that is not a scenario's own deliberate "leave it running
/// for so long": the animation offers no completion event and no per-frame event, so the
/// signals waited on are its dependency properties -
/// <see cref="AnimatedVisualPlayer.IsAnimatedVisualLoadedProperty"/> through a property-changed
/// callback for the load, and <see cref="AnimatedVisualPlayer.IsPlayingProperty"/> through a
/// bounded poll for the end of a segment. Both state their budget, so a run that never gets
/// there fails with a number rather than hanging.
/// </para>
/// <para>
/// Play, Pause, Resume and SetProgress all run on the UI thread, because playing builds a
/// dispatcher timer FOR THE CALLING THREAD; a command issued from anywhere else would build
/// its timer on a thread with no dispatcher and the animation would simply never advance.
/// </para>
/// </summary>
[Binding]
public sealed class LottieSteps
{
	/// <summary>The kind a feature file asks for to get a player an animation can be put into.</summary>
	public const string PlayerKind = "AnimatedVisualPlayer";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-lottie</c> tag: the
	/// add-in assembly, its decoder and the provider registration. Every machine that can build
	/// this project has all three, so nothing is ever skipped for it here - but a run whose
	/// add-in could not be loaded reports "skipped: the animation add-in is not usable" instead
	/// of failing over regions that are merely blank.
	/// </summary>
	public const string LottiePrerequisite = "lottie";

	/// <summary>How long an animation is given to load before the load step gives up.</summary>
	public static readonly TimeSpan LoadBudget = TimeSpan.FromSeconds(10);

	/// <summary>How long a poll leaves the animation alone between two looks at it.</summary>
	public static readonly TimeSpan PlayPollInterval = TimeSpan.FromMilliseconds(50);

	private const char ThemeColorSeparator = '=';

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public LottieSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this group's nouns, before the first scenario.
	/// <para>
	/// The player is built with autoplay OFF, because an animation that starts the moment it is
	/// loaded never lets the panel stand still, and most of these requirements are about what a
	/// person sees in ONE frame. A scenario that is about autoplay turns it on in its own table.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_Lottie_vocabulary()
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
				// The two animation sources are ordinary classes and need no registration, but
				// the framework's ProgressRing resolves its source through the registry and
				// draws a red warning line when nobody answered. One scenario is about exactly
				// that, so a process without the registration has nothing to say.
				if (!ApiExtensibility.IsRegistered<ILottieVisualSourceProvider>())
				{
					Prerequisite.Missing(LottiePrerequisite,
						"nothing registered an animation-source provider in this process, so the "
						+ "framework's ProgressRing cannot draw a ring (Registration.cs is the module "
						+ "initializer that does it)");
					return;
				}

				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(LottiePrerequisite,
					$"the animation add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(PlayerKind, () => new AnimatedVisualPlayer
		{
			// A player with nothing to play measures to nothing, and every scenario here is
			// about a square panel of animation, so the kind carries the size and the fit it is
			// always given. A scenario that is about either of them says so in its own table.
			AutoPlay = false,
			Stretch = Stretch.Uniform,
			Width = 240,
			Height = 240,
		});

		// "Source", "Stretch" and "AutoPlay" are universal words - an image, a media element and
		// a web view all have a Source, and Stretch already means something to an Image in this
		// process - so all of them go in as TYPED setters, which leaves each word free for every
		// other control. "PlaybackRate" and "ThemeColor" are not universal, but they belong to
		// this player just as narrowly, and a typed setter says so.
		ElementFactory.RegisterProperty<AnimatedVisualPlayer>("Source", SetSource);
		ElementFactory.RegisterProperty<AnimatedVisualPlayer>("AutoPlay",
			(player, value) => player.AutoPlay = ReadBoolean(value));
		ElementFactory.RegisterProperty<AnimatedVisualPlayer>("Stretch",
			(player, value) => player.Stretch = GherkinValue.ToEnum<Stretch>(value));
		ElementFactory.RegisterProperty<AnimatedVisualPlayer>("PlaybackRate",
			(player, value) => player.PlaybackRate = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty<AnimatedVisualPlayer>("ThemeColor", SetThemeColor);
	}

	// ------------------------------------------------------------- arranging

	/// <summary>
	/// Waits until a player reports that its animation has finished loading. The load is what
	/// every other claim in a scenario stands on: a mistyped URI or an unparseable document is
	/// reported nowhere - the player simply stays empty - so a scenario says this first and gets
	/// a failure that names the load rather than one about a blank rectangle.
	/// <para>
	/// The signal is the player's own IsAnimatedVisualLoaded dependency property, watched
	/// through a property-changed callback. There is no completion event to wait on: the play
	/// command returns an action that is already complete.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the animation is loaded.</returns>
	[Given("the animation of {string} is loaded")]
	[When("the animation of {string} is loaded")]
	public async Task Given_the_animation_of_is_loaded(string name)
	{
		var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var token = 0L;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var player = PlayerOf(name);
			if (player.IsAnimatedVisualLoaded)
			{
				loaded.TrySetResult();
				return;
			}

			token = player.RegisterPropertyChangedCallback(
				AnimatedVisualPlayer.IsAnimatedVisualLoadedProperty,
				(sender, _) =>
				{
					if (sender is AnimatedVisualPlayer watched && watched.IsAnimatedVisualLoaded)
					{
						loaded.TrySetResult();
					}
				});
		}).ConfigureAwait(false);

		var arrived = true;
		try
		{
			await loaded.Task.WaitAsync(LoadBudget, TestContext.Current.CancellationToken).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			arrived = false;
		}
		finally
		{
			if (token != 0L)
			{
				await TestTargetFixture.RunOnUIThreadAsync(() =>
					PlayerOf(name).UnregisterPropertyChangedCallback(
						AnimatedVisualPlayer.IsAnimatedVisualLoadedProperty, token)).ConfigureAwait(false);
			}
		}

		arrived.Should().BeTrue(
			"the animation of \"{0}\" must have loaded within {1} milliseconds; a document that cannot be "
			+ "found or cannot be decoded is reported nowhere - the player just stays empty",
			name, LoadBudget.TotalMilliseconds);

		// The decoded animation is drawn by a canvas the source adds as the player's child, and
		// it is invalidated from the load. Draining the dispatcher here means the next frame the
		// scenario asks for is the one that shows it.
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Puts a player's animation at one point of its timeline and leaves it there.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="progress">Where to put it, from 0 at the start to 1 at the end.</param>
	/// <returns>A task that completes once the frame at that point has been drawn.</returns>
	[Given("the progress of {string} is set to {float}")]
	[When("the progress of {string} is set to {float}")]
	public async Task When_the_progress_of_is_set_to(string name, float progress)
	{
		await OnThePlayerAsync(name, player => player.SetProgress(progress)).ConfigureAwait(false);
	}

	/// <summary>Plays a segment of a player's animation once, from one point to another.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="fromProgress">Where the segment starts, from 0 to 1.</param>
	/// <param name="toProgress">Where the segment ends, from 0 to 1.</param>
	/// <returns>A task that completes once playing has started.</returns>
	[When("{string} plays from {float} to {float}")]
	public async Task When_plays_from_to(string name, float fromProgress, float toProgress)
	{
		await PlayAsync(name, fromProgress, toProgress, looped: false).ConfigureAwait(false);
	}

	/// <summary>Plays a segment of a player's animation over and over.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="fromProgress">Where the segment starts, from 0 to 1.</param>
	/// <param name="toProgress">Where the segment ends, from 0 to 1.</param>
	/// <returns>A task that completes once playing has started.</returns>
	[When("{string} plays from {float} to {float} looped")]
	public async Task When_plays_from_to_looped(string name, float fromProgress, float toProgress)
	{
		await PlayAsync(name, fromProgress, toProgress, looped: true).ConfigureAwait(false);
	}

	/// <summary>Halts a playing animation where it is.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the animation has been halted.</returns>
	[Given("{string} is paused")]
	[When("{string} is paused")]
	public async Task When_is_paused(string name) =>
		await OnThePlayerAsync(name, player => player.Pause()).ConfigureAwait(false);

	/// <summary>Sets a paused animation going again from the frame it was halted on.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the animation is going again.</returns>
	[When("{string} is resumed")]
	public async Task When_is_resumed(string name) =>
		await OnThePlayerAsync(name, player => player.Resume()).ConfigureAwait(false);

	/// <summary>Ends playing altogether, leaving the frame that was last drawn on the panel.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the animation has been stopped.</returns>
	[When("{string} is stopped")]
	public async Task When_is_stopped(string name) =>
		await OnThePlayerAsync(name, player => player.Stop()).ConfigureAwait(false);

	/// <summary>
	/// Leaves an animation alone for a while, so that the next frame catches it further along.
	/// The frame an animation shows is worked out from a stopwatch, so what a scenario may claim
	/// after this step is coarse - that two frames differ, or that they do not - never that a
	/// particular frame is showing.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long to leave it.</param>
	/// <returns>A task that completes once the time has passed and the UI thread is idle.</returns>
	[When("{string} is left running for {int} milliseconds")]
	public async Task When_is_left_running_for_milliseconds(string name, int milliseconds)
	{
		ElementRegistry.Resolve(name);
		await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ------------------------------------------------------------ assertions

	/// <summary>
	/// Asserts that a player has an animation to draw. It is a fact about the tree, and it is
	/// the one the whole feature stands on.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the animation of {string} has loaded")]
	public async Task Then_the_animation_of_has_loaded(string name)
	{
		var loaded = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => loaded = PlayerOf(name).IsAnimatedVisualLoaded)
			.ConfigureAwait(false);

		loaded.Should().BeTrue("the loaded state of the animation in \"{0}\" was asserted", name);
	}

	/// <summary>
	/// Asserts how long a player's animation lasts. The duration comes from the decoded document
	/// - its frame count over its frame rate - so it is what says that the document a scenario
	/// meant to load is the document that loaded.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long it must last.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the animation of {string} lasts {int} milliseconds")]
	public async Task Then_the_animation_of_lasts_milliseconds(string name, int milliseconds)
	{
		var duration = TimeSpan.Zero;
		await TestTargetFixture.RunOnUIThreadAsync(() => duration = PlayerOf(name).Duration)
			.ConfigureAwait(false);

		((int) Math.Round(duration.TotalMilliseconds)).Should().Be(milliseconds,
			"the duration of the animation in \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that a player's animation is running.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the player {string} is playing")]
	public async Task Then_the_player_is_playing(string name)
	{
		var playing = await IsPlayingAsync(name).ConfigureAwait(false);
		playing.Should().BeTrue("\"{0}\" was asserted to be playing", name);
	}

	/// <summary>Asserts that a player's animation is not running.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the player {string} is not playing")]
	public async Task Then_the_player_is_not_playing(string name)
	{
		var playing = await IsPlayingAsync(name).ConfigureAwait(false);
		playing.Should().BeFalse("\"{0}\" was asserted to have stopped", name);
	}

	/// <summary>
	/// Asserts that a segment ends by itself, inside a budget. There is no event for the end of
	/// a segment - the play command's action is already complete when it is handed back - so the
	/// only signal is the player's IsPlaying property, and the only honest way to watch it is to
	/// keep asking. Each look captures a fresh frame, which is also what keeps the animation
	/// being drawn: the frame the poll ends on is the scenario's current frame, so what a
	/// scenario says next is about the picture the animation stopped on.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long the segment has to end in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the player {string} stops playing within {int} milliseconds")]
	public async Task Then_the_player_stops_playing_within_milliseconds(string name, int milliseconds)
	{
		// ANDROID PORT (g): on Android the animation is drawn by its native view on every display frame, not by the
		// capture, so a frame captured BEFORE a look at IsPlaying can predate the end of the segment while the look
		// already finds it stopped (the segment ends inside the draw that reaches it) - the timing flake the Platform
		// reports for this scenario, only wider here because a device capture takes longer. The poll therefore only
		// looks at IsPlaying, and the frame is captured once, after the player has stopped: once stopped, every draw
		// shows the segment's end frame, so the scenario's frame is the picture the animation came to rest on.
		var stopped = await Poll.UntilAsync(
			async () => !await IsPlayingAsync(name).ConfigureAwait(false),
			TimeSpan.FromMilliseconds(milliseconds),
			PlayPollInterval).ConfigureAwait(false);

		await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName).ConfigureAwait(false);

		stopped.Should().BeTrue(
			"\"{0}\" must have reached the end of its segment and stopped within {1} milliseconds",
			name, milliseconds);
	}

	// --------------------------------------------------------------- inner

	private static async Task PlayAsync(string name, double fromProgress, double toProgress, bool looped)
	{
		// The returned action is already complete when it arrives - it says that playing has
		// STARTED, not that the segment has finished - so there is nothing to await but the
		// dispatcher, which the helper drains. Discarding it is deliberate, and is why the end
		// of a segment has to be watched for on the player's IsPlaying property instead.
		await OnThePlayerAsync(name,
			player => _ = player.PlayAsync(fromProgress, toProgress, looped)).ConfigureAwait(false);
	}

	private static async Task OnThePlayerAsync(string name, Action<AnimatedVisualPlayer> command)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => command(PlayerOf(name))).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<bool> IsPlayingAsync(string name)
	{
		var playing = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => playing = PlayerOf(name).IsPlaying)
			.ConfigureAwait(false);

		return playing;
	}

	private static void SetSource(AnimatedVisualPlayer player, string value)
	{
		var fixture = LottieFixtures.Named(GherkinValue.Unquote(value));

		if (!fixture.IsThemable)
		{
			player.Source = new LottieVisualSource { UriSource = fixture.Uri };
			return;
		}

		// A themable source may already be here, holding a colour the scenario set BEFORE it
		// named a document; giving it the document rather than replacing it is what makes
		// "the colour it was given before it loaded" a thing a scenario can say at all.
		ThemableSourceOf(player).UriSource = fixture.Uri;
	}

	private static void SetThemeColor(AnimatedVisualPlayer player, string value)
	{
		var setting = GherkinValue.Unquote(value);
		var separator = setting.IndexOf(ThemeColorSeparator, StringComparison.Ordinal);

		if (separator <= 0 || separator == setting.Length - 1)
		{
			throw new FormatException(
				$"\"{value}\" does not name a theme colour. Write the binding and the colour with an "
				+ "equals sign between them, as in \"Foreground=Red\".");
		}

		var binding = setting[..separator].Trim();
		var color = Colors.Parse(setting[(separator + 1)..].Trim());

		ThemableSourceOf(player).SetColorThemeProperty(binding, color);
	}

	private static ThemableLottieVisualSource ThemableSourceOf(AnimatedVisualPlayer player)
	{
		switch (player.Source)
		{
			case ThemableLottieVisualSource themable:
				return themable;

			case null:
				// The colour comes first and the document second, so the source that will carry
				// both is built here, empty. Loading starts when a document arrives, and nothing
				// happens before the player is in the tree in any case.
				var source = new ThemableLottieVisualSource();
				player.Source = source;
				return source;

			default:
				throw new NotSupportedException(NotThemable(player));
		}
	}

	// The message is built before it is handed over: string.Create's culture overload takes an
	// interpolated-string handler by reference, and a CONCATENATION of interpolated strings
	// cannot be passed that way (CS1620).
	private static string NotThemable(AnimatedVisualPlayer player)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"The animation in \"{player.Name}\" is a {player.Source?.GetType().Name}");

		return $"{subject}, which carries no colour bindings, so no colour can be set on it. A "
			+ $"scenario that sets one names the \"{LottieFixtures.Themed}\" animation.";
	}

	private static AnimatedVisualPlayer PlayerOf(string name)
	{
		var element = ElementRegistry.Resolve(name);

		return element as AnimatedVisualPlayer ?? throw new NotSupportedException(NotAPlayer(element));
	}

	private static string NotAPlayer(FrameworkElement element)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\"");

		return $"{subject} plays no animation, so nothing can be said about its playback. Ask for "
			+ $"an \"{PlayerKind}\".";
	}

	private static bool ReadBoolean(string value) => bool.Parse(GherkinValue.Unquote(value));
}
