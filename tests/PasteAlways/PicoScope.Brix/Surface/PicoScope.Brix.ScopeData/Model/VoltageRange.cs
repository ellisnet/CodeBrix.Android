using System;

namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// A channel's full-scale input range, using the ordinal values of the driver's
/// <c>PS2000_RANGE</c> enumeration.
/// </summary>
/// <remarks>
/// A range is symmetric about zero: <see cref="Range5V"/> means the channel
/// spans -5 V to +5 V. As with channels, the driver enumerates the union of
/// every model's ranges and each model implements a subset. A PicoScope 2204A
/// supports <see cref="Range50mV"/> through <see cref="Range20V"/>; it rejects
/// <see cref="Range10mV"/>, <see cref="Range20mV"/> and <see cref="Range50V"/>.
/// Consult <see cref="ScopeCapabilities.SupportedRanges"/>.
/// </remarks>
public enum VoltageRange
{
    /// <summary>+/- 10 mV full scale. Not supported by a PicoScope 2204A.</summary>
    Range10mV = 0,

    /// <summary>+/- 20 mV full scale. Not supported by a PicoScope 2204A.</summary>
    Range20mV = 1,

    /// <summary>+/- 50 mV full scale. The most sensitive range on a 2204A.</summary>
    Range50mV = 2,

    /// <summary>+/- 100 mV full scale.</summary>
    Range100mV = 3,

    /// <summary>+/- 200 mV full scale.</summary>
    Range200mV = 4,

    /// <summary>+/- 500 mV full scale.</summary>
    Range500mV = 5,

    /// <summary>+/- 1 V full scale.</summary>
    Range1V = 6,

    /// <summary>+/- 2 V full scale.</summary>
    Range2V = 7,

    /// <summary>+/- 5 V full scale.</summary>
    Range5V = 8,

    /// <summary>+/- 10 V full scale.</summary>
    Range10V = 9,

    /// <summary>+/- 20 V full scale. The least sensitive range on a 2204A.</summary>
    Range20V = 10,

    /// <summary>+/- 50 V full scale. Not supported by a PicoScope 2204A.</summary>
    Range50V = 11
}

/// <summary>
/// Conversions between <see cref="VoltageRange"/> values and real voltages.
/// </summary>
public static class VoltageRangeExtensions
{
    //Indexed by the VoltageRange ordinal. This is the same table the Pico
    //  examples carry, and it is the multiplier used to turn an ADC count into
    //  a voltage.
    private static readonly int[] FullScaleMillivolts =
        { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };

    /// <summary>
    /// Returns the range's full-scale value in millivolts -- the voltage that
    /// corresponds to an ADC count of <see cref="ScopeConstants.MaxAdcValue"/>.
    /// </summary>
    /// <param name="range">The range to measure.</param>
    /// <returns>Full scale in millivolts, for example 5000 for <see cref="VoltageRange.Range5V"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined range.</exception>
    public static int FullScaleInMillivolts(this VoltageRange range)
    {
        int index = (int)range;
        if (index < 0 || index >= FullScaleMillivolts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(range), range, "Not a defined PS2000 voltage range.");
        }
        return FullScaleMillivolts[index];
    }

    /// <summary>
    /// Returns the range's full-scale value in volts.
    /// </summary>
    /// <param name="range">The range to measure.</param>
    /// <returns>Full scale in volts, for example 5.0 for <see cref="VoltageRange.Range5V"/>.</returns>
    public static double FullScaleInVolts(this VoltageRange range)
        => range.FullScaleInMillivolts() / 1000.0;

    /// <summary>
    /// Converts a raw ADC count from this range into millivolts.
    /// </summary>
    /// <param name="range">The range the sample was captured on.</param>
    /// <param name="adcCount">The raw sample, nominally -32767 to +32767.</param>
    /// <returns>The sample in millivolts.</returns>
    /// <remarks>
    /// The divisor is <see cref="ScopeConstants.MaxAdcValue"/> (32767). Note this
    /// differs from the ps2000<i>a</i> driver's 32512 -- copying scaling code
    /// between the two drivers introduces a silent error of roughly 0.8%.
    /// </remarks>
    public static double AdcToMillivolts(this VoltageRange range, int adcCount)
        => adcCount * (double)range.FullScaleInMillivolts() / ScopeConstants.MaxAdcValue;

    /// <summary>
    /// Converts a raw ADC count from this range into volts.
    /// </summary>
    /// <param name="range">The range the sample was captured on.</param>
    /// <param name="adcCount">The raw sample, nominally -32767 to +32767.</param>
    /// <returns>The sample in volts.</returns>
    public static double AdcToVolts(this VoltageRange range, int adcCount)
        => range.AdcToMillivolts(adcCount) / 1000.0;

    /// <summary>
    /// Converts a millivolt value into the ADC count that represents it on this
    /// range. Useful for turning a desired trigger level into a threshold.
    /// </summary>
    /// <param name="range">The range the threshold applies to.</param>
    /// <param name="millivolts">The level in millivolts.</param>
    /// <returns>The equivalent ADC count, clamped to the representable range.</returns>
    public static short MillivoltsToAdc(this VoltageRange range, double millivolts)
    {
        double raw = millivolts * ScopeConstants.MaxAdcValue / range.FullScaleInMillivolts();
        if (raw > ScopeConstants.MaxAdcValue) { raw = ScopeConstants.MaxAdcValue; }
        if (raw < ScopeConstants.MinAdcValue) { raw = ScopeConstants.MinAdcValue; }
        return (short)Math.Round(raw);
    }

    /// <summary>
    /// Returns a short human-readable label for the range, such as "+/-5 V".
    /// </summary>
    /// <param name="range">The range to describe.</param>
    /// <returns>A display label.</returns>
    public static string ToDisplayString(this VoltageRange range)
    {
        int mv = range.FullScaleInMillivolts();
        return mv < 1000 ? $"+/-{mv} mV" : $"+/-{mv / 1000} V";
    }
}
