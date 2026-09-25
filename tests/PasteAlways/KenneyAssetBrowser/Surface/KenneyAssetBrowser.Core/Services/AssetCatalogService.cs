using KenneyAssetBrowser.AssetRead;
using KenneyAssetBrowser.AssetRead.Models;
using System.Threading.Tasks;

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
