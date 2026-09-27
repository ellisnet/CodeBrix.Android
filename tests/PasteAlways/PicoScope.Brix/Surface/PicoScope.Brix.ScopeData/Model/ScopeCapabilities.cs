using System;
using System.Collections.Generic;
using System.Linq;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// What a particular scope can actually do.
/// </summary>
/// <remarks>
/// <para>
/// The ps2000 driver is shared by every 2000-series model, and its enumerations
/// list the union of what any of them supports. Asking for a capability the
/// attached unit lacks simply fails, usually with an unhelpful zero return. This
/// type records what an individual unit really supports, so callers can adapt --
/// build a range selector from <see cref="SupportedRanges"/> rather than from
/// every value of <see cref="VoltageRange"/>, for instance.
/// </para>
/// <para>
/// An implementation is expected to populate this by interrogating the device
/// when it opens, not by matching on the model name. That way the code stays
/// correct if a different 2000-series scope is plugged in.
/// </para>
/// </remarks>
public sealed class ScopeCapabilities
{
    /// <summary>The model name the device reports, for example "2204A".</summary>
    public string Variant { get; init; } = string.Empty;

    /// <summary>The input channels the device actually has.</summary>
    public IReadOnlyList<ChannelId> SupportedChannels { get; init; } = Array.Empty<ChannelId>();

    /// <summary>The input ranges the device actually accepts.</summary>
    public IReadOnlyList<VoltageRange> SupportedRanges { get; init; } = Array.Empty<VoltageRange>();

    /// <summary>The channels the device will accept as a trigger source.</summary>
    public IReadOnlyList<ChannelId> SupportedTriggerSources { get; init; } = Array.Empty<ChannelId>();

    /// <summary>The waveforms the built-in signal generator can produce.</summary>
    public IReadOnlyList<WaveType> SupportedWaveTypes { get; init; } = Array.Empty<WaveType>();

    /// <summary>
    /// The lowest valid timebase index when only one channel is enabled. On a
    /// PicoScope 2204A this is 0, giving a 10 ns interval.
    /// </summary>
    public int MinTimebaseSingleChannel { get; init; }

    /// <summary>
    /// The lowest valid timebase index when more than one channel is enabled.
    /// On a PicoScope 2204A this is 1, giving a 20 ns interval -- the sampling
    /// hardware is shared between channels.
    /// </summary>
    public int MinTimebaseMultiChannel { get; init; }

    /// <summary>The highest valid timebase index. On a PicoScope 2204A this is 23.</summary>
    public int MaxTimebase { get; init; }

    /// <summary>The block sample-buffer depth with one channel enabled.</summary>
    public int MaxSamplesSingleChannel { get; init; }

    /// <summary>The block sample-buffer depth with more than one channel enabled.</summary>
    public int MaxSamplesMultiChannel { get; init; }

    /// <summary>
    /// The largest oversampling factor the device accepts. The driver header
    /// advertises 256 for the series; a PicoScope 2204A accepts only 4.
    /// </summary>
    public int MaxOversample { get; init; } = 1;

    /// <summary>Whether the device supports equivalent-time sampling.</summary>
    public bool SupportsEts { get; init; }

    /// <summary>
    /// The effective sampling interval ETS achieves, in picoseconds, or zero
    /// when ETS is unsupported.
    /// </summary>
    public int EtsEffectiveIntervalPicoseconds { get; init; }

    /// <summary>The maximum number of captures ETS will interleave.</summary>
    public int MaxEtsCycles { get; init; }

    /// <summary>The maximum number of sub-sample offsets ETS will step through.</summary>
    public int MaxEtsInterleave { get; init; }

    /// <summary>Whether the device supports the original polled streaming mode.</summary>
    public bool SupportsCompatibleStreaming { get; init; }

    /// <summary>Whether the device supports callback-driven fast streaming.</summary>
    public bool SupportsFastStreaming { get; init; }

    /// <summary>Whether the device has a built-in signal generator.</summary>
    public bool SupportsSignalGenerator { get; init; }

    /// <summary>Whether the device has an arbitrary waveform generator.</summary>
    public bool SupportsArbitraryWaveform { get; init; }

    /// <summary>The highest frequency the signal generator will accept, in hertz.</summary>
    public double MaxSignalGeneratorFrequencyHz { get; init; }

    /// <summary>
    /// The largest peak-to-peak amplitude the signal generator will accept, in
    /// microvolts. A PicoScope 2204A accepts up to 4 V peak-to-peak and rejects
    /// more.
    /// </summary>
    public uint MaxSignalGeneratorAmplitudeMicrovolts { get; init; }

