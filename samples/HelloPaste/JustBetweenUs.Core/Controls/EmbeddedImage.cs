using CodeBrix.Platform.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace JustBetweenUs.Controls;

/// <summary>
/// An Image control that supports loading image files from embedded resources
/// via the embedded://AssemblyName/ResourceName URI scheme,
/// as well as standard URIs (ms-appx:///, https://).
/// Supports SVG, PNG, JPEG, BMP, GIF and other image formats.
/// </summary>
public sealed class EmbeddedImage : Image
{
    /// <summary>
    /// Raised when the image named by <see cref="UriSource"/> could not be loaded - a misspelled
    /// resource name, an assembly that is not loaded, or an image the decoder rejected. Without a
    /// handler the control is left empty, so anything that cares about a missing image should
    /// subscribe; the failure is written to the application log either way.
    /// </summary>
    public event EventHandler<EmbeddedImageFailedEventArgs> LoadFailed;

    public static readonly DependencyProperty UriSourceProperty =
        DependencyProperty.Register(
            nameof(UriSource), typeof(string), typeof(EmbeddedImage),
            new PropertyMetadata(null, OnUriSourceChanged));

    /// <summary>
    /// The URI of the image source. Supports embedded://AssemblyName/ResourceName
    /// for embedded resources, or standard URIs (ms-appx:///, https://).
    /// </summary>
    public string UriSource
    {
        get => (string)GetValue(UriSourceProperty);
        set => SetValue(UriSourceProperty, value);
    }

    private static void OnUriSourceChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
        => _ = LoadImageAsync((EmbeddedImage)d, e.NewValue as string);

    private static async Task LoadImageAsync(EmbeddedImage image, string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            image.Source = null;
            return;
        }

        try
        {
            if (uri.StartsWith("embedded://", StringComparison.OrdinalIgnoreCase))
            {
                // Parse: embedded://AssemblyName/Fully.Qualified.Resource.Name
                var path = uri["embedded://".Length..];
                var separatorIndex = path.IndexOf('/');
                if (separatorIndex < 0)
                    throw new ArgumentException(
                        $"Invalid embedded resource URI: {uri}. "
                        + "Expected: embedded://AssemblyName/Resource.Name");

                var assemblyName = path[..separatorIndex];
                var resourceName = path[(separatorIndex + 1)..];

                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName)
                    ?? throw new InvalidOperationException(
                        $"Assembly '{assemblyName}' is not loaded.");

                await using var resourceStream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException(
                        $"Resource '{resourceName}' not found in '{assemblyName}'.");

                // Copy embedded resource into an IRandomAccessStream.
                // Note: ras and writeStream are intentionally not disposed here.
                // - Disposing writeStream closes the underlying ras (Platform behavior).
                // - Disposing ras is unsafe because SetSourceAsync may retain a reference
                //   to the stream rather than copying its contents, and that contract is
                //   not guaranteed across CodeBrix Platform targets.
                // - InMemoryRandomAccessStream is backed entirely by managed memory (no
                //   file handles or unmanaged resources), so the GC will reclaim it once
                //   the image source releases its reference.
                var ras = new InMemoryRandomAccessStream();
                var writeStream = ras.AsStreamForWrite();
                await resourceStream.CopyToAsync(writeStream);
                await writeStream.FlushAsync();
                ras.Seek(0);

                // Use SvgImageSource for .svg files, BitmapImage for everything else
                if (resourceName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var svgSource = new SvgImageSource();
                    await svgSource.SetSourceAsync(ras);
                    image.Source = svgSource;
                }
                else
                {
                    var bitmapSource = new BitmapImage();
                    await bitmapSource.SetSourceAsync(ras);
                    image.Source = bitmapSource;
                }
            }
            else
            {
                // Standard URI — use SvgImageSource for .svg, BitmapImage otherwise
                if (uri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    image.Source = new SvgImageSource { UriSource = new Uri(uri) };
                else
                    image.Source = new BitmapImage { UriSource = new Uri(uri) };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EmbeddedImage] Failed to load from '{uri}': {ex.Message}");

            LogExtensionPoint.AmbientLoggerFactory
                .CreateLogger<EmbeddedImage>()
                .LogError(ex, "EmbeddedImage failed to load from '{UriSource}'.", uri);

            image.LoadFailed?.Invoke(image, new EmbeddedImageFailedEventArgs(uri, ex));
        }
    }
}
