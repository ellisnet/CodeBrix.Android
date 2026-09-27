namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// Equivalent-time sampling mode.
/// </summary>
/// <remarks>
/// ETS builds one very high resolution picture of a <b>repetitive</b> waveform
/// by capturing it many times, each capture deliberately offset by a fraction of
/// a sample interval, then interleaving the results. It buys effective sampling
/// rates far beyond the hardware's real one -- a PicoScope 2204A reaches an
/// effective 500 ps interval, against a real best of 10 ns -- but it is
/// meaningless on a single-shot or non-repeating signal.
/// </remarks>
public enum EtsMode
{
    /// <summary>ETS disabled; captures are ordinary real-time blocks.</summary>
    Off = 0,

    /// <summary>Report ready as soon as the requested number of interleaves is available.</summary>
    Fast = 1,

    /// <summary>Report ready every time a complete set of cycles has been collected.</summary>
    Slow = 2
}

/// <summary>
/// The outcome of configuring equivalent-time sampling.
/// </summary>
/// <param name="Mode">The mode that was requested.</param>
/// <param name="Cycles">The number of captures to interleave.</param>
/// <param name="Interleave">The number of sub-sample offsets to step through.</param>
/// <param name="EffectiveIntervalPicoseconds">
/// The effective sampling interval the device achieved, in picoseconds. Zero
/// means the device does not support ETS.
/// </param>
public readonly record struct EtsResult(
    EtsMode Mode,
    int Cycles,
    int Interleave,
    int EffectiveIntervalPicoseconds)
{
    /// <summary>Whether the device accepted the ETS configuration.</summary>
    public bool IsSupported => EffectiveIntervalPicoseconds > 0;

    /// <summary>The effective sampling interval in nanoseconds.</summary>
    public double EffectiveIntervalNanoseconds => EffectiveIntervalPicoseconds / 1000.0;
}
