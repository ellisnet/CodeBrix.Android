using System;
using System.IO;
using CodeBrix.Android.UI.AudioPlayer.Portable;
using CodeBrix.Audio.Android;
using AApplication = Android.App.Application;

namespace CodeBrix.Android.UI.AudioPlayer.Android;

/// <summary>
/// ms-appx:/// audio on Android: the APK's assets are streams, not files, while the Core's source resolver and
/// CodeBrix.Audio open audio BY PATH (an SFZ instrument opens its samples beside it). The ms-appx root
/// (<see cref="AssetLocationAndroidPlatform.InstalledPath"/>) is the folder CodeBrix.Audio.Android's AndroidPackagedAssets
/// copies assets out to, and a path under it is copied out of the APK the first time it is opened (once per installed
/// build; later launches reuse the copy).
/// </summary>
internal static class PackagedAudioAssets
{
    /// <summary>The ms-appx root: AndroidPackagedAssets' folder under the app's private files.</summary>
    internal static string Root => AndroidPackagedAssets.RootDirectory(AApplication.Context);

    /// <summary>Copies out the asset a resolved path names, when it is under the ms-appx root and not there yet.</summary>
    /// <param name="path">A path the source resolver produced (any path; others are left alone).</param>
    /// <param name="instrument">True for a MIDI instrument (an SFZ or Decent Sampler preset brings its folder).</param>
    internal static void EnsureCopiedOut(string path, bool instrument = false)
    {
        var asset = PackagedAssetPaths.AssetPathOf(Root, path);
        if (asset == null || File.Exists(path) || Directory.Exists(path))
        {
            return;
        }

        try
        {
            AndroidPackagedAssets.Materialize(instrument ? PackagedAssetPaths.InstrumentAssetToCopy(asset) : asset, AApplication.Context);
        }
        catch (FileNotFoundException)
        {
            // Not an asset of this app: the player's own open reports the missing file, in its own words.
        }
    }

    /// <summary>The same, for a source string in any form the resolver accepts (only ms-appx names a copy).</summary>
    /// <param name="source">The source as written.</param>
    /// <param name="instrument">True for a MIDI instrument.</param>
    internal static void EnsureCopiedOutForSource(string source, bool instrument = false)
    {
        if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var afterScheme = Uri.UnescapeDataString(source[(source.IndexOf(':') + 1)..].TrimStart('/'));
        EnsureCopiedOut(Path.Join(Root, afterScheme), instrument);
    }
}