    /// <summary>The largest arbitrary waveform buffer the device accepts, in samples.</summary>
    public int MaxArbitraryWaveformSize { get; init; }

    /// <summary>Whether the device supports the advanced trigger calls.</summary>
    public bool SupportsAdvancedTrigger { get; init; }

    /// <summary>Whether the device has an LED that can be flashed for identification.</summary>
    public bool SupportsFlashLed { get; init; }

    /// <summary>
    /// Whether the device has a front-panel button whose state can be read.
    /// Present on the handheld 2104 and 2105; absent on a 2204A.
    /// </summary>
    public bool SupportsButton { get; init; }

    /// <summary>The number of input channels.</summary>
    public int ChannelCount => SupportedChannels.Count;

    /// <summary>
    /// Returns the lowest timebase index that is valid for the given number of
    /// enabled channels.
    /// </summary>
    /// <param name="enabledChannelCount">How many channels are enabled.</param>
    /// <returns>The lowest usable timebase index.</returns>
    public int MinTimebaseFor(int enabledChannelCount)
        => enabledChannelCount <= 1 ? MinTimebaseSingleChannel : MinTimebaseMultiChannel;

    /// <summary>
    /// Returns the block sample-buffer depth for the given number of enabled
    /// channels.
    /// </summary>
    /// <param name="enabledChannelCount">How many channels are enabled.</param>
    /// <returns>The maximum block size, in samples per channel.</returns>
    public int MaxSamplesFor(int enabledChannelCount)
        => enabledChannelCount <= 1 ? MaxSamplesSingleChannel : MaxSamplesMultiChannel;

    /// <summary>
    /// Whether the given range is accepted by this device.
    /// </summary>
    /// <param name="range">The range to test.</param>
    /// <returns>True when the device accepts it.</returns>
    public bool Supports(VoltageRange range) => SupportedRanges.Contains(range);

    /// <summary>
    /// Whether the given channel exists on this device.
    /// </summary>
    /// <param name="channel">The channel to test.</param>
    /// <returns>True when the device has it.</returns>
    public bool Supports(ChannelId channel) => SupportedChannels.Contains(channel);

    /// <summary>
    /// The capability set of a PicoScope 2204A, as measured from a real device.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="Simulation.SimulatedScopeDataDevice"/> so that the simulator
    /// imposes the same limits the hardware does -- code developed against the
    /// simulator then behaves the same way against the real scope. Every value
    /// here was verified by probing an actual unit, not read off a datasheet.
    /// </remarks>
    public static ScopeCapabilities PicoScope2204A { get; } = new ScopeCapabilities
    {
        Variant = "2204A",
        SupportedChannels = new[] { ChannelId.ChannelA, ChannelId.ChannelB },
        SupportedRanges = new[]
        {
            VoltageRange.Range50mV, VoltageRange.Range100mV, VoltageRange.Range200mV,
            VoltageRange.Range500mV, VoltageRange.Range1V, VoltageRange.Range2V,
            VoltageRange.Range5V, VoltageRange.Range10V, VoltageRange.Range20V
        },
        SupportedTriggerSources = new[] { ChannelId.ChannelA, ChannelId.ChannelB, ChannelId.None },
        SupportedWaveTypes = new[]
        {
            WaveType.Sine, WaveType.Square, WaveType.Triangle, WaveType.RampUp,
            WaveType.RampDown, WaveType.DcVoltage, WaveType.Gaussian, WaveType.Sinc,
            WaveType.HalfSine
        },
        MinTimebaseSingleChannel = 0,
        MinTimebaseMultiChannel = 1,
        MaxTimebase = 23,
        MaxSamplesSingleChannel = 8064,
        MaxSamplesMultiChannel = 3968,
        MaxOversample = 4,
        SupportsEts = true,
        EtsEffectiveIntervalPicoseconds = 500,
        MaxEtsCycles = 250,
        MaxEtsInterleave = 40,
        SupportsCompatibleStreaming = true,
        SupportsFastStreaming = true,
        SupportsSignalGenerator = true,
        SupportsArbitraryWaveform = true,
        MaxSignalGeneratorFrequencyHz = 100000.0,
        MaxSignalGeneratorAmplitudeMicrovolts = 4_000_000,
        MaxArbitraryWaveformSize = 4096,
        SupportsAdvancedTrigger = true,
        SupportsFlashLed = true,
        SupportsButton = false
    };
}
