#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBrix.Android.UI.AudioPlayer.Android;
using CodeBrix.Audio.Android;
using CodeBrix.Platform.UI.AddIn.AudioPlayer.UIReqs.Support;
using CodeBrix.Platform.UI.AudioPlayer.Skia;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reqnroll;
using SilverAssertions;
using AudioPlayerElement = CodeBrix.Platform.UI.AudioPlayer.Skia.AudioPlayer;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The AudioPlayer on Android" feature (AndroidFeatures/AndroidAudioPlayer), and of the
/// restated AndroidElements10B MidiPlayer scenario: the AudioPlayer add-in's Android platforms, ms-appx audio copied out of
/// the APK, sound effects, and the add-in's own MidiPlayer.
/// </summary>
[Binding]
public sealed class AudioPlayerAndroidSteps
{
    private static string _failure;
    private static bool _opened;
    private static bool _effectAccepted;

    /// <summary>Stops and unloads the players this group built (the copied group's own hook is scoped to its features).</summary>
    [AfterScenario]
    public static async Task Unload()
    {
        if (!TestTargetFixture.IsLaunched)
        {
            return;
        }

        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var name in ElementRegistry.Names)
            {
                if (!ElementRegistry.TryResolve(name, out var element))
                {
                    continue;
                }

                if (element is AudioPlayerElement audio)
                {
                    audio.Stop();
                    audio.Source = "";
                }
                else if (element is MidiPlayer midi)
                {
                    midi.Stop();
                    midi.Source = "";
                    midi.Instrument = "";
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>An AudioPlayer in a StackPanel, volume 0, its failures recorded.</summary>
    [Given("an AudioPlayer named {string} is on the panel, turned down to nothing")]
    public async Task Given_an_audio_player(string name)
    {
        ElementRegistry.Clear();
        _failure = null;
        StackPanel panel = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var player = new AudioPlayerElement { Volume = 0, PositionUpdateInterval = TimeSpan.FromMilliseconds(50) };
            player.MediaFailed += (_, e) => _failure = e.Message;
            ElementRegistry.Register(name, player);
            panel = new StackPanel { Width = 600, Height = 200 };
            panel.Children.Add(player);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(panel).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A MidiPlayer in a StackPanel, volume 0, its opening and failures recorded.</summary>
    [Given("a MidiPlayer named {string} is on the panel, turned down to nothing")]
    public async Task Given_a_midi_player(string name)
    {
        ElementRegistry.Clear();
        StackPanel panel = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var midi = new MidiPlayer { Volume = 0 };
            ElementRegistry.Register(name, midi);
            panel = new StackPanel { Width = 600, Height = 200 };
            panel.Children.Add(midi);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(panel).ConfigureAwait(false);
        await Watch(name).ConfigureAwait(false);
    }

    /// <summary>CodeBrixAndroidAudio.Initialize ran (the add-in's bootstrap).</summary>
    [Then("the Android audio backend is initialised")]
    public void Then_backend_initialised() =>
        CodeBrixAndroidAudio.IsInitialized.Should().BeTrue("the AudioPlayer add-in initialises CodeBrix.Audio.Android at start-up");

    /// <summary>The element's per-element platform hook is the Android add-in's.</summary>
    [Then("{string} plays through the Android add-in's AudioFilePlayer output")]
    public async Task Then_plays_through_android(string name)
    {
        var hook = await OnUIThreadAsync(() =>
            typeof(AudioPlayerElement).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(f => f.GetValue(ElementRegistry.Resolve(name)))
                .FirstOrDefault(v => v?.GetType().Name == nameof(AudioPlayerAndroidPlatform))?.GetType().FullName).ConfigureAwait(false);
        hook.Should().Be(typeof(AudioPlayerAndroidPlatform).FullName);
        AndroidPlatformBootstrap.IsRegistered.Should().BeTrue();
    }

    /// <summary>IAssetLocation's root is the AndroidPackagedAssets folder.</summary>
    [Then("the root of ms-appx audio is the Android asset copy folder")]
    public void Then_root_is_copy_folder()
    {
        var root = new AssetLocationAndroidPlatform().InstalledPath;
        root.Should().Be(AndroidPackagedAssets.RootDirectory());
        root.Should().StartWith(global::Android.App.Application.Context.FilesDir!.AbsolutePath);
    }

    /// <summary>Sets an AudioPlayer's Source.</summary>
    [When("the Source of the AudioPlayer {string} is {string}")]
    public async Task When_source(string name, string source)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((AudioPlayerElement)ElementRegistry.Resolve(name)).Source = source).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>The length and the failures.</summary>
    [Then("the AudioPlayer {string} has a length of about {int} seconds and reported no failure")]
    public async Task Then_length(string name, int seconds)
    {
        var duration = await OnUIThreadAsync(() => ((AudioPlayerElement)ElementRegistry.Resolve(name)).DurationSeconds).ConfigureAwait(false);
        _failure.Should().BeNull();
        duration.Should().BeApproximately(seconds, 0.1);
    }

    /// <summary>The APK asset exists under the copy folder.</summary>
    [Then("the asset {string} has been copied out of the APK")]
    public void Then_asset_copied(string asset) =>
        File.Exists(Path.Combine(AndroidPackagedAssets.RootDirectory(), asset)).Should().BeTrue("the asset is copied out on first use");

    /// <summary>The APK asset folder exists under the copy folder.</summary>
    [Then("the asset folder {string} has been copied out of the APK")]
    public void Then_folder_copied(string asset) =>
        Directory.Exists(Path.Combine(AndroidPackagedAssets.RootDirectory(), asset)).Should().BeTrue("an SFZ instrument's folder is copied out with it");

    /// <summary>Plays an AudioPlayer.</summary>
    [When("the AudioPlayer {string} plays")]
    public async Task When_plays(string name) =>
        await TestTargetFixture.RunOnUIThreadAsync(() => ((AudioPlayerElement)ElementRegistry.Resolve(name)).Play()).ConfigureAwait(false);

    /// <summary>The position passes a mark while playing.</summary>
    [Then("the AudioPlayer {string} plays past {float} seconds within {int} milliseconds")]
    public async Task Then_plays_past(string name, float seconds, int milliseconds)
    {
        var position = 0d;
        for (var waited = 0; waited < milliseconds && position <= seconds; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
            position = await OnUIThreadAsync(() => ((AudioPlayerElement)ElementRegistry.Resolve(name)).PositionSeconds).ConfigureAwait(false);
        }

        _failure.Should().BeNull();
        position.Should().BeGreaterThan(seconds, "the Android output is running the player");
    }

    /// <summary>SoundEffect (Core) over the Android output's voices.</summary>
    [When("a silent sound effect is played at volume 0")]
    public async Task When_sound_effect() =>
        await TestTargetFixture.RunOnUIThreadAsync(() => _effectAccepted = SoundEffect.Play(AudioFixtures.Resolve(AudioFixtures.ShortWave), 0.0)).ConfigureAwait(false);

    /// <summary>SoundEffect.Play returned true.</summary>
    [Then("the sound effect was accepted by the Android output")]
    public void Then_effect_accepted() => _effectAccepted.Should().BeTrue("SoundEffect decodes and plays through IAudioOutputPlatform");

    /// <summary>The element's type is this add-in's.</summary>
    [Then("{string} is a MidiPlayer of the Android AudioPlayer add-in")]
    public async Task Then_is_android_midi(string name)
    {
        var assembly = await OnUIThreadAsync(() => ElementRegistry.Resolve(name).GetType().Assembly.GetName().Name).ConfigureAwait(false);
        assembly.Should().Be("CodeBrix.Android.UI.AudioPlayer");
    }

    /// <summary>Gives a MidiPlayer an instrument and the copied group's generated MIDI sequence.</summary>
    [When("the MidiPlayer {string} is given the instrument {string} and the generated sequence")]
    public async Task When_midi_loads(string name, string instrument)
    {
        var sequence = AudioFixtures.MidiFilePath();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var midi = (MidiPlayer)ElementRegistry.Resolve(name);
            midi.Instrument = instrument;
            midi.Source = sequence;
        }).ConfigureAwait(false);
    }

