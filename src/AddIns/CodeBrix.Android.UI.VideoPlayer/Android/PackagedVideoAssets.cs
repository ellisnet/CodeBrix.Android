using System;
using System.IO;
using CodeBrix.Android.UI.VideoPlayer.Portable;
using CodeBrix.Audio.Android;
using AApplication = Android.App.Application;

namespace CodeBrix.Android.UI.VideoPlayer.Android;

/// <summary>
/// ms-appx:/// video on Android: the APK's assets are streams, not files, while CodeBrix.VideoPlayback opens a source BY PATH
/// (streaming it). The ms-appx root (<see cref="AssetLocationAndroidPlatform.InstalledPath"/>) is the folder
/// CodeBrix.Audio.Android's AndroidPackagedAssets copies assets out to (the AudioPlayer add-in uses the same folder), and
/// the asset a source names is copied out of the APK before it is opened (once per installed build).
/// </summary>
internal static class PackagedVideoAssets
{
    /// <summary>The ms-appx root: AndroidPackagedAssets' folder under the app's private files.</summary>
    internal static string Root => AndroidPackagedAssets.RootDirectory(AApplication.Context);

    /// <summary>Copies out the asset an ms-appx source names, when it is not there yet (off the UI thread: videos are large).</summary>
    /// <param name="source">The source as written (only ms-appx names a copy).</param>
    internal static void EnsureCopiedOutForSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = Path.Join(Root, Uri.UnescapeDataString(source[(source.IndexOf(':') + 1)..].TrimStart('/')));
        var asset = PackagedAssetPaths.AssetPathOf(Root, path);
        if (asset == null || File.Exists(path))
        {
            return;
        }

        try
        {
            AndroidPackagedAssets.Materialize(asset, AApplication.Context);
        }
        catch (FileNotFoundException)
        {
            // Not an asset of this app: the player's own open reports the missing file, in its own words.
        }
    }
}
