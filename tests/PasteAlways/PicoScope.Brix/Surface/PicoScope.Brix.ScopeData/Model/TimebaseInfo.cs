namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// The units the driver uses to report sample timestamps, matching its
/// <c>PS2000_TIME_UNITS</c> enumeration.
/// </summary>
/// <remarks>
/// This describes the <b>timestamp array</b> filled by a times-and-values
/// capture -- not the sampling interval. See the remarks on
/// <see cref="TimebaseInfo.IntervalNanoseconds"/>, which is the single most
/// misread part of this API.
/// </remarks>
public enum TimeUnits
{
    /// <summary>Femtoseconds.</summary>
    Femtoseconds = 0,

    /// <summary>Picoseconds.</summary>
    Picoseconds = 1,

    /// <summary>Nanoseconds.</summary>
    Nanoseconds = 2,

    /// <summary>Microseconds.</summary>
    Microseconds = 3,

    /// <summary>Milliseconds.</summary>
    Milliseconds = 4,

    /// <summary>Seconds.</summary>
    Seconds = 5
}

/// <summary>
/// What the device reports about a particular timebase, for a particular
/// channel configuration and sample count.
/// </summary>
/// <param name="Timebase">
/// The timebase index that was queried. On a 2000-series device the sampling
/// interval is <c>10 * 2^timebase</c> nanoseconds, so index 0 is 10 ns
/// (100 MS/s) and index 23 is about 83.9 ms.
/// </param>
/// <param name="IsValid">
/// Whether the device accepted this timebase for the requested configuration.
/// Timebase 0 is rejected on a two-channel model unless exactly one channel is
/// enabled, because it needs the full sampling rate for a single input.
/// </param>
/// <param name="IntervalNanoseconds">
/// The sampling interval, <b>always in nanoseconds</b>.
/// </param>
/// <param name="TimestampUnits">
/// The units in which a times-and-values capture will report per-sample
/// timestamps.
/// </param>
/// <param name="MaxSamples">
/// The largest number of samples that can be captured in one block at this
/// timebase and channel configuration.
/// </param>
/// <param name="Oversample">The oversampling factor the query was made with.</param>
/// <remarks>
/// <para>
/// <b>The interval trap.</b> The driver's <c>ps2000_get_timebase</c> returns
/// both a <c>time_interval</c> and a <c>time_units</c>, and it is natural --
/// and wrong -- to read the interval as being expressed in those units. The
/// interval is always nanoseconds; the units apply only to the timestamp array.
/// </para>
/// <para>
/// Measured on a PicoScope 2204A: timebase 18 reports an interval of 2621440
/// with units of microseconds. Read literally that is 2.6 seconds per sample,
/// and an 8000-sample capture would take almost six hours. It actually
/// completes in 21 seconds, which is exactly 8000 x 2621440 <i>nanoseconds</i>.
/// The driver picks the finest timestamp unit in which the whole capture still
/// fits a 32-bit integer, which is why the reported unit drifts from
/// picoseconds to microseconds as the timebase slows.
/// </para>
/// </remarks>
public readonly record struct TimebaseInfo(
    int Timebase,
    bool IsValid,
    long IntervalNanoseconds,
    TimeUnits TimestampUnits,
    int MaxSamples,
    int Oversample)
{
    /// <summary>The sampling interval in seconds.</summary>
    public double IntervalSeconds => IntervalNanoseconds / 1e9;

    /// <summary>
    /// The effective sampling rate in samples per second, or 0 when the
    /// timebase is not valid.
    /// </summary>
    public double SampleRateHz => IntervalNanoseconds > 0 ? 1e9 / IntervalNanoseconds : 0.0;

    /// <summary>
    /// Returns an invalid result for a timebase the device rejected.
    /// </summary>
    /// <param name="timebase">The rejected timebase index.</param>
    /// <param name="oversample">The oversample factor that was requested.</param>
    /// <returns>A <see cref="TimebaseInfo"/> with <see cref="IsValid"/> false.</returns>
    public static TimebaseInfo Invalid(int timebase, int oversample)
        => new TimebaseInfo(timebase, false, 0, TimeUnits.Nanoseconds, 0, oversample);
}
