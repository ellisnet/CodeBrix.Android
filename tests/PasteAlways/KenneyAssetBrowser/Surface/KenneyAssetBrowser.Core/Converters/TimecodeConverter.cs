using Microsoft.UI.Xaml.Data;
using System;

namespace KenneyAssetBrowser.Converters;

/// <summary>
/// Formats an AudioPlayer position/duration <see cref="TimeSpan"/> for the audio scrubber's
/// two timecode labels. The tenth of a second is deliberate: most of what an asset pack ships
/// is a sound effect well under a second long, and a plain m:ss would show "0:00 / 0:00" for
/// the whole clip.
/// </summary>
public sealed class TimecodeConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is TimeSpan time ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 100}" : "0:00.0";

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
