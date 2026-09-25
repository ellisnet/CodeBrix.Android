using System;
using System.IO;

namespace CodeBrix.Android.Portable;

/// <summary>
/// The Windows.Storage.ApplicationData folder layout on Android, computed from the
/// application's private files and cache directories (Context.FilesDir, Context.CacheDir).
/// Android has no roaming storage; Roaming is a private sub-folder of the files directory.
/// </summary>
internal sealed class ApplicationDataLayout
{
    /// <summary>Name of the roaming sub-folder of the files directory.</summary>
    internal const string RoamingFolderName = "Roaming";

    /// <summary>Name of the settings sub-folder of the files directory.</summary>
    internal const string SettingsFolderName = "Settings";

    /// <summary>Name of the temporary sub-folder of the cache directory.</summary>
    internal const string TemporaryFolderName = "Temp";

    /// <summary>Creates the layout for the given files and cache directories.</summary>
    internal ApplicationDataLayout(string filesDirectory, string cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(filesDirectory))
        {
            throw new ArgumentException("The files directory is required.", nameof(filesDirectory));
        }

        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            throw new ArgumentException("The cache directory is required.", nameof(cacheDirectory));
        }

        LocalFolderPath = Trim(filesDirectory);
        LocalCacheFolderPath = Trim(cacheDirectory);
        RoamingFolderPath = Path.Combine(LocalFolderPath, RoamingFolderName);
        SettingsFolderPath = Path.Combine(LocalFolderPath, SettingsFolderName);
        TemporaryFolderPath = Path.Combine(LocalCacheFolderPath, TemporaryFolderName);
    }

    /// <summary>ApplicationData.LocalFolder = Context.FilesDir.</summary>
    internal string LocalFolderPath { get; }

    /// <summary>ApplicationData.RoamingFolder = FilesDir/Roaming.</summary>
    internal string RoamingFolderPath { get; }

    /// <summary>ApplicationData.LocalCacheFolder = Context.CacheDir.</summary>
    internal string LocalCacheFolderPath { get; }

    /// <summary>ApplicationData.TemporaryFolder = CacheDir/Temp.</summary>
    internal string TemporaryFolderPath { get; }

    /// <summary>The folder that holds ApplicationData.LocalSettings = FilesDir/Settings.</summary>
    internal string SettingsFolderPath { get; }

    private static string Trim(string path) =>
        path.Length > 1 ? path.TrimEnd('/', '\\') : path;
}
