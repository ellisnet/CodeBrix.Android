namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// Fixed values from the ps2000 driver that callers need in order to interpret
/// what the device returns.
/// </summary>
public static class ScopeConstants
{
    /// <summary>
    /// The ADC count corresponding to positive full scale: 32767.
    /// </summary>
    /// <remarks>
    /// The 2000-series hardware uses an 8-bit ADC, but the driver always scales
    /// its output up to 16 bits -- whether or not oversampling is in use -- so
    /// samples always arrive on this scale.
    /// <para>
    /// This is 32767, <b>not</b> the 32512 used by the ps2000a driver for the
    /// A-series models. The two drivers genuinely differ, and reusing an
    /// A-series scaling constant here produces a silent error of about 0.8%.
    /// </para>
    /// </remarks>
    public const int MaxAdcValue = 32767;

    /// <summary>The ADC count corresponding to negative full scale: -32767.</summary>
    public const int MinAdcValue = -32767;

    /// <summary>
    /// The sentinel the driver writes into a streaming buffer position for which
    /// no data arrived: -32768. Distinct from <see cref="MinAdcValue"/>, so a
    /// genuine negative full-scale sample is never mistaken for a gap.
    /// </summary>
    public const int LostData = -32768;

    /// <summary>
    /// The largest arbitrary waveform buffer the 2000-series AWG accepts: 4096
    /// samples. Larger buffers are rejected.
    /// </summary>
    public const int MaxArbitraryWaveformSize = 4096;

    /// <summary>The AWG's DDS clock frequency, 48 MHz, used to convert a frequency into a phase increment.</summary>
    public const double AwgDdsFrequency = 48e6;

    /// <summary>The AWG's phase accumulator size, 2^32.</summary>
    public const double AwgPhaseAccumulator = 4294967296.0;

    /// <summary>The lowest frequency the built-in signal generator accepts: 0 Hz.</summary>
    public const double MinSignalGeneratorFrequency = 0.0;

    /// <summary>The highest frequency the built-in signal generator accepts: 100 kHz.</summary>
    public const double MaxSignalGeneratorFrequency = 100000.0;
}
