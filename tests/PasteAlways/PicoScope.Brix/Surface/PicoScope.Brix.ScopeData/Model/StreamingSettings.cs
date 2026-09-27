using System;
using System.Collections.Generic;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// Which of the driver's two streaming implementations to use.
/// </summary>
/// <remarks>
/// The ps2000 driver grew streaming twice. Both are supported by a
/// PicoScope 2204A, and they are not interchangeable.
/// </remarks>
public enum StreamingMode
{
    /// <summary>
    /// The original streaming mode. The sample interval is expressed in whole
    /// milliseconds and the host polls for data; capture is capped at 60000
    /// samples. Simple, but slow and coarse.
    /// </summary>
    Compatible = 0,

    /// <summary>
    /// Fast streaming. The sample interval can be specified down to
    /// nanoseconds, the driver pushes data through a callback, and captures can
    /// run to millions of samples. This is the mode to use for a live chart.
    /// </summary>
    Fast = 1
}

/// <summary>
/// Configuration for a streaming acquisition.
/// </summary>
public sealed class StreamingSettings
{
    /// <summary>Which streaming implementation to use. Defaults to <see cref="StreamingMode.Fast"/>.</summary>
    public StreamingMode Mode { get; set; } = StreamingMode.Fast;

    /// <summary>
    /// The interval between samples, expressed in <see cref="IntervalUnits"/>.
    /// </summary>
    public uint SampleInterval { get; set; } = 100;

    /// <summary>
    /// The units <see cref="SampleInterval"/> is expressed in. Ignored in
    /// <see cref="StreamingMode.Compatible"/>, which is always milliseconds.
    /// </summary>
    public TimeUnits IntervalUnits { get; set; } = TimeUnits.Microseconds;

    /// <summary>
    /// The maximum number of samples to collect before stopping, when
    /// <see cref="AutoStop"/> is set.
    /// </summary>
    public uint MaxSamples { get; set; } = 1_000_000;

    /// <summary>
    /// Whether the device stops by itself once <see cref="MaxSamples"/> have
    /// been collected. When false, streaming runs until explicitly stopped.
    /// </summary>
    public bool AutoStop { get; set; }

    /// <summary>
    /// How many raw samples the driver combines into each reported value. One
    /// means no aggregation. Above one, each reported position carries a
    /// minimum and a maximum, which is how a long window is drawn without
    /// losing spikes.
    /// </summary>
    public uint SamplesPerAggregate { get; set; } = 1;

    /// <summary>
    /// The size of the driver's internal overview buffer, in samples. It must be
    /// comfortably larger than the number of samples that arrive between polls,
    /// or data is overwritten before it is read.
    /// </summary>
    public uint OverviewBufferSize { get; set; } = 15000;

    /// <summary>
    /// How often the host polls the driver for newly arrived data, in
    /// milliseconds.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 10;

    /// <summary>
    /// Returns the sample interval expressed in nanoseconds.
    /// </summary>
    /// <returns>The interval in nanoseconds.</returns>
    public double IntervalInNanoseconds() => IntervalUnits switch
    {
        TimeUnits.Femtoseconds => SampleInterval / 1e6,
        TimeUnits.Picoseconds => SampleInterval / 1e3,
        TimeUnits.Nanoseconds => SampleInterval,
        TimeUnits.Microseconds => SampleInterval * 1e3,
        TimeUnits.Milliseconds => SampleInterval * 1e6,
        _ => SampleInterval * 1e9
    };
}

/// <summary>
/// A batch of samples delivered by a running stream.
/// </summary>
public sealed class StreamingSamplesEventArgs : EventArgs
{
    /// <summary>
    /// The samples that arrived in this batch, keyed by channel.
    /// </summary>
    public IReadOnlyDictionary<ChannelId, ChannelSamples> Channels { get; init; }
        = new Dictionary<ChannelId, ChannelSamples>();

    /// <summary>The number of samples in this batch.</summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// The running total of samples delivered since the stream started.
    /// </summary>
    public long TotalSampleCount { get; init; }

    /// <summary>The interval between samples, in nanoseconds.</summary>
    public double IntervalNanoseconds { get; init; }

    /// <summary>Whether a trigger fired within this batch.</summary>
    public bool Triggered { get; init; }

    /// <summary>
    /// The index within this batch at which the trigger fired, meaningful only
    /// when <see cref="Triggered"/> is set.
    /// </summary>
    public uint TriggerIndex { get; init; }

    /// <summary>The driver's per-channel overflow flags for this batch.</summary>
    public short OverflowFlags { get; init; }

    /// <summary>
    /// Whether the driver reported that its overview buffer overran -- meaning
    /// samples were produced faster than the host collected them, and some were
    /// lost. Increase the overview buffer size or poll more often.
    /// </summary>
    public bool BufferOverrun { get; init; }
}
