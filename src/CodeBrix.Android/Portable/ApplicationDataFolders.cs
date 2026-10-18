using System;
using System.IO;
using CodeBrix.Platform.Contracts;

namespace CodeBrix.Android.Portable;

/// <summary>
/// The ApplicationData folders of an <see cref="ApplicationDataLayout"/> as the <see cref="IApplicationDataPlatform"/>
/// contract hands them out: the local, roaming, cache and temporary folders are created on first use; the settings
/// folder is only named - it is created when the first setting is written into it (ApplicationData.LocalSettings
/// creates its folder before it writes), so an app that never uses LocalSettings has no empty Settings folder.
/// </summary>
internal sealed class ApplicationDataFolders : IApplicationDataPlatform
{
    private readonly object _gate = new();
    private readonly Func<ApplicationDataLayout> _createLayout;
    private ApplicationDataLayout _layout;

    /// <summary>Creates the folders over a layout computed on first use.</summary>
    /// <param name="createLayout">Computes the layout (called once, on the first folder query).</param>
    /// <exception cref="ArgumentNullException"><paramref name="createLayout"/> is null.</exception>
    internal ApplicationDataFolders(Func<ApplicationDataLayout> createLayout)
    {
        ArgumentNullException.ThrowIfNull(createLayout);
        _createLayout = createLayout;
    }

    /// <inheritdoc />
    public string GetLocalFolderPath() => Ensure(Layout.LocalFolderPath);

    /// <inheritdoc />
    public string GetRoamingFolderPath() => Ensure(Layout.RoamingFolderPath);

    /// <inheritdoc />
    public string GetLocalCacheFolderPath() => Ensure(Layout.LocalCacheFolderPath);

    /// <inheritdoc />
    public string GetTemporaryFolderPath() => Ensure(Layout.TemporaryFolderPath);

    /// <summary>
    /// Returns the folder that holds the files behind ApplicationData.LocalSettings and RoamingSettings WITHOUT creating
    /// it: the settings store reads its file only when it exists and creates the folder when it first writes.
    /// </summary>
    /// <returns>The full path of the settings folder (it may not exist yet).</returns>
    public string GetSettingsFolderPath() => Layout.SettingsFolderPath;

    private ApplicationDataLayout Layout
    {
        get
        {
            lock (_gate)
            {
                return _layout ??= _createLayout();
            }
        }
    }

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
