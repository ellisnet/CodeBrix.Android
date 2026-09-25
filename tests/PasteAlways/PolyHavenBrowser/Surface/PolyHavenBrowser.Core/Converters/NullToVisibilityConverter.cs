using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PolyHavenBrowser.Converters;

/// <summary>
/// Shows an element only while a bound value is <see langword="null"/> (e.g. the loading
/// ring behind a catalog cell's thumbnail, hidden once the image arrives). Pass any
/// ConverterParameter to invert (visible when non-null).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNull = value == null;
        var visibleWhenNull = parameter == null;
        return isNull == visibleWhenNull ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
