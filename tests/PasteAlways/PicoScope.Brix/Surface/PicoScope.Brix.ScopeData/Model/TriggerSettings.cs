using System;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// The edge a simple trigger fires on.
/// </summary>
public enum TriggerDirection
{
    /// <summary>Fire when the signal crosses the threshold going up.</summary>
    Rising = 0,

    /// <summary>Fire when the signal crosses the threshold going down.</summary>
    Falling = 1
}

/// <summary>
/// The condition an advanced trigger tests, matching the driver's
/// <c>PS2000_THRESHOLD_DIRECTION</c> enumeration.
/// </summary>
/// <remarks>
/// The level and window conditions deliberately share ordinal values upstream --
/// <c>Inside</c> is the same value as <c>Above</c>, and so on -- because which
/// meaning applies is decided by the channel's
/// <see cref="ThresholdMode"/>, not by this value alone.
/// </remarks>
public enum ThresholdDirection
{
    /// <summary>Level mode: signal is above the threshold. Window mode: inside the window.</summary>
    Above = 0,

    /// <summary>Level mode: signal is below the threshold. Window mode: outside the window.</summary>
    Below = 1,

    /// <summary>Level mode: rising edge. Window mode: entering the window.</summary>
    Rising = 2,

    /// <summary>Level mode: falling edge. Window mode: leaving the window.</summary>
    Falling = 3,

    /// <summary>Either edge, or either entering or leaving the window.</summary>
    RisingOrFalling = 4,

    /// <summary>No condition on this channel. Shares its value with <see cref="Rising"/>.</summary>
    None = 2
}

/// <summary>
/// Whether an advanced trigger compares against a single level or a window
/// bounded by two levels.
/// </summary>
public enum ThresholdMode
{
    /// <summary>Compare against a single threshold.</summary>
    Level = 0,

    /// <summary>Compare against a window formed by the upper and lower thresholds.</summary>
    Window = 1
}

/// <summary>
/// The state a channel must be in for an advanced trigger condition to be met.
/// </summary>
public enum TriggerState
{
    /// <summary>This channel does not participate in the condition.</summary>
    DontCare = 0,

    /// <summary>The channel's threshold condition must be met.</summary>
    True = 1,

    /// <summary>The channel's threshold condition must not be met.</summary>
    False = 2
}

/// <summary>
/// The comparison a pulse-width qualifier applies to a measured pulse.
/// </summary>
public enum PulseWidthType
{
    /// <summary>No pulse-width qualification.</summary>
    None = 0,

    /// <summary>The pulse must be shorter than the lower bound.</summary>
    LessThan = 1,

    /// <summary>The pulse must be longer than the lower bound.</summary>
    GreaterThan = 2,

    /// <summary>The pulse width must fall between the lower and upper bounds.</summary>
    InRange = 3,

    /// <summary>The pulse width must fall outside the lower and upper bounds.</summary>
    OutOfRange = 4
}

/// <summary>
/// A simple edge trigger: one channel, one threshold, one direction.
/// </summary>
/// <param name="Source">
/// The channel to trigger from. A PicoScope 2204A accepts
/// <see cref="ChannelId.ChannelA"/>, <see cref="ChannelId.ChannelB"/> and
/// <see cref="ChannelId.None"/>; it has no external trigger input.
/// </param>
/// <param name="ThresholdAdc">
/// The trigger level as a raw ADC count on the source channel's range. Use
/// <see cref="VoltageRangeExtensions.MillivoltsToAdc"/> to convert from volts.
/// </param>
/// <param name="Direction">The edge to fire on.</param>
/// <param name="DelaySamples">
/// Where the trigger event sits within the captured block, as a fraction of the
/// block expressed in percent from -100 to +100. Zero puts the trigger at the
/// start of the block; -50 places it half way through, giving pre-trigger data.
/// </param>
/// <param name="AutoTriggerMilliseconds">
/// How long to wait for the trigger before capturing anyway. Zero waits
/// indefinitely, which will hang a capture if the event never arrives -- prefer
/// a non-zero value unless you specifically want to block.
/// </param>
public readonly record struct TriggerSettings(
    ChannelId Source,
    short ThresholdAdc,
    TriggerDirection Direction,
    float DelaySamples,
    short AutoTriggerMilliseconds)
{
    /// <summary>
    /// A trigger configuration that disables triggering, so captures start
    /// immediately.
    /// </summary>
    public static TriggerSettings Disabled { get; } =
        new TriggerSettings(ChannelId.None, 0, TriggerDirection.Rising, 0f, 0);

    /// <summary>
    /// Builds a rising-edge trigger at a given voltage on a given channel.
    /// </summary>
    /// <param name="source">The channel to trigger from.</param>
    /// <param name="range">The range that channel is set to, used to scale the level.</param>
    /// <param name="millivolts">The trigger level in millivolts.</param>
    /// <param name="autoTriggerMilliseconds">Auto-trigger timeout; defaults to one second.</param>
    /// <returns>A populated <see cref="TriggerSettings"/>.</returns>
    public static TriggerSettings RisingEdge(
        ChannelId source,
        VoltageRange range,
        double millivolts,
        short autoTriggerMilliseconds = 1000)
        => new TriggerSettings(source, range.MillivoltsToAdc(millivolts),
            TriggerDirection.Rising, 0f, autoTriggerMilliseconds);

    /// <summary>
    /// Builds a falling-edge trigger at a given voltage on a given channel.
    /// </summary>
    /// <param name="source">The channel to trigger from.</param>
    /// <param name="range">The range that channel is set to, used to scale the level.</param>
    /// <param name="millivolts">The trigger level in millivolts.</param>
    /// <param name="autoTriggerMilliseconds">Auto-trigger timeout; defaults to one second.</param>
    /// <returns>A populated <see cref="TriggerSettings"/>.</returns>
    public static TriggerSettings FallingEdge(
        ChannelId source,
        VoltageRange range,
        double millivolts,
        short autoTriggerMilliseconds = 1000)
        => new TriggerSettings(source, range.MillivoltsToAdc(millivolts),
            TriggerDirection.Falling, 0f, autoTriggerMilliseconds);
}

