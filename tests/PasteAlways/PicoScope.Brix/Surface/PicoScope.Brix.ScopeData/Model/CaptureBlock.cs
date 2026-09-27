using System;
using System.Collections.Generic;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// The samples captured from one channel during a single acquisition, together
/// with the range they were captured on so they can be scaled to volts.
/// </summary>
/// <param name="Channel">The channel the samples came from.</param>
/// <param name="Range">The range the channel was set to during the capture.</param>
/// <param name="Samples">
/// The raw ADC counts, on the -32767 to +32767 scale described by
/// <see cref="ScopeConstants.MaxAdcValue"/>.
/// </param>
public readonly record struct ChannelSamples(ChannelId Channel, VoltageRange Range, short[] Samples)
{
    /// <summary>The number of samples captured.</summary>
    public int Count => Samples == null ? 0 : Samples.Length;

    /// <summary>
    /// Returns the sample at <paramref name="index"/> converted to volts.
    /// </summary>
    /// <param name="index">The zero-based sample index.</param>
    /// <returns>The sample in volts.</returns>
    public double VoltsAt(int index) => Range.AdcToVolts(Samples[index]);

    /// <summary>
    /// Returns the sample at <paramref name="index"/> converted to millivolts.
    /// </summary>
    /// <param name="index">The zero-based sample index.</param>
    /// <returns>The sample in millivolts.</returns>
    public double MillivoltsAt(int index) => Range.AdcToMillivolts(Samples[index]);

    /// <summary>
    /// Whether the sample at <paramref name="index"/> is the driver's
    /// "no data" sentinel rather than a real measurement.
    /// </summary>
    /// <param name="index">The zero-based sample index.</param>
    /// <returns>True when the position holds <see cref="ScopeConstants.LostData"/>.</returns>
    public bool IsLostData(int index) => Samples[index] == ScopeConstants.LostData;
}

/// <summary>
/// One complete acquisition: the samples from every enabled channel, plus the
/// timing information needed to place them on a time axis.
/// </summary>
public sealed class CaptureBlock
{
    /// <summary>
    /// The per-channel samples, keyed by channel.
    /// </summary>
    public IReadOnlyDictionary<ChannelId, ChannelSamples> Channels { get; init; }
        = new Dictionary<ChannelId, ChannelSamples>();

    /// <summary>
    /// The interval between consecutive samples, in nanoseconds.
    /// </summary>
    public double IntervalNanoseconds { get; init; }

    /// <summary>The number of samples captured on each channel.</summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Per-sample timestamps, when the capture requested them, otherwise null.
    /// The units are given by <see cref="TimestampUnits"/>.
    /// </summary>
    public int[] Timestamps { get; init; }

    /// <summary>The units the values in <see cref="Timestamps"/> are expressed in.</summary>
    public TimeUnits TimestampUnits { get; init; } = TimeUnits.Nanoseconds;

    /// <summary>
    /// The driver's overflow flags: a bit per channel, set when that channel's
    /// input exceeded the selected range during the capture.
    /// </summary>
    public short OverflowFlags { get; init; }

    /// <summary>
    /// When the capture was taken, in UTC.
    /// </summary>
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the given channel's input exceeded its range during the capture,
    /// which means its samples are clipped and its readings untrustworthy.
    /// </summary>
    /// <param name="channel">The channel to test.</param>
    /// <returns>True when that channel overflowed.</returns>
    public bool DidOverflow(ChannelId channel) => (OverflowFlags & (1 << (int)channel)) != 0;

    /// <summary>
    /// Returns the elapsed time of the sample at <paramref name="index"/>, in
    /// seconds from the start of the block.
    /// </summary>
    /// <param name="index">The zero-based sample index.</param>
    /// <returns>Elapsed seconds.</returns>
    public double TimeSecondsAt(int index) => index * IntervalNanoseconds / 1e9;

    /// <summary>The total duration covered by the block, in seconds.</summary>
    public double DurationSeconds => SampleCount * IntervalNanoseconds / 1e9;
}
