namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// Identifies an input channel, using the same ordinal values the ps2000 driver
/// uses for its <c>PS2000_CHANNEL</c> enumeration.
/// </summary>
/// <remarks>
/// The driver's enumeration covers the whole 2000 series, so it names four
/// channels plus an external trigger input. Individual models implement a
/// subset: a PicoScope 2204A has <see cref="ChannelA"/> and <see cref="ChannelB"/>
/// only, and no external trigger. Ask
/// <see cref="ScopeCapabilities.SupportedChannels"/> rather than assuming.
/// </remarks>
public enum ChannelId
{
    /// <summary>Input channel A. Present on every 2000-series model.</summary>
    ChannelA = 0,

    /// <summary>Input channel B.</summary>
    ChannelB = 1,

    /// <summary>Input channel C. Not present on a two-channel model.</summary>
    ChannelC = 2,

    /// <summary>Input channel D. Not present on a two-channel model.</summary>
    ChannelD = 3,

    /// <summary>
    /// The external trigger input. Not present on a PicoScope 2204A -- the
    /// driver accepts the value but the device rejects it.
    /// </summary>
    External = 4,

    /// <summary>No channel. Used to disable triggering.</summary>
    None = 5
}
