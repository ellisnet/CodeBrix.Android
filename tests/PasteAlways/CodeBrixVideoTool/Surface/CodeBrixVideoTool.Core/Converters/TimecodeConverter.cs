using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace CodeBrixVideoTool.Converters;

/// <summary>
/// Formats a position or a duration for the transport bar: <c>m:ss</c> for anything under an hour,
/// <c>h:mm:ss</c> above it.
/// </summary>
public sealed class TimecodeConverter : IValueConverter
{
    /// <summary>Formats a <see cref="TimeSpan" /> as a timecode.</summary>
    /// <param name="value">The position or duration.</param>
    /// <param name="targetType">Ignored.</param>
    /// <param name="parameter">Ignored.</param>
    /// <param name="language">Ignored.</param>
    /// <returns>The timecode, or "0:00" when the value is not a time.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TimeSpan time || time < TimeSpan.Zero)
        {
            return "0:00";
        }

        return time.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalMinutes}:{time.Seconds:00}");
    }

    /// <summary>Not supported: a timecode is never typed back into the player.</summary>
    /// <param name="value">Ignored.</param>
    /// <param name="targetType">Ignored.</param>
    /// <param name="parameter">Ignored.</param>
    /// <param name="language">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("A timecode is shown, never entered.");
}
