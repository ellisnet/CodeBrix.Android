using System;
using System.IO;
using CodeBrix.Platform.UI.CommandBar.Contracts;
using CodeBrix.Platform.UI.Svg;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CodeBrix.Android.UI.CommandBar.Android;

/// <summary>
/// The Android implementation of <see cref="IIconRasterizationPlatform"/>, the same as every CodeBrix.Platform head's:
/// an icon is an SvgImageSource rasterized at the icon size (RasterizePixelWidth/Height), tinted through a stylesheet
/// the Svg add-in applies before it parses (SvgProvider.SetCss), and loaded from the document bytes when the icon
/// carries its markup, otherwise from the artwork URI. The Svg add-in draws it (an Image's SvgCanvas).
/// </summary>
internal sealed class IconRasterizationAndroidPlatform : IIconRasterizationPlatform
{
    /// <inheritdoc />
    public SvgImageSource CreateSvgImageSource(Uri artwork, byte[] document, double size, string css)
    {
        var source = new SvgImageSource { RasterizePixelWidth = size, RasterizePixelHeight = size };
        SvgProvider.SetCss(source, css);
        if (document != null)
        {
            _ = source.SetSourceAsync(new MemoryStream(document).AsRandomAccessStream());
        }
        else if (artwork != null)
        {
            source.UriSource = artwork;
        }

        return source;
    }
}
