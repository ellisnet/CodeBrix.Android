using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PolyHavenBrowser.Converters;

/// <summary>
/// Returns the <c>AccentButtonStyle</c> when the bound value is <see langword="true"/> and
/// <see langword="null"/> (the default button style) otherwise, so the selected sample
/// button is highlighted like a radio selection.
/// </summary>
public sealed class BoolToAccentStyleConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool selected && selected
            && Application.Current is { } app
            && app.Resources.TryGetValue("AccentButtonStyle", out var resource)
            && resource is Style style)
        {
            return style;
        }

        return null;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