    /// <summary>The background load finished with MediaOpened.</summary>
    [Then("the MidiPlayer {string} opens its media within {int} milliseconds with an SFZ instrument")]
    public async Task Then_midi_opened(string name, int milliseconds)
    {
        for (var waited = 0; waited < milliseconds && !_opened && _failure == null; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        _failure.Should().BeNull();
        _opened.Should().BeTrue("the MidiPlayer raises MediaOpened once its instrument and sequence are loaded");
        var kind = await OnUIThreadAsync(() => ((MidiPlayer)ElementRegistry.Resolve(name)).InstrumentKind).ConfigureAwait(false);
        kind.Should().Be(MidiInstrumentKind.Sfz);
    }

    /// <summary>Plays a MidiPlayer.</summary>
    [When("the MidiPlayer {string} plays")]
    public async Task When_midi_plays(string name) =>
        await TestTargetFixture.RunOnUIThreadAsync(() => ((MidiPlayer)ElementRegistry.Resolve(name)).Play()).ConfigureAwait(false);

    /// <summary>The synthesizer sounds voices through the Android output.</summary>
    [Then("the MidiPlayer {string} sounds voices within {int} milliseconds")]
    public async Task Then_midi_voices(string name, int milliseconds)
    {
        var voices = 0;
        for (var waited = 0; waited < milliseconds && voices == 0; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
            voices = await OnUIThreadAsync(() => ((MidiPlayer)ElementRegistry.Resolve(name)).ActiveVoiceCount).ConfigureAwait(false);
        }

        voices.Should().BeGreaterThan(0);
    }

    /// <summary>AP10-B (restated at AP7-C): the sample's MidiPlayer loads the copied group's silent SFZ and generated sequence.</summary>
    [When("{string} is given the silent SFZ instrument and the generated MIDI sequence")]
    public async Task When_sample_midi_loads(string name)
    {
        await Watch(name).ConfigureAwait(false);
        var instrument = AudioFixtures.Resolve("silent.sfz");
        var sequence = AudioFixtures.MidiFilePath();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var midi = (MidiPlayer)ElementRegistry.Resolve(name);
            midi.Volume = 0;
            midi.Instrument = instrument;
            midi.Source = sequence;
        }).ConfigureAwait(false);
    }

    /// <summary>AP10-B (restated at AP7-C): MediaOpened within a time.</summary>
    [Then("{string} opens its media within {int} milliseconds")]
    public async Task Then_sample_midi_opened(string name, int milliseconds)
    {
        for (var waited = 0; waited < milliseconds && !_opened && _failure == null; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        _failure.Should().BeNull();
        _opened.Should().BeTrue();
    }

    private static async Task Watch(string name)
    {
        _opened = false;
        _failure = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var midi = (MidiPlayer)ElementRegistry.Resolve(name);
            midi.MediaOpened += (_, _) => _opened = true;
            midi.MediaFailed += (_, e) => _failure = e.Message;
        }).ConfigureAwait(false);
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
