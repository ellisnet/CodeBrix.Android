using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.AudioPlayer.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Reqnroll;
using SilverAssertions;
using Xunit;
using AudioPlayerElement = CodeBrix.Platform.UI.AudioPlayer.Skia.AudioPlayer;
using AudioPlayerFailedEventArgs = CodeBrix.Platform.UI.AudioPlayer.Skia.AudioPlayerFailedEventArgs;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
using MidiInstrumentKind = CodeBrix.Platform.UI.AudioPlayer.Skia.MidiInstrumentKind;
using MidiPlayerElement = CodeBrix.Platform.UI.AudioPlayer.Skia.MidiPlayer;

namespace CodeBrix.Platform.UI.AddIn.AudioPlayer.UIReqs.Steps;

/// <summary>
/// The AudioPlayer group's vocabulary: the two players, the transport a person drives them with,
/// and the bound controls that are the only thing about them a person can SEE.
/// <para>
/// Both elements are non-visual - they render nothing and take no space - so no scenario in this
/// group ever asks for "the region of" a player. Every pixel claim lands on a Slider or a
/// ProgressRing bound to one of the player's properties, which is exactly the add-in's headline
/// claim: one piece of markup, either player, no converter.
/// </para>
/// <para>
/// Everything else these scenarios say - showing a layout, capturing a frame, dragging a Slider's
/// thumb, what a region looks like, what an event count is - is the core harness's own vocabulary,
/// reached through this project's reqnroll.json binding assemblies.
/// </para>
/// </summary>
[Binding]
public sealed class AudioPlayerSteps
{
	/// <summary>The name a feature file builds an audio player with.</summary>
	public const string AudioPlayerKind = "AudioPlayer";

	/// <summary>The name a feature file builds a MIDI player with.</summary>
	public const string MidiPlayerKind = "MidiPlayer";

	/// <summary>The core harness's name for the range control a scrubber is built from.</summary>
	public const string SliderKind = "Slider";

	/// <summary>The core harness's name for the control a loading indicator is built from.</summary>
	public const string ProgressRingKind = "ProgressRing";

	/// <summary>The event name recorded when a player reaches the end of its file.</summary>
	public const string PlaybackEndedEvent = "PlaybackEnded";

	/// <summary>The event name recorded when a source or an instrument fails to load or play.</summary>
	public const string MediaFailedEvent = "MediaFailed";

	/// <summary>The event name recorded when a MIDI player's instrument and sequence are ready.</summary>
	public const string MediaOpenedEvent = "MediaOpened";

	/// <summary>
	/// The event name recorded the moment a bound ProgressRing turns itself on. A background load
	/// that takes a couple of milliseconds cannot be caught in a frame - a frame takes longer than
	/// that - so what is recorded is the moment the BINDING drove the control, which is the thing
	/// the requirement is actually about.
	/// </summary>
	public const string SpinnerShownEvent = "SpinnerShown";

	/// <summary>The event name recorded the moment a bound ProgressRing turns itself off again.</summary>
	public const string SpinnerHiddenEvent = "SpinnerHidden";

	/// <summary>
	/// The event name recorded the moment a MIDI player's IsLoading turns true. A background load
	/// can finish before a step on the test thread has looked at the property, so the fact that
	/// loading was ANNOUNCED is recorded as it happens rather than read afterwards.
	/// </summary>
	public const string LoadingStartedEvent = "LoadingStarted";

	/// <summary>
	/// The prerequisite name both feature files declare with a <c>@needs-audioplayer</c> tag: the
	/// add-in assembly itself.
	/// </summary>
	public const string AudioPlayerPrerequisite = "audioplayer";

	/// <summary>
	/// The prerequisite name the scenarios that actually play declare with a
	/// <c>@needs-audio-device</c> tag. Playing opens the machine's shared audio output, and a
	/// machine with no sound server has no output to open: it reports skipped scenarios with the
	/// player's own message rather than a suite of failures about a scrubber that never moved.
	/// </summary>
	public const string AudioDevicePrerequisite = "audio-device";

	/// <summary>How wide a bound scrubber is built, so that a second of audio is worth real pixels.</summary>
	public const int ScrubberWidth = 800;

	/// <summary>How wide and tall a bound loading indicator is built.</summary>
	public const int SpinnerSize = 60;

