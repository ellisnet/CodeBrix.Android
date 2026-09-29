using System.Reflection;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.AudioPlayer.Android;

/// <summary>
/// The Android implementation of IAssetLocation (WPE1 C10; handed on from AP1.9): the root of ms-appx:/// paths is the
/// folder APK assets are copied out to (<see cref="PackagedAudioAssets.Root"/>; an APK's assets are no folder), and the
/// application assembly is the running XAML application's (embedded://./... resources), as on the Skia heads.
/// </summary>
internal sealed class AssetLocationAndroidPlatform : IAssetLocation
{
    /// <inheritdoc/>
    public string InstalledPath => PackagedAudioAssets.Root;

    /// <inheritdoc/>
    public Assembly ApplicationAssembly => Application.Current.GetType().Assembly;
}
