// STUB (paste always, compile only; runtime: D-O14 NotSupported - GameEngine): CodeBrix.Platform.GameEngine.Audio.AudioMixer,
// MusicTransitionQuantize, MusicStemSet, MusicStem and MidiMusicTrack (package CodeBrix.Platform.GameEngine.MitLicenseForever,
// assembly CodeBrix.Platform.GameEngine); the package depends on CodeBrix.Platform repo packages, so an Android build cannot
// reference it. Same namespace and type names; only the members GameEngineMusicDemo's MainPage uses (names and types from
// the package's public API).
using System;
using System.Collections.Generic;

namespace CodeBrix.Platform.GameEngine.Audio;

/// <summary>Stand-in for the engine's volume buses (compile-only).</summary>
public static class AudioMixer
{
    /// <summary>The master volume, 0.0 to 1.0.</summary>
    public static float MasterVolume { get; set; } = 1f;

    /// <summary>The music bus volume, 0.0 to 1.0.</summary>
    public static float MusicVolume { get; set; } = 1f;

    /// <summary>The sound-effects bus volume, 0.0 to 1.0.</summary>
    public static float SfxVolume { get; set; } = 1f;
}

/// <summary>Stand-in for where a music transition may start (compile-only).</summary>
public enum MusicTransitionQuantize
{
    /// <summary>Start at once.</summary>
    Immediate,

    /// <summary>Start on the next beat.</summary>
    Beat,

    /// <summary>Start on the next bar.</summary>
    Bar,
}

/// <summary>Stand-in for one layer of a stem set (compile-only).</summary>
public sealed class MusicStem
{
    /// <summary>The layer's name.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>The layer's level within the set, 0.0 to 1.0.</summary>
    public float Gain { get; set; } = 1f;

    /// <summary>Fades the layer's level to <paramref name="target"/> over <paramref name="duration"/>.</summary>
    public void FadeTo(float target, TimeSpan duration) => Gain = target;
}

/// <summary>Stand-in for a sample-locked set of music layers (compile-only).</summary>
public sealed class MusicStemSet
{
    private readonly List<MusicStem> stems = new List<MusicStem>();

    /// <summary>The number of layers.</summary>
    public int Count => stems.Count;

    /// <summary>The layer at <paramref name="index"/>.</summary>
    public MusicStem this[int index] => stems[index];
}

/// <summary>Stand-in for a MIDI music track (compile-only).</summary>
public sealed class MidiMusicTrack
{
    /// <summary>The playback tempo multiplier (no pitch change).</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Fades one MIDI channel's level to <paramref name="target"/> over <paramref name="duration"/>.</summary>
    public void FadeLayerTo(int channel, float target, TimeSpan duration) { }
}
