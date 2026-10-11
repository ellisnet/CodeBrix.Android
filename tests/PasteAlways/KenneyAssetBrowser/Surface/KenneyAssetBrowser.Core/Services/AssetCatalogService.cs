using KenneyAssetBrowser.AssetRead;
using KenneyAssetBrowser.AssetRead.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace KenneyAssetBrowser.Services;

/// <summary>
/// The app's gateway to the AssetRead library: loads the bundle catalog from the user's
/// assets folder and opens individual bundle archives for entry reads, keeping the
/// blocking zip work off the UI thread.
/// </summary>
public class AssetCatalogService
{
    /// <summary>
    /// Reads every bundle zip in the assets folder on a worker thread.
    /// </summary>
    /// <param name="folderPath">The folder holding the user's downloaded bundle zip files.</param>
    /// <returns>The loaded catalog (empty when the folder is missing or holds no zips).</returns>
    public Task<AssetFolderCatalog> LoadCatalogAsync(string folderPath) =>
        Task.Run(() => AssetFolderCatalog.LoadFrom(folderPath));

    /// <summary>
    /// Reads every bundle zip in a picked folder that has no local path (a folder picked on
    /// Android): the folder is listed through its own members, and each zip's Path then gives a
    /// local copy of it for the zip reader.
    /// </summary>
    /// <param name="folder">The picked assets folder.</param>
    /// <returns>The loaded catalog (empty when the folder holds no zips).</returns>
    public async Task<AssetFolderCatalog> LoadCatalogAsync(StorageFolder folder)
    {
        var files = await folder.GetFilesAsync();
        var zips = files
            .Where(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        //Reading Path copies the document to local storage, so keep it off the UI thread too
        return await Task.Run(() => AssetFolderCatalog.LoadFrom(folder.Name, zips.Select(f => f.Path).ToList()));
    }

    /// <summary>
    /// Opens one bundle's archive for on-demand entry reads on a worker thread.
    /// The caller owns (and must dispose) the returned archive.
    /// </summary>
    /// <param name="bundle">The bundle to open.</param>
    public Task<BundleArchive> OpenArchiveAsync(AssetBundle bundle) =>
        Task.Run(() => new BundleArchive(bundle.ZipPath));

    /// <summary>
    /// Reads one entry's bytes from a bundle without keeping the archive open — used for
    /// one-off reads like the sidebar cover thumbnails.
    /// </summary>
    /// <param name="bundle">The bundle to read from.</param>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    /// <returns>The entry's bytes, or <c>null</c> when the bundle has no such entry.</returns>
    public Task<byte[]> ReadEntryBytesAsync(AssetBundle bundle, string entryPath) =>
        Task.Run(() =>
        {
            using var archive = new BundleArchive(bundle.ZipPath);
            return archive.ReadEntryBytes(entryPath);
        });
}
