using System.Reflection;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Contracts;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.VideoPlayer.Android;

/// <summary>
/// The Android implementation of the VideoPlayer Core's IAssetLocation (WPE1 C11; handed on from AP1.9): the root of
/// ms-appx:/// paths is the folder APK assets are copied out to (<see cref="PackagedVideoAssets.Root"/>; an APK's assets are
/// no folder), and the application assembly is the running XAML application's (embedded://./... resources).
/// </summary>
internal sealed class AssetLocationAndroidPlatform : IAssetLocation
{
    /// <inheritdoc/>
    public string InstalledPath => PackagedVideoAssets.Root;

    /// <inheritdoc/>
    public Assembly ApplicationAssembly => Application.Current.GetType().Assembly;
}
