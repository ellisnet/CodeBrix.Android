using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Android.AppSettings.Portable;
using CodeBrix.Platform.AppSettings.Contracts;

namespace CodeBrix.Android.AppSettings.Android;

/// <summary>
/// The Android implementation of <see cref="IAppSettingsStoragePlatform"/>: the default settings folder is
/// Context.FilesDir/CodeBrix/&lt;app&gt;/settings (app-private, kept across updates, removed with the app - the
/// Android counterpart of the desktop heads' ApplicationData/CodeBrix/&lt;app&gt;/settings), and a store opens a
/// <see cref="SqliteAppSettingsStorage"/> there (an import is validated in Context.CacheDir).
/// </summary>
internal sealed class AppSettingsStorageAndroidPlatform : IAppSettingsStoragePlatform
{
    /// <inheritdoc />
    public string GetDefaultDirectory(string appName) => Path.Combine(FilesRoot(), "CodeBrix", appName, "settings");

    /// <inheritdoc />
    public IAppSettingsStorage Open(string appName, string directoryPath, IDictionary<string, string> values, Func<DateTime> clock) =>
        new SqliteAppSettingsStorage(appName, directoryPath, values, clock, CacheRoot());

    private static string FilesRoot() =>
        global::Android.App.Application.Context?.FilesDir?.AbsolutePath
        ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string CacheRoot() =>
        global::Android.App.Application.Context?.CacheDir?.AbsolutePath ?? Path.GetTempPath();
}