	/// <summary>
	/// How often a playing player refreshes its position. The default is 150 ms; these scenarios
	/// ask for 50 ms so that a few hundred milliseconds of playback is several refreshes and not
	/// one, which is what lets a position band be narrow enough to mean something.
	/// </summary>
	public static readonly TimeSpan PositionUpdateInterval = TimeSpan.FromMilliseconds(50);

	/// <summary>How long a poll leaves the player alone between two looks at its position.</summary>
	public static readonly TimeSpan PositionPollInterval = TimeSpan.FromMilliseconds(25);

	/// <summary>How close a reported duration has to be to the length a scenario names.</summary>
	public const double DurationTolerance = 0.1;

	private static readonly object RegistrationLock = new();

	// What a player said when it last failed. An event count says that something went wrong;
	// the player's own sentence says what, and it is the only thing that makes a failed load
	// diagnosable from a test report.
	private static readonly Dictionary<string, string> LastFailureByElement = new(StringComparer.Ordinal);

	private static bool _registered;

	// ------------------------------------------------------------- registration

	/// <summary>
	/// Teaches the element factory this add-in's two nouns, before the first scenario.
	/// <para>
	/// Both players are built turned all the way down and refreshing quickly, because that is what
	/// every scenario needs and none of them is about either setting. Their events are recorded as
	/// they are built, since the element factory gives an element its name only after the factory
	/// has returned it - so the name is read when the event fires, not when it is subscribed.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_AudioPlayer_vocabulary()
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
				Prerequisite.Missing(AudioPlayerPrerequisite,
					$"the AudioPlayer add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	/// <summary>
	/// Looks for an audio output device, once, by doing exactly what a scenario does: loading a
	/// silent file into a player turned down to nothing and asking it to play.
	/// <para>
	/// The add-in has no null-device option - playing opens the machine's process-wide shared audio
	/// output - and a load or play failure never throws, it raises MediaFailed. So the probe is the
	/// event and the flag, not an exception; a machine with no sound server records the player's
	/// own message and every scenario tagged <c>@needs-audio-device</c> is skipped with it.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once the probe has finished and released the device.</returns>
	[BeforeTestRun(Order = 11)]
	public static async Task Look_for_an_audio_output_device()
	{
		if (Prerequisite.IsMissing(AudioPlayerPrerequisite, out _))
		{
			return;
		}

		var fixture = AudioFixtures.Resolve(AudioFixtures.ShortWave);
		if (!File.Exists(fixture))
		{
			Prerequisite.Missing(AudioDevicePrerequisite,
				$"the silent fixture \"{AudioFixtures.ShortWave}\" is not beside the tests ({AudioFixtures.Describe()})");
			return;
		}

		var played = false;
		var message = string.Empty;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var probe = new AudioPlayerElement { Volume = 0 };
			probe.MediaFailed += (_, failure) => message = failure.Message;

			try
			{
				probe.Source = fixture;
				probe.Play();
				played = probe.IsPlaying;
			}
			catch (Exception failure)
			{
				message = $"{failure.GetType().Name}: {failure.Message}";
			}
			finally
			{
				probe.Stop();
				probe.Source = "";
			}
		}).ConfigureAwait(false);

