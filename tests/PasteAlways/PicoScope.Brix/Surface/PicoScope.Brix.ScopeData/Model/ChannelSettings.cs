namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// How a channel's input is coupled to the measurement circuit.
/// </summary>
public enum Coupling
{
    /// <summary>
    /// AC coupling: a series capacitor blocks the DC component, so only the
    /// varying part of the signal is measured.
    /// </summary>
    Ac = 0,

    /// <summary>DC coupling: the whole signal, DC offset included, is measured.</summary>
    Dc = 1
}

/// <summary>
/// The configuration of a single input channel.
/// </summary>
/// <param name="Enabled">
/// Whether the channel captures data. Disabling a channel is not merely
/// cosmetic -- on a two-channel model, running with one channel enabled doubles
/// the maximum sampling rate and the sample-buffer depth. See
/// <see cref="ScopeCapabilities.MaxSamplesSingleChannel"/>.
/// </param>
/// <param name="Coupling">Whether the input is AC- or DC-coupled.</param>
/// <param name="Range">The full-scale input range.</param>
public readonly record struct ChannelSettings(bool Enabled, Coupling Coupling, VoltageRange Range)
{
    /// <summary>
    /// A sensible default: enabled, DC-coupled, +/-5 V.
    /// </summary>
    public static ChannelSettings Default { get; } =
        new ChannelSettings(true, Coupling.Dc, VoltageRange.Range5V);

    /// <summary>
    /// Returns a copy of these settings that is disabled.
    /// </summary>
    /// <returns>The same settings with <see cref="Enabled"/> set to false.</returns>
    public ChannelSettings AsDisabled() => this with { Enabled = false };
}
