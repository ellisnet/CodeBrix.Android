using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Media.Playback;
using CodeBrix.Platform.UI.AddIn.MediaPlayer.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Reqnroll;
using SilverAssertions;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
using MediaPlayerEngine = Windows.Media.Playback.MediaPlayer;

namespace CodeBrix.Platform.UI.AddIn.MediaPlayer.UIReqs.Steps;

/// <summary>
/// The media group's vocabulary: the element that plays a clip, the clips themselves, and the
/// handful of things the core harness cannot say - that a media has opened, that the picture on
/// the panel has caught up with the decoder, and what the built-in transport chrome is doing.
/// <para>
/// Everything else a media scenario says - showing the element, sizing it, where its colours
/// landed, whether a template part has ink, how far the built-in slider's thumb has moved,
/// which events were raised - is the core harness's own vocabulary, reached through this
/// project's reqnroll.json binding assemblies. Nothing from the core project is duplicated here.
/// </para>
/// <para>
/// There is no sleep anywhere in this class. Where a signal exists the step waits on it (the
/// element publishes its player through a dependency property, and the player raises MediaOpened,
/// MediaEnded and MediaFailed); where one does not - the picture on the panel is a paint-time
/// fact about a colour change that happened at decode time - the step is a BOUNDED POLL with the
/// budget written into the sentence, so a machine that never gets there fails with a number.
/// </para>
/// </summary>
[Binding]
public sealed class MediaPlayerSteps
{
	/// <summary>The kind a feature file asks for to get an element that plays a clip.</summary>
	public const string MediaPlayerElementKind = "MediaPlayerElement";

	/// <summary>
	/// The prerequisite name the playback feature files declare with a <c>@needs-libvlc</c> tag.
	/// The native playback engine is a SYSTEM package, not a NuGet: a machine without it cannot
	/// run the scenarios that play anything, and the first thing such a machine would otherwise
	/// see is an exception thrown from the middle of a Given. The chrome scenarios carry no tag,
	/// because drawing the transport controls, the poster and the empty box needs no engine.
	/// </summary>
	public const string EnginePrerequisite = "libvlc";

	/// <summary>The value a feature file writes to take a source away again.</summary>
	private const string NoneValue = "None";

	/// <summary>The visual state group that decides which face the play button wears.</summary>
	private const string PlayPauseStatesGroup = "PlayPauseStates";

	/// <summary>The state that shows the play glyph.</summary>
	private const string PlayStateName = "PlayState";

	/// <summary>The state that shows the pause glyph.</summary>
	private const string PauseStateName = "PauseState";

	/// <summary>The template part that shows the video frames.</summary>
	private const string PresenterPart = "MediaPlayerPresenter";

	/// <summary>The soname of the native playback engine this add-in drives.</summary>
	private const string EngineLibrary = "libvlc.so.5";

	private static readonly TimeSpan WaitInterval = TimeSpan.FromMilliseconds(50);

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public MediaPlayerSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	// ------------------------------------------------------------ the group

