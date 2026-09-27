using System;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// The waveform the built-in signal generator produces, matching the driver's
/// <c>PS2000_WAVE_TYPE</c> enumeration.
/// </summary>
/// <remarks>
/// Note the ordering differs from the ps2000<i>a</i> driver's equivalent
/// enumeration -- here <see cref="DcVoltage"/> is 5 and <see cref="Gaussian"/>
/// is 6, whereas the A-series driver places them at 8 and 6. All nine values are
/// accepted by a PicoScope 2204A.
/// </remarks>
public enum WaveType
{
    /// <summary>A sine wave.</summary>
    Sine = 0,

    /// <summary>A square wave.</summary>
    Square = 1,

    /// <summary>A triangle wave.</summary>
    Triangle = 2,

    /// <summary>A rising sawtooth.</summary>
    RampUp = 3,

    /// <summary>A falling sawtooth.</summary>
    RampDown = 4,

    /// <summary>A constant voltage, set by the offset.</summary>
    DcVoltage = 5,

    /// <summary>A Gaussian pulse.</summary>
    Gaussian = 6,

    /// <summary>A sinc (sin x / x) pulse.</summary>
    Sinc = 7,

    /// <summary>A half sine.</summary>
    HalfSine = 8
}

/// <summary>
/// How a frequency sweep moves between its start and stop frequencies.
/// </summary>
public enum SweepType
{
    /// <summary>Sweep from start up to stop, then jump back to start.</summary>
    Up = 0,

    /// <summary>Sweep from start down to stop, then jump back to start.</summary>
    Down = 1,

    /// <summary>Sweep up then back down.</summary>
    UpDown = 2,

    /// <summary>Sweep down then back up.</summary>
    DownUp = 3
}

/// <summary>
/// A built-in signal generator configuration.
/// </summary>
/// <param name="WaveType">The waveform to generate.</param>
/// <param name="OffsetMicrovolts">The DC offset applied to the waveform, in microvolts.</param>
/// <param name="PeakToPeakMicrovolts">The peak-to-peak amplitude, in microvolts.</param>
/// <param name="StartFrequencyHz">
/// The frequency to generate, or the frequency to start a sweep at. A
/// PicoScope 2204A accepts 0 Hz to 100 kHz and rejects anything higher.
/// </param>
/// <param name="StopFrequencyHz">
/// The frequency to sweep to. Set equal to <paramref name="StartFrequencyHz"/>
/// for a fixed tone.
/// </param>
/// <param name="IncrementHz">How far the frequency moves at each sweep step.</param>
/// <param name="DwellTimeSeconds">How long the generator sits at each sweep step.</param>
/// <param name="SweepType">The shape of the sweep.</param>
/// <param name="SweepCount">
/// How many times to sweep. Zero means sweep continuously.
/// </param>
public readonly record struct SignalGeneratorSettings(
    WaveType WaveType,
    int OffsetMicrovolts,
    uint PeakToPeakMicrovolts,
    double StartFrequencyHz,
    double StopFrequencyHz,
    double IncrementHz,
    double DwellTimeSeconds,
    SweepType SweepType,
    uint SweepCount)
{
    /// <summary>
    /// Builds a fixed-frequency tone with no sweep and no DC offset.
    /// </summary>
    /// <param name="waveType">The waveform to generate.</param>
    /// <param name="frequencyHz">The frequency, in hertz.</param>
    /// <param name="peakToPeakVolts">The peak-to-peak amplitude, in volts.</param>
    /// <returns>A populated <see cref="SignalGeneratorSettings"/>.</returns>
    public static SignalGeneratorSettings Tone(WaveType waveType, double frequencyHz, double peakToPeakVolts)
        => new SignalGeneratorSettings(
            waveType, 0, (uint)Math.Round(peakToPeakVolts * 1_000_000.0),
            frequencyHz, frequencyHz, 0, 0, SweepType.Up, 0);

    /// <summary>
    /// A configuration that silences the generator by holding it at 0 V DC.
    /// </summary>
    public static SignalGeneratorSettings Off { get; } =
        new SignalGeneratorSettings(WaveType.DcVoltage, 0, 0, 0, 0, 0, 0, SweepType.Up, 0);
}

