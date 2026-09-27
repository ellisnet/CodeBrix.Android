using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WebcamPainter.Converters;

/// <summary>
/// Turns a <see cref="Color"/> from the imaging library into a XAML brush, so the highlighter
/// buttons take their background and caption colors straight from the palette that the
/// painting library owns instead of repeating those values as literals in the markup.
/// </summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
        {
            Rgba32 rgba = color.ToPixel<Rgba32>();
            return new SolidColorBrush(Windows.UI.Color.FromArgb(rgba.A, rgba.R, rgba.G, rgba.B));
        }

        return null;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
