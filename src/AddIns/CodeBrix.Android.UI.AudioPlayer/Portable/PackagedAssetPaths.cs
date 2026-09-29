using System;
using System.IO;

namespace CodeBrix.Android.UI.AudioPlayer.Portable;

/// <summary>
/// The path rules of the ms-appx:/// audio assets on Android (host-free). The add-in's IAssetLocation names a FOLDER as the
/// root of ms-appx paths (the Core's source resolver joins the asset path to it); on Android that folder is where
/// CodeBrix.Audio.Android's AndroidPackagedAssets copies APK assets out to (their copies mirror the asset paths beneath
/// it), so a path the resolver made is under that root and its remainder is the APK asset to copy out.
/// </summary>
internal static class PackagedAssetPaths
{
    /// <summary>
    /// The APK asset path a resolved file path names: the part below <paramref name="root"/>, with forward slashes; null
    /// when the path is not under the root (a plain file, a file:// URI, an app-data path).
    /// </summary>
    /// <param name="root">The ms-appx root folder (IAssetLocation.InstalledPath).</param>
    /// <param name="path">A path the source resolver produced.</param>
    /// <returns>The asset path, or null.</returns>
    internal static string AssetPathOf(string root, string path)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
        {
            return null;
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        var relative = fullPath.Substring(fullRoot.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
        return relative.Length == 0 ? null : relative;
    }

    /// <summary>
    /// What to copy out for an instrument: an SFZ or Decent Sampler preset names its samples as files BESIDE it, so its
    /// whole folder; a SoundFont, a Decent Sampler library/bundle archive or a folder, itself. Never the asset root.
    /// </summary>
    /// <param name="assetPath">The instrument's asset path (from <see cref="AssetPathOf"/>).</param>
    /// <returns>The asset path (file or folder) to copy out.</returns>
    internal static string InstrumentAssetToCopy(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return assetPath;
        }

        var extension = Path.GetExtension(assetPath);
        var besideItsSamples = extension.Equals(".sfz", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dspreset", StringComparison.OrdinalIgnoreCase);
        var slash = assetPath.LastIndexOf('/');
        return besideItsSamples && slash > 0 ? assetPath.Substring(0, slash) : assetPath;
    }
}