/// <summary>
/// The threshold configuration for one channel within an advanced trigger.
/// </summary>
/// <param name="Channel">The channel this applies to.</param>
/// <param name="ThresholdMajor">The primary threshold, as an ADC count.</param>
/// <param name="ThresholdMinor">
/// The secondary threshold, as an ADC count. Used as the second edge of the
/// window in <see cref="ThresholdMode.Window"/> mode; ignored in level mode.
/// </param>
/// <param name="Hysteresis">
/// Noise rejection band around the threshold, as an ADC count. A signal must
/// move this far past the threshold before a second crossing is recognised.
/// </param>
/// <param name="Mode">Whether to compare against a level or a window.</param>
public readonly record struct TriggerChannelProperties(
    ChannelId Channel,
    short ThresholdMajor,
    short ThresholdMinor,
    ushort Hysteresis,
    ThresholdMode Mode);

/// <summary>
/// A complete advanced-trigger configuration: per-channel thresholds, the
/// logical condition combining them, the edge each channel tests, and an
/// optional pulse-width qualifier.
/// </summary>
/// <remarks>
/// The advanced trigger is considerably more capable than
/// <see cref="TriggerSettings"/>: it can require several channels to be in
/// particular states simultaneously, test windows rather than levels, and
/// qualify on how long a condition persists. A PicoScope 2204A accepts all of
/// the advanced trigger calls.
/// </remarks>
public sealed class AdvancedTriggerSettings
{
    /// <summary>
    /// The per-channel threshold properties. One entry per participating channel.
    /// </summary>
    public TriggerChannelProperties[] ChannelProperties { get; set; } = Array.Empty<TriggerChannelProperties>();

    /// <summary>
    /// How long to wait for the trigger before capturing anyway, in
    /// milliseconds. Zero waits indefinitely.
    /// </summary>
    public int AutoTriggerMilliseconds { get; set; } = 1000;

    /// <summary>The state channel A must be in.</summary>
    public TriggerState ChannelAState { get; set; } = TriggerState.DontCare;

    /// <summary>The state channel B must be in.</summary>
    public TriggerState ChannelBState { get; set; } = TriggerState.DontCare;

    /// <summary>The state the pulse-width qualifier must be in.</summary>
    public TriggerState PulseWidthQualifierState { get; set; } = TriggerState.DontCare;

    /// <summary>The threshold condition channel A tests.</summary>
    public ThresholdDirection ChannelADirection { get; set; } = ThresholdDirection.None;

    /// <summary>The threshold condition channel B tests.</summary>
    public ThresholdDirection ChannelBDirection { get; set; } = ThresholdDirection.None;

    /// <summary>
    /// Trigger delay, in samples, applied after the condition is met.
    /// </summary>
    public uint Delay { get; set; }

    /// <summary>
    /// The fraction of the block captured before the trigger, from -100 to 0.
    /// </summary>
    public float PreTriggerDelayPercent { get; set; }
}

/// <summary>
/// A pulse-width qualifier: an extra condition requiring that a channel's
/// threshold condition persist for a particular length of time.
/// </summary>
/// <param name="ChannelAState">The state channel A must be in.</param>
/// <param name="ChannelBState">The state channel B must be in.</param>
/// <param name="Direction">The threshold condition being timed.</param>
/// <param name="LowerSamples">The lower bound of the width, in samples.</param>
/// <param name="UpperSamples">The upper bound of the width, in samples. Ignored unless the type is a range.</param>
/// <param name="Type">The comparison to apply.</param>
public readonly record struct PulseWidthQualifier(
    TriggerState ChannelAState,
    TriggerState ChannelBState,
    ThresholdDirection Direction,
    uint LowerSamples,
    uint UpperSamples,
    PulseWidthType Type);
