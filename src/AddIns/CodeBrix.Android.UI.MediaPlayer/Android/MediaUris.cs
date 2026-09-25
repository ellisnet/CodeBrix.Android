using System;
using System.IO;
using Windows.Storage;

namespace CodeBrix.Android.UI.MediaPlayer.Android;

/// <summary>
/// The address Media3 plays for a MediaPlayer source (the resolution every CodeBrix.Platform engine makes): a relative
/// address is ms-appx; ms-appx is the app's packaged Content, which the consumer build packages as Android assets under
/// its ms-appx path (asset:///); ms-appdata is the app data folder; a file or a network address plays as it is.
/// </summary>
internal static class MediaUris
{
    /// <summary>The engine address of a source.</summary>
    /// <param name="uri">The source.</param>
    /// <returns>The address Media3 opens.</returns>
    internal static string ToEngineUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Scheme))
        {
            uri = new Uri("ms-appx:///" + uri.OriginalString.TrimStart('/'));
        }

        switch (uri.Scheme.ToLowerInvariant())
        {
            case "ms-appx":
            {
                var path = (uri.Host.Length > 0 ? uri.Host + "/" : string.Empty) + uri.AbsolutePath.TrimStart('/');
                return "asset:///" + Uri.UnescapeDataString(path);
            }

            case "ms-appdata":
            {
                var path = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                var slash = path.IndexOf('/');
                var root = slash < 0 ? path : path.Substring(0, slash);
                var rest = slash < 0 ? string.Empty : path.Substring(slash + 1);
                var folder = root.ToLowerInvariant() switch
                {
                    "roaming" => ApplicationData.Current.RoamingFolder.Path,
                    "temp" => ApplicationData.Current.TemporaryFolder.Path,
                    _ => ApplicationData.Current.LocalFolder.Path,
                };
                return new Uri(Path.Combine(folder, rest)).AbsoluteUri;
            }

            default:
                return uri.IsFile ? uri.AbsoluteUri : uri.OriginalString;
        }
    }
}
