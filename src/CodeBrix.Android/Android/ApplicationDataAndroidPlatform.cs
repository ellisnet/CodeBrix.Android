using System.IO;
using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of <see cref="IApplicationDataPlatform"/>: the
/// ApplicationData folders live in the application's private files and cache
/// directories (see <see cref="ApplicationDataLayout"/>). Every folder is created on
/// first use.
/// </summary>
internal sealed class ApplicationDataAndroidPlatform : IApplicationDataPlatform
{
    private readonly object _gate = new();
    private ApplicationDataLayout _layout;

    /// <inheritdoc />
    public string GetLocalFolderPath() => Ensure(Layout.LocalFolderPath);

    /// <inheritdoc />
    public string GetRoamingFolderPath() => Ensure(Layout.RoamingFolderPath);

    /// <inheritdoc />
    public string GetLocalCacheFolderPath() => Ensure(Layout.LocalCacheFolderPath);

    /// <inheritdoc />
    public string GetTemporaryFolderPath() => Ensure(Layout.TemporaryFolderPath);

    /// <inheritdoc />
    public string GetSettingsFolderPath() => Ensure(Layout.SettingsFolderPath);

    private ApplicationDataLayout Layout
    {
        get
        {
            lock (_gate)
            {
                if (_layout == null)
                {
                    var context = AndroidContext.Current;
                    _layout = new ApplicationDataLayout(context.FilesDir.AbsolutePath, context.CacheDir.AbsolutePath);
                }

                return _layout;
            }
        }
    }

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