		if (!played)
		{
			Prerequisite.Missing(AudioDevicePrerequisite,
				message.Length == 0
					? "the shared audio output could not be opened"
					: $"the shared audio output could not be opened ({message})");
		}
	}

	/// <summary>
	/// Stops and unloads every player the scenario built, before the panel is emptied.
	/// <para>
	/// The harness's own reset empties the root; it disposes nothing. A player left loaded keeps
	/// the engine's decode work and the machine's shared audio output alive into the next scenario,
	/// which is how one scenario's playback ends up being counted by another's.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once every player is stopped and unloaded.</returns>
	[AfterScenario(Order = 0)]
	public static async Task Stop_and_unload_every_player()
	{
		if (!TestTargetFixture.IsLaunched)
		{
			return;
		}

		var names = ElementRegistry.Names;

		lock (RegistrationLock)
		{
			LastFailureByElement.Clear();
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			foreach (var name in names)
			{
				if (!ElementRegistry.TryResolve(name, out var element))
				{
					continue;
				}

				switch (element)
				{
					case AudioPlayerElement audio:
						audio.Stop();
						audio.Source = "";
						break;
					case MidiPlayerElement midi:
						midi.Stop();
						midi.Source = "";
						midi.Instrument = "";
						break;
					default:
						break;
				}
			}
		}).ConfigureAwait(false);
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(AudioPlayerKind, BuildAudioPlayer);
		ElementFactory.RegisterKind(MidiPlayerKind, BuildMidiPlayer);

		// Source and Instrument are typed rather than general: "Source" is a universal property
		// word (an Image has one too), and a feature file names a fixture rather than a path, so
		// the setter is this group's own on this group's own elements and nothing else's anywhere.
		ElementFactory.RegisterProperty<AudioPlayerElement>("Source",
			(player, value) => player.Source = AudioFixtures.Resolve(value));
		ElementFactory.RegisterProperty<MidiPlayerElement>("Source",
			(player, value) => player.Source = AudioFixtures.Resolve(value));
		ElementFactory.RegisterProperty<MidiPlayerElement>("Instrument",
			(player, value) => player.Instrument = AudioFixtures.Resolve(value));

		ElementFactory.RegisterProperty<AudioPlayerElement>("IsLooping",
			(player, value) => player.IsLooping = ReadBoolean(value));

		// The MIDI sequence is written on first use rather than committed as a binary nobody can
		// read; doing it here means the first scenario does not pay for it.
		AudioFixtures.MidiFilePath();
	}

	private static FrameworkElement BuildAudioPlayer()
	{
		var player = new AudioPlayerElement
		{
			Volume = 0,
			PositionUpdateInterval = PositionUpdateInterval,
		};

		player.PlaybackEnded += (sender, _) => Record(sender, PlaybackEndedEvent);
		player.MediaFailed += (sender, failure) => RecordFailure(sender, failure);
		return player;
	}

	private static FrameworkElement BuildMidiPlayer()
	{
		var player = new MidiPlayerElement
		{
			Volume = 0,
			PositionUpdateInterval = PositionUpdateInterval,
		};

		player.MediaOpened += (sender, _) => Record(sender, MediaOpenedEvent);
		player.PlaybackEnded += (sender, _) => Record(sender, PlaybackEndedEvent);
		player.MediaFailed += (sender, failure) => RecordFailure(sender, failure);
		player.RegisterPropertyChangedCallback(MidiPlayerElement.IsLoadingProperty, (element, _) =>
		{
			if (element is MidiPlayerElement loading && loading.IsLoading)
			{
				Record(loading, LoadingStartedEvent);
			}
		});

		return player;
	}

	// The name is read when the event fires rather than when it is subscribed, because the element
	// factory gives an element its name only after the factory has returned it.
	private static void Record(object? sender, string eventName)
	{
		if (sender is FrameworkElement element && element.Name.Length > 0)
		{
			EventRecorder.Record(element.Name, eventName);
		}
	}

	// A failure is counted like any other event AND remembered in the player's own words, because
	// "MediaOpened never came" is not a diagnosis and "the instrument could not be read, because
	// ..." is.
	private static void RecordFailure(object? sender, AudioPlayerFailedEventArgs failure)
	{
		if (sender is not FrameworkElement element || element.Name.Length == 0)
		{
			return;
		}

		EventRecorder.Record(element.Name, MediaFailedEvent);

		// The player's sentence says WHAT failed; the exception behind it says why, and a report
		// that carries only the first of those sends a person back to the debugger.
		var message = string.Create(CultureInfo.InvariantCulture,
			$"{failure.Message} [{failure.Error.GetType().Name}: {failure.Error.Message}]");

		lock (RegistrationLock)
		{
			LastFailureByElement[element.Name] = message;
		}
	}

	/// <summary>What a player said the last time it failed, or an empty string if it never has.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>The message the player reported.</returns>
	public static string LastFailureOf(string name)
	{
		lock (RegistrationLock)
		{
			return LastFailureByElement.TryGetValue(name, out var message) ? message : string.Empty;
		}
	}

	// --------------------------------------------------------------- the rig

	/// <summary>
	/// Builds a Slider that follows a player and drives it: its Maximum takes the file's length,
	/// its Value two-way binds to the timecode. That is the whole of the markup a real page uses -
	/// no converter, no code - and it is what makes a player a person can see and use.
	/// </summary>
	/// <param name="sliderName">The name the scenario refers to the Slider by.</param>
	/// <param name="playerName">The Gherkin name of the player it follows.</param>
	/// <returns>A task that completes once the Slider is in the tree beside the player.</returns>
	[Given("a Slider named {string} is bound to {string}")]
	[When("a Slider named {string} is bound to {string}")]
	public async Task Given_a_Slider_named_is_bound_to(string sliderName, string playerName)
	{
		var slider = await ElementFactory.CreateAsync(SliderKind, sliderName,
			new[] { new KeyValuePair<string, string>("Width", Number(ScrubberWidth)) }).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var player = ElementRegistry.Resolve(playerName);
			slider.SetBinding(RangeBase.MaximumProperty, new Binding
			{
				Source = player,
				Path = new PropertyPath("DurationSeconds"),
			});
			slider.SetBinding(RangeBase.ValueProperty, new Binding
			{
				Source = player,
				Path = new PropertyPath("PositionSeconds"),
				Mode = BindingMode.TwoWay,
			});
		}).ConfigureAwait(false);

		await AddBesideAsync(playerName, slider).ConfigureAwait(false);
	}

	/// <summary>
	/// Builds a ProgressRing that spins while a MIDI player is loading its instrument, which is the
	/// one thing a person sees about a load that happens in the background.
	/// </summary>
	/// <param name="ringName">The name the scenario refers to the ProgressRing by.</param>
	/// <param name="playerName">The Gherkin name of the player it follows.</param>
	/// <returns>A task that completes once the ProgressRing is in the tree beside the player.</returns>
	[Given("a ProgressRing named {string} is bound to the IsLoading of {string}")]
	[When("a ProgressRing named {string} is bound to the IsLoading of {string}")]
	public async Task Given_a_ProgressRing_named_is_bound_to_the_IsLoading_of(string ringName, string playerName)
	{
		var ring = await ElementFactory.CreateAsync(ProgressRingKind, ringName, new[]
		{
			new KeyValuePair<string, string>("Width", Number(SpinnerSize)),
			new KeyValuePair<string, string>("Height", Number(SpinnerSize)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var player = ElementRegistry.Resolve(playerName);
			ring.SetBinding(ProgressRing.IsActiveProperty, new Binding
			{
				Source = player,
				Path = new PropertyPath("IsLoading"),
			});

			// What the binding did to the control is recorded as it happens. An instrument that
			// loads in a couple of milliseconds is gone again before any frame can be taken, so a
			// scenario that waited for a frame showing the spinner would be waiting on a race; this
			// is the same requirement stated about the thing that is not a race.
			ring.RegisterPropertyChangedCallback(ProgressRing.IsActiveProperty, (element, _) =>
			{
				if (element is ProgressRing spinner)
				{
					Record(spinner, spinner.IsActive ? SpinnerShownEvent : SpinnerHiddenEvent);
				}
			});
		}).ConfigureAwait(false);

		await AddBesideAsync(playerName, ring).ConfigureAwait(false);
	}

	// ------------------------------------------------------------- transport

	/// <summary>Waits for the bound scrubber to match the player's position across two rendered frames.</summary>
	/// <param name="sliderName">The name of the bound Slider.</param>
	/// <param name="playerName">The name of the player it follows.</param>
	/// <returns>A task that completes when the binding and layout have caught up.</returns>
	[When("the Slider {string} has caught up with {string}")]
	public async Task When_the_Slider_has_caught_up_with(string sliderName, string playerName)
	{
		var matchingFrames = 0;
		var caughtUp = await Poll.UntilAsync(async () =>
		{
			await TestTargetFixture.RunOnUIThreadAsync(() =>
				ElementRegistry.Resolve(sliderName).UpdateLayout()).ConfigureAwait(false);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
			await TestTargetFixture.WaitForRenderAsync().ConfigureAwait(false);
			var matches = false;
			await TestTargetFixture.RunOnUIThreadAsync(() =>
			{
				var slider = (Slider)ElementRegistry.Resolve(sliderName);
				var player = ElementRegistry.Resolve(playerName);
				var position = player switch
				{
					AudioPlayerElement audio => audio.PositionSeconds,
					MidiPlayerElement midi => midi.PositionSeconds,
					_ => throw NotAPlayer(player, playerName),
				};
				matches = Math.Abs(slider.Value - position) < 0.001;
			}).ConfigureAwait(false);
			matchingFrames = matches ? matchingFrames + 1 : 0;
			return matchingFrames >= 2;
		}, TimeSpan.FromSeconds(2), PositionPollInterval).ConfigureAwait(false);
		caughtUp.Should().BeTrue("the scrubber must follow the player's position before its fill is captured");
	}

	/// <summary>Asks a player to play, which is what a Play button does.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the UI thread is idle again.</returns>
	[Given("{string} starts playing")]
	[When("{string} starts playing")]
	public async Task When_starts_playing(string name)
	{
		await OnPlayerAsync(name, audio => audio.Play(), midi => midi.Play()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asks a player to pause, which keeps the timecode where it is.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the UI thread is idle again.</returns>
	[When("{string} is paused")]
	public async Task When_is_paused(string name)
	{
		await OnPlayerAsync(name, audio => audio.Pause(), midi => midi.Pause()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asks a player to stop, which rewinds it to the beginning.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes once the UI thread is idle again.</returns>
	[When("{string} is stopped")]
	public async Task When_is_stopped(string name)
	{
		await OnPlayerAsync(name, audio => audio.Stop(), midi => midi.Stop()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asks a player to jump to a timecode. Seek is the immediate one: writing the bound position
	/// is debounced so that a whole scrub gesture lands one seek, and this is not that.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The timecode to jump to.</param>
	/// <returns>A task that completes once the UI thread is idle again.</returns>
	[Given("{string} seeks to {float} seconds")]
	[When("{string} seeks to {float} seconds")]
	public async Task When_seeks_to_seconds(string name, float seconds)
	{
		var position = TimeSpan.FromSeconds(seconds);
		await OnPlayerAsync(name, audio => audio.Seek(position), midi => midi.Seek(position)).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Waits, with a budget, until a player's timecode has passed a mark. The timecode is a signal -
	/// the player raises it on the UI thread every position interval - so this is a bounded poll on
	/// that signal and never a sleep, and it states the requirement with the number it got to when
	/// the budget runs out.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The mark the timecode has to pass.</param>
	/// <param name="milliseconds">How long it has to pass it.</param>
	/// <returns>A task that completes when the mark was passed, or the budget is gone.</returns>
	[Given("{string} plays past {float} seconds within {int} milliseconds")]
	[When("{string} plays past {float} seconds within {int} milliseconds")]
	public async Task When_plays_past_seconds_within_milliseconds(string name, float seconds, int milliseconds)
	{
		await Poll.UntilAsync(
			async () => await PositionOfAsync(name).ConfigureAwait(false) > seconds,
			TimeSpan.FromMilliseconds(milliseconds),
			PositionPollInterval).ConfigureAwait(false);

		(await PositionOfAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(seconds,
			"\"{0}\" had {1} milliseconds to play past {2} seconds", name, milliseconds, seconds);
	}

	/// <summary>
	/// Waits, with a budget, until a player's timecode has dropped back below a mark - which is
	/// what a player that has looped round to the beginning of its file has done.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The mark the timecode has to be back below.</param>
	/// <param name="milliseconds">How long it has to get back there.</param>
	/// <returns>A task that completes when the timecode dropped back, or the budget is gone.</returns>
	[When("{string} returns to before {float} seconds within {int} milliseconds")]
	public async Task When_returns_to_before_seconds_within_milliseconds(string name, float seconds, int milliseconds)
	{
		await Poll.UntilAsync(
			async () => await PositionOfAsync(name).ConfigureAwait(false) < seconds,
			TimeSpan.FromMilliseconds(milliseconds),
			PositionPollInterval).ConfigureAwait(false);

		(await PositionOfAsync(name).ConfigureAwait(false)).Should().BeLessThan(seconds,
			"\"{0}\" had {1} milliseconds to come back round to before {2} seconds", name, milliseconds, seconds);
	}

	// ------------------------------------------------ what the player reports

	/// <summary>Asserts that a player is playing.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} is playing")]
	public async Task Then_is_playing(string name) =>
		(await IsPlayingAsync(name).ConfigureAwait(false)).Should().BeTrue(
			"\"{0}\" was asserted to be playing; its timecode is {1}",
			name, Seconds(await PositionOfAsync(name).ConfigureAwait(false)));

	/// <summary>Asserts that a player is not playing.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} is not playing")]
	public async Task Then_is_not_playing(string name) =>
		(await IsPlayingAsync(name).ConfigureAwait(false)).Should().BeFalse(
			"\"{0}\" was asserted to be stopped; its timecode is {1}",
			name, Seconds(await PositionOfAsync(name).ConfigureAwait(false)));

	/// <summary>Asserts that a MIDI player has finished loading.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} is not loading")]
	public async Task Then_is_not_loading(string name) =>
		(await ReadAsync(name, element => MidiOf(element, "IsLoading").IsLoading).ConfigureAwait(false))
			.Should().BeFalse("\"{0}\" was asserted to have finished loading", name);

	/// <summary>Asserts the length a player read out of its file.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The length it must have read.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the DurationSeconds of {string} is about {float}")]
	public async Task Then_the_DurationSeconds_of_is_about(string name, float seconds) =>
		(await DurationOfAsync(name).ConfigureAwait(false)).Should().BeApproximately(seconds, DurationTolerance,
			"the length \"{0}\" read out of its file was asserted", name);

	/// <summary>Asserts that a player read a length longer than a number out of its file.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The number it must be longer than.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the DurationSeconds of {string} is more than {float}")]
	public async Task Then_the_DurationSeconds_of_is_more_than(string name, float seconds) =>
		(await DurationOfAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(seconds,
			"the length \"{0}\" read out of its file was asserted", name);

	/// <summary>Asserts that a player's timecode is past a mark. Timecodes are asserted as bands.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The mark it must be past.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the PositionSeconds of {string} is more than {float}")]
	public async Task Then_the_PositionSeconds_of_is_more_than(string name, float seconds) =>
		(await PositionOfAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(seconds,
			"the timecode of \"{0}\" was asserted", name);

	/// <summary>Asserts that a player's timecode is short of a mark.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="seconds">The mark it must be short of.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the PositionSeconds of {string} is less than {float}")]
	public async Task Then_the_PositionSeconds_of_is_less_than(string name, float seconds) =>
		(await PositionOfAsync(name).ConfigureAwait(false)).Should().BeLessThan(seconds,
			"the timecode of \"{0}\" was asserted", name);

	/// <summary>Asserts which instrument format a MIDI player turned out to be playing through.</summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="kind">None, SoundFont, Sfz or DecentSampler.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the InstrumentKind of {string} is {string}")]
	public async Task Then_the_InstrumentKind_of_is(string name, string kind) =>
		(await ReadAsync(name, element => MidiOf(element, "InstrumentKind").InstrumentKind).ConfigureAwait(false))
			.Should().Be(GherkinValue.ToEnum<MidiInstrumentKind>(kind),
				"the instrument format \"{0}\" loaded was asserted", name);

	/// <summary>
	/// Waits, with a budget, until a MIDI player reports more than a number of voices sounding, and
	/// then asserts it. Voices are counted on the same refresh as the timecode, so this is a bounded
	/// poll on a signal.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="voices">The number it must exceed.</param>
	/// <param name="milliseconds">How long it has to exceed it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ActiveVoiceCount of {string} is more than {int} within {int} milliseconds")]
	public async Task Then_the_ActiveVoiceCount_of_is_more_than_within(string name, int voices, int milliseconds)
	{
		var seen = 0;
		await Poll.UntilAsync(
			async () =>
			{
				seen = Math.Max(seen, await ReadAsync(name,
					element => MidiOf(element, "ActiveVoiceCount").ActiveVoiceCount).ConfigureAwait(false));
				return seen > voices;
			},
			TimeSpan.FromMilliseconds(milliseconds),
			PositionPollInterval).ConfigureAwait(false);

		seen.Should().BeGreaterThan(voices,
			"\"{0}\" had {1} milliseconds to report more than {2} voices sounding", name, milliseconds, voices);
	}

	/// <summary>
	/// Waits, with a budget, until a MIDI player's musical clock has moved on from where it was
	/// when this step started, and then asserts it. The beat position is the sequence's own clock
	/// rather than the file's timecode, so this is what says the music is actually being played.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	/// <param name="milliseconds">How long it has to move on.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the BeatPosition of {string} rises within {int} milliseconds")]
	public async Task Then_the_BeatPosition_of_rises_within(string name, int milliseconds)
	{
		var started = await BeatPositionOfAsync(name).ConfigureAwait(false);
		var reached = started;

		await Poll.UntilAsync(
			async () =>
			{
				reached = await BeatPositionOfAsync(name).ConfigureAwait(false);
				return reached > started;
			},
			TimeSpan.FromMilliseconds(milliseconds),
			PositionPollInterval).ConfigureAwait(false);

		reached.Should().BeGreaterThan(started,
			"the musical clock of \"{0}\" had {1} milliseconds to move on from {2}",
			name, milliseconds, Seconds(started));
	}

	// ------------------------------------------------------- what a failure says

	/// <summary>
	/// Asserts that a player that failed said something a person could act on. A failure that
	/// carries no words is not a report.
	/// </summary>
	/// <param name="name">The Gherkin name of the player.</param>
	[Then("the failure message of {string} is not empty")]
	public void Then_the_failure_message_of_is_not_empty(string name) =>
		LastFailureOf(name).Should().NotBeEmpty(
			"the message \"{0}\" reported with its MediaFailed was asserted", name);

	// ---------------------------------------------------------------- inner

	private static async Task AddToAsync(string layoutName, FrameworkElement child)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			PanelOf(ElementRegistry.Resolve(layoutName), layoutName).Children.Add(child);
			VirtualApplication.Instance.Root.UpdateLayout();
		}).ConfigureAwait(false);
	}

	private static async Task AddBesideAsync(string siblingName, FrameworkElement child)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var sibling = ElementRegistry.Resolve(siblingName);
			var parent = sibling.Parent as Panel
				?? throw new NotSupportedException(
					$"\"{siblingName}\" is not inside a layout, so there is nowhere to put the control "
					+ "that follows it. Put the player in a layout first.");

			parent.Children.Add(child);
			VirtualApplication.Instance.Root.UpdateLayout();
		}).ConfigureAwait(false);
	}

	private static Panel PanelOf(FrameworkElement element, string name) =>
		element as Panel
		?? throw new NotSupportedException(
			$"A {element.GetType().Name} named \"{name}\" cannot hold a player.");

	private static async Task OnPlayerAsync(string name, Action<AudioPlayerElement> onAudio,
		Action<MidiPlayerElement> onMidi)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			switch (ElementRegistry.Resolve(name))
			{
				case AudioPlayerElement audio:
					onAudio(audio);
					break;
				case MidiPlayerElement midi:
					onMidi(midi);
					break;
				case { } other:
					throw NotAPlayer(other, name);
			}
		}).ConfigureAwait(false);
	}

	private static async Task<T> ReadAsync<T>(string name, Func<FrameworkElement, T> read)
	{
		var value = default(T)!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			value = read(ElementRegistry.Resolve(name))).ConfigureAwait(false);

		return value;
	}

	private static Task<double> PositionOfAsync(string name) => ReadAsync(name, element => element switch
	{
		AudioPlayerElement audio => audio.PositionSeconds,
		MidiPlayerElement midi => midi.PositionSeconds,
		_ => throw NotAPlayer(element, element.Name),
	});

	private static Task<double> DurationOfAsync(string name) => ReadAsync(name, element => element switch
	{
		AudioPlayerElement audio => audio.DurationSeconds,
		MidiPlayerElement midi => midi.DurationSeconds,
		_ => throw NotAPlayer(element, element.Name),
	});

	private static Task<bool> IsPlayingAsync(string name) => ReadAsync(name, element => element switch
	{
		AudioPlayerElement audio => audio.IsPlaying,
		MidiPlayerElement midi => midi.IsPlaying,
		_ => throw NotAPlayer(element, element.Name),
	});

	private static Task<double> BeatPositionOfAsync(string name) =>
		ReadAsync(name, element => MidiOf(element, "BeatPosition").BeatPosition);

	private static MidiPlayerElement MidiOf(FrameworkElement element, string property) =>
		element as MidiPlayerElement
		?? throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property}: only a MidiPlayer has one."));

	private static NotSupportedException NotAPlayer(FrameworkElement element, string name) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{name}\" is neither an {AudioPlayerKind} nor a {MidiPlayerKind}."));

	private static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Seconds(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
