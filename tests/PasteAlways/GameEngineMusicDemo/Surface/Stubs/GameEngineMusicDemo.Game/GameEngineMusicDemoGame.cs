// STUB (paste always, compile only; runtime: D-O14 NotSupported - GameEngine): GameEngineMusicDemo.Game.GameEngineMusicDemoGame
// (CodeBrix.Samples GameEngineMusicDemo/src/libs/GameEngineMusicDemo.Game, the app's own library); it drives the
// CodeBrix.Platform.GameEngine package throughout, which an Android build cannot reference. Same namespace and type name;
// the public members GameEngineMusicDemo's MainPage and view model use (signatures as in the library).
using System;
using CodeBrix.Platform.GameEngine.Audio;
using CodeBrix.Platform.GameEngine.Host.Rendering;

namespace GameEngineMusicDemo.Game;

/// <summary>Stand-in for the music demonstration (compile-only).</summary>
public sealed class GameEngineMusicDemoGame
{
    /// <summary>Creates the demonstration over <paramref name="canvas"/>.</summary>
    public GameEngineMusicDemoGame(GameSurfaceCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
    }

    /// <summary>The three-layer adaptive stem set.</summary>
    public MusicStemSet Stems { get; } = new MusicStemSet();

    /// <summary>The stems export's stem set.</summary>
    public MusicStemSet SongStems { get; } = new MusicStemSet();

    /// <summary>The MIDI theme (SFZ).</summary>
    public MidiMusicTrack MidiTrack { get; } = new MidiMusicTrack();

    /// <summary>The MIDI theme (Decent Sampler).</summary>
    public MidiMusicTrack SamplerTrack { get; } = new MidiMusicTrack();

    /// <summary>Whether the engine is paused.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Starts the engine and loads the music.</summary>
    public void Start() { }

    /// <summary>Plays track A with a fade in.</summary>
    public void PlayTrackA() { }

    /// <summary>Crossfades to track B.</summary>
    public void CrossfadeToTrackB(MusicTransitionQuantize quantize) { }

    /// <summary>Crossfades back to track A.</summary>
    public void CrossfadeToTrackA(MusicTransitionQuantize quantize) { }

    /// <summary>Plays the adaptive stems.</summary>
    public void PlayStems() { }

    /// <summary>Plays the MIDI theme (SFZ).</summary>
    public void PlayMidi() { }

    /// <summary>Plays the MIDI theme (Decent Sampler).</summary>
    public void PlayDecentSamplerTheme() { }

    /// <summary>Plays the stems export.</summary>
    public void PlaySongStems() { }

    /// <summary>Fades one layer of the stems export.</summary>
    public void FadeSongStem(string stemName, float target) { }

    /// <summary>Plays the playlist.</summary>
    public void PlayPlaylist() { }

    /// <summary>Moves to the next playlist entry.</summary>
    public void NextInPlaylist() { }

    /// <summary>Stops the music with a fade out.</summary>
    public void StopMusic() { }

    /// <summary>Cancels a queued transition.</summary>
    public void CancelQueuedTransition() { }

    /// <summary>Plays a stinger that ducks the music.</summary>
    public void PlayStinger() { }

    /// <summary>Plays a dialogue line that ducks the music for its length.</summary>
    public void PlayDuckedDialogue() { }

    /// <summary>Holds a duck open.</summary>
    public void HoldDuck() { }

    /// <summary>Releases the held duck.</summary>
    public void ReleaseHeldDuck() { }

    /// <summary>Jumps to a MIDI marker.</summary>
    public bool JumpToMarker(string marker) => false;

    /// <summary>Pauses or resumes the engine.</summary>
    public void TogglePause() => IsPaused = !IsPaused;

    /// <summary>Stops the engine.</summary>
    public void Stop() { }
}
