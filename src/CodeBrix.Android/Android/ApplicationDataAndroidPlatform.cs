using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of <see cref="IApplicationDataPlatform"/>: the
/// ApplicationData folders live in the application's private files and cache
/// directories (see <see cref="ApplicationDataLayout"/>). The local, roaming, cache and
/// temporary folders are created on first use; the settings folder only when the first
/// setting is written (see <see cref="ApplicationDataFolders"/>).
/// </summary>
internal sealed class ApplicationDataAndroidPlatform : IApplicationDataPlatform
{
    private readonly ApplicationDataFolders _folders = new(() =>
    {
        var context = AndroidContext.Current;
        return new ApplicationDataLayout(context.FilesDir.AbsolutePath, context.CacheDir.AbsolutePath);
    });

    /// <inheritdoc />
    public string GetLocalFolderPath() => _folders.GetLocalFolderPath();

    /// <inheritdoc />
    public string GetRoamingFolderPath() => _folders.GetRoamingFolderPath();

    /// <inheritdoc />
    public string GetLocalCacheFolderPath() => _folders.GetLocalCacheFolderPath();

    /// <inheritdoc />
    public string GetTemporaryFolderPath() => _folders.GetTemporaryFolderPath();

    /// <inheritdoc />
    public string GetSettingsFolderPath() => _folders.GetSettingsFolderPath();
}