	/// <summary>
	/// Looks for the native engine, checks that the add-in is switched on, and teaches the
	/// element factory this group's nouns - before the first scenario.
	/// <para>
	/// The registration check is not a formality. In an application the two extension
	/// registrations are generated from XAML; a UIReqs project compiles none, so
	/// <c>Registration.cs</c> writes them by hand. Without them the element still builds, its
	/// template still applies and its transport controls still draw, while every Play, Pause and
	/// Position is a silent no-op - which would turn ten requirements about playback into ten
	/// passes about nothing. That is a defect in this project, not a property of the machine, so
	/// it is a hard failure here rather than a skip.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_MediaPlayer_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			if (!ApiExtensibility.IsRegistered<IMediaPlayerExtension>()
				|| !ApiExtensibility.IsRegistered<IMediaPlayerPresenterExtension>())
			{
				throw new InvalidOperationException(
					"Nothing registered the media add-in's playback engine or its video presenter in this "
					+ "process. Registration.cs is the module initializer that does it, and without it every "
					+ "transport call in this assembly is a silent no-op while the chrome still draws.");
			}

			try
			{
				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				Prerequisite.Missing(EnginePrerequisite,
					$"the media add-in could not be used ({failure.GetType().Name}: {failure.Message})");
				return;
			}

			// ANDROID PORT (d): the engine is Media3 ExoPlayer inside the Android MediaPlayer add-in (no native library to
			// look for, nothing to preload); the Linux libvlc check and PreloadVlc are removed.
		}
	}

	/// <summary>Starts the scenario with nothing remembered about any player.</summary>
	[BeforeScenario(Order = 1)]
	public static void Forget_what_the_players_reported() => PlayerWatch.Clear();

	/// <summary>
	/// Releases every player the scenario built. It runs AFTER the harness's own reset, which is
	/// what takes the elements off the panel: unloading a presenter stops its player, and a
	/// player that is disposed while its presenter is still in the tree would be stopped after
	/// its native handle had already gone. A player left alive keeps decode threads, a native
	/// player and - for a clip with an audio track - the output device open across the scenarios
	/// that follow.
	/// </summary>
	/// <returns>A task that completes once every player has been released.</returns>
	[AfterScenario(Order = 200)]
	public static async Task Release_the_players()
	{
		var players = PlayerRegistry.TakeAll();
		if (players.Count == 0 || !TestTargetFixture.IsLaunched)
		{
			return;
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			foreach (var player in players)
			{
				player.Source = null;
				player.Dispose();
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ------------------------------------------------------------- sources

	/// <summary>
	/// Hands a clip to an element. Setting the source is all this does: whether the engine can
	/// open it is the next step's business, and one scenario in this suite is about a source
	/// that names a file which is not there.
	/// </summary>
	/// <param name="clipName">The clip, as <see cref="MediaFixtures"/> names it.</param>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the UI thread has applied the source.</returns>
	[Given("the clip {string} is given to {string}")]
	[When("the clip {string} is given to {string}")]
	public static async Task Given_the_clip_is_given_to(string clipName, string elementName)
	{
		var clip = GherkinValue.Unquote(clipName);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			ElementOf(elementName).Source = MediaSource.CreateFromUri(MediaFixtures.UriOf(clip)))
			.ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Waits until the element's player says it has opened its media. This is a real signal -
	/// the engine raises MediaOpened once its parser has read the file's metadata - and the
	/// budget is only there so that a machine where it never arrives fails with a number.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="milliseconds">How long the media has to open in.</param>
	/// <returns>A task that completes when the media has opened, or the budget is gone.</returns>
	[Given("the media of {string} opens within {int} milliseconds")]
	[When("the media of {string} opens within {int} milliseconds")]
	[Then("the media of {string} opens within {int} milliseconds")]
	public async Task Then_the_media_of_opens_within(string elementName, int milliseconds)
	{
		var opened = await WaitForAsync(() => PlayerWatch.OpenedCount(elementName) > 0, milliseconds)
			.ConfigureAwait(false);

		opened.Should().BeTrue(
			"\"{0}\" must open its media within {1} ms; the players reported [{2}]",
			elementName, milliseconds, PlayerWatch.Describe());
	}

	/// <summary>Waits until the element's player says it could not play its media.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="milliseconds">How long the failure has to arrive in.</param>
	/// <returns>A task that completes when the failure has arrived, or the budget is gone.</returns>
	[Given("the media of {string} fails within {int} milliseconds")]
	[When("the media of {string} fails within {int} milliseconds")]
	[Then("the media of {string} fails within {int} milliseconds")]
	public async Task Then_the_media_of_fails_within(string elementName, int milliseconds)
	{
		var failed = await WaitForAsync(() => PlayerWatch.FailedCount(elementName) > 0, milliseconds)
			.ConfigureAwait(false);

		failed.Should().BeTrue(
			"\"{0}\" must report a failure within {1} ms; the players reported [{2}]",
			elementName, milliseconds, PlayerWatch.Describe());
	}

	/// <summary>Waits until the element's player says it reached the end of its media.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="milliseconds">How long the end has to arrive in.</param>
	/// <returns>A task that completes when the end has arrived, or the budget is gone.</returns>
	[Given("the media of {string} ends within {int} milliseconds")]
	[When("the media of {string} ends within {int} milliseconds")]
	[Then("the media of {string} ends within {int} milliseconds")]
	public async Task Then_the_media_of_ends_within(string elementName, int milliseconds)
	{
		var ended = await WaitForAsync(() => PlayerWatch.EndedCount(elementName) > 0, milliseconds)
			.ConfigureAwait(false);

		ended.Should().BeTrue(
			"\"{0}\" must reach the end of its media within {1} ms; the players reported [{2}]",
			elementName, milliseconds, PlayerWatch.Describe());
	}

	/// <summary>
	/// Asserts that the failure a player reported names the file it could not open. An
	/// application shows a person what went wrong out of this message and out of nothing else,
	/// so a failure with no message in it is a failure nobody can act on.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="expected">Text the message must contain.</param>
	[Then("the failure reported by {string} names {string}")]
	public static void Then_the_failure_reported_by_names(string elementName, string expected)
	{
		var wanted = GherkinValue.Unquote(expected);
		var failure = PlayerWatch.LastFailure(elementName);

		failure.Should().NotBeNull("\"{0}\" must have reported a failure to have a message", elementName);
		(failure!.ErrorMessage ?? string.Empty).Should().Contain(wanted,
			"the failure \"{0}\" reported must say which media it was about; it reported error {1} with "
			+ "message \"{2}\"",
			elementName, failure.Error, failure.ErrorMessage);
	}

	// ------------------------------------------------------------ transport

	/// <summary>Starts playback through the element's own player.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes once the UI thread has asked the player to play.</returns>
	[Given("{string} is played")]
	[When("{string} is played")]
	public static async Task When_is_played(string elementName)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => PlayerOf(elementName).Play()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Moves the playback position. The session's own setter is what an application writes to,
	/// and the engine acts on it at once - it raises no "the seek finished" event of any kind, so
	/// what a scenario waits on afterwards is the picture, never a signal that does not exist.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="seconds">Where to move it to.</param>
	/// <returns>A task that completes once the UI thread has moved the position.</returns>
	[Given("the position of {string} is moved to {float} seconds")]
	[When("the position of {string} is moved to {float} seconds")]
	public static async Task When_the_position_of_is_moved_to(string elementName, float seconds)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			PlayerOf(elementName).PlaybackSession.Position = TimeSpan.FromSeconds(seconds))
			.ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Waits until the element's player is in a state. The engine publishes its state through a
	/// sixteen-millisecond poll of its own rather than at the moment a call takes effect, so the
	/// state a Play or a Pause produces is not there the instant the call returns.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="expected">None, Opening, Buffering, Playing or Paused.</param>
	/// <param name="milliseconds">How long it has to get there.</param>
	/// <returns>A task that completes when the player is in that state, or the budget is gone.</returns>
	[Given("the playback state of {string} is {string} within {int} milliseconds")]
	[When("the playback state of {string} is {string} within {int} milliseconds")]
	[Then("the playback state of {string} is {string} within {int} milliseconds")]
	public async Task Then_the_playback_state_of_is_within(string elementName, string expected, int milliseconds)
	{
		var wanted = GherkinValue.ToEnum<MediaPlaybackState>(expected);
		var reached = await Poll.UntilAsync(
			async () => await StateOfAsync(elementName).ConfigureAwait(false) == wanted,
			TimeSpan.FromMilliseconds(milliseconds),
			WaitInterval).ConfigureAwait(false);

		if (!reached)
		{
			var actual = await StateOfAsync(elementName).ConfigureAwait(false);
			actual.Should().Be(wanted,
				"the playback state of \"{0}\" was asserted within {1} ms", elementName, milliseconds);
		}
	}

	/// <summary>
	/// Waits until the player has got past a point in its media. The engine publishes where it
	/// has got to on its own cadence rather than once per frame, so this is a bounded poll over
	/// a value that really does arrive - not a substitute for a signal that exists.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="seconds">The point in the media to get past.</param>
	/// <param name="milliseconds">How long it has to get there.</param>
	/// <returns>A task that completes when the player is past that point, or the budget is gone.</returns>
	[Given("the position of {string} passes {float} seconds within {int} milliseconds")]
	[When("the position of {string} passes {float} seconds within {int} milliseconds")]
	[Then("the position of {string} passes {float} seconds within {int} milliseconds")]
	public async Task Then_the_position_of_passes_seconds_within(string elementName, float seconds,
		int milliseconds)
	{
		var passed = await Poll.UntilAsync(
			async () => await PositionOfAsync(elementName).ConfigureAwait(false) >= seconds,
			TimeSpan.FromMilliseconds(milliseconds),
			WaitInterval).ConfigureAwait(false);

		if (!passed)
		{
			var position = await PositionOfAsync(elementName).ConfigureAwait(false);
			position.Should().BeGreaterThanOrEqualTo(seconds,
				"\"{0}\" must get past {1} seconds of its media within {2} ms", elementName, seconds,
				milliseconds);
		}
	}

	/// <summary>
	/// Asserts which of its two faces the built-in play button is wearing. The glyph is swapped
	/// by a visual state rather than by anybody setting a property, so the state the chrome is in
	/// IS the tree fact behind what a person sees; the pixels of the glyph are asserted
	/// separately, with the harness's own "differs from frame" sentence.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="expected">Play or Pause.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the built-in play button of {string} shows {string}")]
	public static async Task Then_the_built_in_play_button_of_shows(string elementName, string expected)
	{
		var wanted = GherkinValue.Unquote(expected);
		var actual = string.Empty;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = FaceOf(PlayPauseStateOf(elementName))).ConfigureAwait(false);

		actual.Should().Be(wanted,
			"the face of the built-in play button of \"{0}\" was asserted", elementName);
	}

	// -------------------------------------------------------------- the media

	/// <summary>Asserts how long the media a player opened is.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="seconds">The length the file really is.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the media of {string} is about {float} seconds long")]
	public static async Task Then_the_media_of_is_about_seconds_long(string elementName, float seconds)
	{
		var duration = TimeSpan.Zero;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			duration = PlayerOf(elementName).PlaybackSession.NaturalDuration).ConfigureAwait(false);

		Math.Abs(duration.TotalSeconds - seconds).Should().BeLessThanOrEqualTo(0.1,
			"the length \"{0}\" reported for its media was asserted; it reported {1} seconds",
			elementName, duration.TotalSeconds);
	}

	/// <summary>Asserts that the media a player opened has a picture in it.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the media of {string} has a picture")]
	public static async Task Then_the_media_of_has_a_picture(string elementName) =>
		(await HasPictureAsync(elementName).ConfigureAwait(false)).Should().BeTrue(
			"the media \"{0}\" opened must have a video track", elementName);

	/// <summary>Asserts that the media a player opened is sound only.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the media of {string} has no picture")]
	public static async Task Then_the_media_of_has_no_picture(string elementName) =>
		(await HasPictureAsync(elementName).ConfigureAwait(false)).Should().BeFalse(
			"the media \"{0}\" opened must have no video track", elementName);

	/// <summary>Asserts where in its media a player has got to.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="from">The earliest the position may be, in seconds.</param>
	/// <param name="to">The latest the position may be, in seconds.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the position of {string} is between {float} and {float} seconds")]
	public static async Task Then_the_position_of_is_between(string elementName, float from, float to)
	{
		var position = await PositionOfAsync(elementName).ConfigureAwait(false);

		position.Should().BeInRange(from, to,
			"the position of \"{0}\" was asserted as a band, because where a decoder has got to is not "
			+ "an exact number", elementName);
	}

	/// <summary>
	/// Asserts that the element is showing no video surface at all, which is what an audio-only
	/// media looks like: the surface the frames would be painted on stays collapsed, and the
	/// poster is what a person sees instead.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the video surface of {string} is hidden")]
	public static async Task Then_the_video_surface_of_is_hidden(string elementName) =>
		(await VideoSurfaceIsShowingAsync(elementName).ConfigureAwait(false)).Should().BeFalse(
			"\"{0}\" must show no video surface for a media with no picture in it", elementName);

	/// <summary>Asserts that the element is showing the surface its frames are painted on.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the video surface of {string} is showing")]
	public static async Task Then_the_video_surface_of_is_showing(string elementName) =>
		(await VideoSurfaceIsShowingAsync(elementName).ConfigureAwait(false)).Should().BeTrue(
			"\"{0}\" must show the surface its frames are painted on", elementName);

	// ---------------------------------------------------------------- inner

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(MediaPlayerElementKind, BuildElement);

		// "Stretch" and "Source" are universal words - an image, a web view and a media element
		// all have one - so they go in as TYPED setters, which leaves both words free for every
		// other control in the assembly.
		ElementFactory.RegisterProperty<MediaPlayerElement>("Stretch",
			(element, value) => element.Stretch = GherkinValue.ToEnum<Stretch>(value));
		ElementFactory.RegisterProperty<MediaPlayerElement>("Source",
			(element, value) => element.Source = ToSource(value));

		// These three are this control's own words, and no other control in this assembly spells
		// anything that way, so they go in the general table under their own names.
		ElementFactory.RegisterProperty("AreTransportControlsEnabled",
			(element, value) => AsMediaPlayerElement(element).AreTransportControlsEnabled = ToBool(value));
		ElementFactory.RegisterProperty("AutoPlay",
			(element, value) => AsMediaPlayerElement(element).AutoPlay = ToBool(value));

		// The poster is named by its COLOUR rather than by a file: a scenario that says the
		// poster is showing has to name what it expects to see, and a picture the test painted
		// itself cannot drift away from the colour the scenario asserts.
		ElementFactory.RegisterProperty("PosterColour",
			(element, value) => AsMediaPlayerElement(element).PosterSource = ToPoster(value));
	}

	private static MediaPlayerElement BuildElement()
	{
		var element = new MediaPlayerElement
		{
			// A scenario says when playback starts. Left at its default the element would start
			// playing the moment a source arrived, and half of these requirements are about what
			// is on the panel BEFORE anything has been asked to play.
			AutoPlay = false,
			AreTransportControlsEnabled = true,
			Stretch = Stretch.Uniform,
		};

		// The chrome hides itself three seconds after playback starts, and it fades while it
		// goes. That is a timer that repaints, so it is off for every scenario in this assembly;
		// nothing here is a requirement about the auto-hide.
		element.TransportControls.ShowAndHideAutomatically = false;

		// The element builds its own player as it applies its template, which is later than
		// this. The dependency property it publishes it through is the signal that it exists.
		element.RegisterPropertyChangedCallback(MediaPlayerElement.MediaPlayerProperty, AttachToThePlayer);
		return element;
	}

	private static void AttachToThePlayer(DependencyObject sender, DependencyProperty property)
	{
		if (sender is not MediaPlayerElement element || element.MediaPlayer is not { } player)
		{
			return;
		}

		var name = element.Name;
		if (string.IsNullOrEmpty(name) || !PlayerRegistry.Add(player))
		{
			return;
		}

		// Nothing is ever heard from these scenarios: the clips are digital silence AND the
		// player is muted with its volume at zero. Both, because either one alone would leave
		// the requirement resting on the fixtures being what they are said to be.
		player.IsMuted = true;
		player.Volume = 0;

		player.MediaOpened += (_, _) =>
		{
			PlayerWatch.Opened(name);
			EventRecorder.Record(name, "MediaOpened");
		};
		player.MediaEnded += (_, _) =>
		{
			PlayerWatch.Ended(name);
			EventRecorder.Record(name, "MediaEnded");
		};
		player.MediaFailed += (_, args) =>
		{
			PlayerWatch.Failed(name, args);
			EventRecorder.Record(name, "MediaFailed");
		};
		player.SourceChanged += (_, _) => EventRecorder.Record(name, "SourceChanged");
		player.NaturalVideoDimensionChanged += (_, _) =>
			EventRecorder.Record(name, "NaturalVideoDimensionChanged");
		player.PlaybackSession.PlaybackStateChanged += (_, _) =>
			EventRecorder.Record(name, "PlaybackStateChanged");
		player.PlaybackSession.PositionChanged += (_, _) => EventRecorder.Record(name, "PositionChanged");
		player.PlaybackSession.NaturalDurationChanged += (_, _) =>
			EventRecorder.Record(name, "NaturalDurationChanged");
	}

	private static MediaPlayerElement ElementOf(string elementName) =>
		AsMediaPlayerElement(ElementRegistry.Resolve(elementName));

	private static MediaPlayerElement AsMediaPlayerElement(FrameworkElement element) =>
		element as MediaPlayerElement ?? throw new NotSupportedException(NotAMediaPlayerElement(element));

	private static string NotAMediaPlayerElement(FrameworkElement element)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\"");

		return $"{subject} plays no media. Ask for a \"{MediaPlayerElementKind}\".";
	}

	private static MediaPlayerEngine PlayerOf(string elementName) =>
		ElementOf(elementName).MediaPlayer ?? throw new InvalidOperationException(
			$"\"{elementName}\" has no player yet. The element builds one as it applies its template, "
			+ "which happens the first time it is laid out - so a step that drives playback comes after "
			+ "the element is showing.");

	private static async Task<MediaPlaybackState> StateOfAsync(string elementName)
	{
		var state = MediaPlaybackState.None;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			state = PlayerOf(elementName).PlaybackSession.PlaybackState).ConfigureAwait(false);

		return state;
	}

	/// <summary>
	/// The name of the visual state the transport chrome's play/pause group is in. Call this on
	/// the UI thread.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <returns>The state name, or an empty string when the group has not been put into one.</returns>
	private static string PlayPauseStateOf(string elementName)
	{
		var element = ElementOf(elementName);
		var controls = VisualTreeSearch.FindDescendant<MediaTransportControls>(element)
			?? throw MissingPart(elementName, nameof(MediaTransportControls));

		// A control's visual state groups live on the first child of its template, which is where
		// the framework itself looks for them.
		var templateRoot = VisualTreeHelper.GetChildrenCount(controls) > 0
			? VisualTreeHelper.GetChild(controls, 0) as FrameworkElement
			: null;
		if (templateRoot is null)
		{
			throw MissingPart(elementName, PlayPauseStatesGroup);
		}

		foreach (var group in VisualStateManager.GetVisualStateGroups(templateRoot))
		{
			if (string.Equals(group.Name, PlayPauseStatesGroup, StringComparison.Ordinal))
			{
				return group.CurrentState?.Name ?? string.Empty;
			}
		}

		throw MissingPart(elementName, PlayPauseStatesGroup);
	}

	private static string FaceOf(string stateName) => stateName switch
	{
		PlayStateName => "Play",
		PauseStateName => "Pause",
		"" => "nothing yet",
		_ => stateName,
	};

	private static async Task<double> PositionOfAsync(string elementName)
	{
		var position = TimeSpan.Zero;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			position = PlayerOf(elementName).PlaybackSession.Position).ConfigureAwait(false);

		return position.TotalSeconds;
	}

	private static async Task<bool> HasPictureAsync(string elementName)
	{
		var hasPicture = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			hasPicture = PlayerOf(elementName).IsVideo).ConfigureAwait(false);

		return hasPicture;
	}

	private static async Task<bool> VideoSurfaceIsShowingAsync(string elementName)
	{
		var showing = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var presenter = VisualTreeSearch.FindDescendantNamed(ElementOf(elementName), PresenterPart)
				as MediaPlayerPresenter ?? throw MissingPart(elementName, PresenterPart);

			// The surface the add-in paints frames on is the presenter's child, and it is the
			// add-in's own type - which this project cannot name, because it is internal to the
			// add-in. Its visibility is all a requirement is about.
			showing = presenter.Visibility == Visibility.Visible
				&& presenter.Child is { Visibility: Visibility.Visible };
		}).ConfigureAwait(false);

		return showing;
	}

	private static Task<bool> WaitForAsync(Func<bool> probe, int milliseconds) =>
		Poll.UntilAsync(
			async () =>
			{
				await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
				return probe();
			},
			TimeSpan.FromMilliseconds(milliseconds),
			WaitInterval);

	private static InvalidOperationException MissingPart(string elementName, string partName) =>
		new($"\"{elementName}\" has no template part named \"{partName}\", so there is nothing to look at. "
			+ "The element's template is applied the first time it is laid out.");

	private static IMediaPlaybackSource? ToSource(string value) =>
		string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase)
			? null
			: MediaSource.CreateFromUri(MediaFixtures.UriOf(value));

	private static ImageSource? ToPoster(string value)
	{
		if (string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var poster = new BitmapImage();
		using (var stream = MediaFixtures.OpenPosterPng(Colors.Parse(value)))
		{
			poster.SetSource(stream);
		}

		return poster;
	}

	private static bool ToBool(string value) => bool.Parse(GherkinValue.Unquote(value));
}