/// <summary>
/// An arbitrary waveform generator configuration.
/// </summary>
/// <param name="Waveform">
/// The waveform samples, one byte per sample, spanning the generator's full
/// output swing from 0 to 255. At most
/// <see cref="ScopeConstants.MaxArbitraryWaveformSize"/> samples; longer buffers
/// are rejected by the device.
/// </param>
/// <param name="OffsetMicrovolts">The DC offset applied to the waveform, in microvolts.</param>
/// <param name="PeakToPeakMicrovolts">The peak-to-peak amplitude, in microvolts.</param>
/// <param name="StartDeltaPhase">
/// The phase increment per DDS clock tick at the start of a sweep. Use
/// <see cref="FrequencyToDeltaPhase"/> to derive it from a frequency.
/// </param>
/// <param name="StopDeltaPhase">The phase increment at the end of a sweep.</param>
/// <param name="DeltaPhaseIncrement">How much the phase increment changes at each sweep step.</param>
/// <param name="DwellCount">How many DDS ticks the generator sits at each sweep step.</param>
/// <param name="SweepType">The shape of the sweep.</param>
/// <param name="SweepCount">How many times to sweep. Zero means continuously.</param>
public readonly record struct ArbitraryWaveformSettings(
    byte[] Waveform,
    int OffsetMicrovolts,
    uint PeakToPeakMicrovolts,
    uint StartDeltaPhase,
    uint StopDeltaPhase,
    uint DeltaPhaseIncrement,
    uint DwellCount,
    SweepType SweepType,
    uint SweepCount)
{
    /// <summary>
    /// Converts a desired output frequency into the DDS phase increment the
    /// generator needs.
    /// </summary>
    /// <param name="frequencyHz">The desired repetition frequency of the whole waveform.</param>
    /// <param name="waveformLength">The number of samples in the waveform buffer.</param>
    /// <returns>The phase increment per DDS clock tick.</returns>
    /// <remarks>
    /// The generator advances a 32-bit phase accumulator once per DDS clock
    /// tick, and the waveform repeats each time the accumulator wraps. So one
    /// full cycle per second needs an increment of 2^32 / ddsFrequency, and the
    /// increment for any frequency is just:
    /// <code>deltaPhase = frequency * 2^32 / ddsFrequency</code>
    /// The buffer length cancels out of that expression -- it sets the
    /// resolution of the waveform, not its repetition rate -- which is why it is
    /// not used here despite being the obvious thing to reach for.
    /// </remarks>
    public static uint FrequencyToDeltaPhase(double frequencyHz, int waveformLength)
    {
        if (waveformLength <= 0) { throw new ArgumentOutOfRangeException(nameof(waveformLength)); }

        double increment = frequencyHz * ScopeConstants.AwgPhaseAccumulator / ScopeConstants.AwgDdsFrequency;
        if (increment < 0) { increment = 0; }
        if (increment > uint.MaxValue) { increment = uint.MaxValue; }
        return (uint)increment;
    }

    /// <summary>
    /// Builds a fixed-frequency arbitrary waveform with no sweep.
    /// </summary>
    /// <param name="waveform">The waveform samples.</param>
    /// <param name="frequencyHz">The repetition frequency, in hertz.</param>
    /// <param name="peakToPeakVolts">The peak-to-peak amplitude, in volts.</param>
    /// <returns>A populated <see cref="ArbitraryWaveformSettings"/>.</returns>
    public static ArbitraryWaveformSettings Fixed(byte[] waveform, double frequencyHz, double peakToPeakVolts)
    {
        if (waveform == null) { throw new ArgumentNullException(nameof(waveform)); }
        uint delta = FrequencyToDeltaPhase(frequencyHz, waveform.Length);
        return new ArbitraryWaveformSettings(
            waveform, 0, (uint)Math.Round(peakToPeakVolts * 1_000_000.0),
            delta, delta, 0, 0, SweepType.Up, 0);
    }
}
