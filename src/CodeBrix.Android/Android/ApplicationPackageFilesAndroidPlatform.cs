using System.IO;
using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;
using AAssetManager = global::Android.Content.Res.AssetManager;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the optional IApplicationPackageFilesPlatform contract (AP1.12, WPE1-13 item a): the
/// application package's files - what an ms-appx:/// URI names - are the APK's ASSETS, read through the application's
/// AssetManager. CodeBrix.Android's build puts every packaged file of an app at its package-relative path in the assets
/// (the same path AppAssetPath maps), so "ms-appx:///Assets/pulse.json" is the asset "Assets/pulse.json". While this is
/// registered, Core's StorageFile.GetFileFromApplicationUriAsync, the image loaders, Lottie's ms-appx documents, the font
/// manifests and RandomAccessStreamReference.CreateFromUri all read the package through it.
/// </summary>
/// <remarks>The path rules and the existence answer are <see cref="PackageFileLookup"/> (host-free tested); this class
/// only supplies the AssetManager's open and list. Called from any thread (AssetManager is thread-safe).</remarks>
internal sealed class ApplicationPackageFilesAndroidPlatform : IApplicationPackageFilesPlatform
{
    private PackageFileLookup _lookup;

    /// <inheritdoc />
    public bool FileExists(string relativePath) => Lookup.FileExists(relativePath);

    /// <inheritdoc />
    public Stream OpenRead(string relativePath) => Lookup.OpenRead(relativePath);

    // Created on first use: the contract is registered from the module initializer, before the application context exists.
    private PackageFileLookup Lookup => _lookup ??= new PackageFileLookup(Open, List);

    private static AAssetManager Assets => AndroidContext.Current.Assets;

    private static Stream Open(string path)
    {
        try
        {
            return Assets.Open(path);
        }
        catch (Java.IO.IOException)
        {
            // FileNotFoundException (no such asset) or a read failure: "no such file" to Core.
            return null;
        }
    }

    private static string[] List(string folder)
    {
        try
        {
            return Assets.List(folder) ?? System.Array.Empty<string>();
        }
        catch (Java.IO.IOException)
        {
            return System.Array.Empty<string>();
        }
    }
}
