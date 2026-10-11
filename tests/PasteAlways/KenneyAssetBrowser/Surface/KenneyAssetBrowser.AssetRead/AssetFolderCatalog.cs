using KenneyAssetBrowser.AssetRead.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KenneyAssetBrowser.AssetRead;

/// <summary>
/// The catalog of every readable Kenney asset bundle (.zip) found in the user's assets folder,
/// read once at load time.
/// </summary>
public class AssetFolderCatalog
{
    private AssetFolderCatalog(string folderPath, IReadOnlyList<AssetBundle> bundles, IReadOnlyList<string> warnings)
    {
        FolderPath = folderPath;
        Bundles = bundles;
        Warnings = warnings;
    }

    /// <summary>Gets the assets folder the catalog was loaded from.</summary>
    public string FolderPath { get; }

    /// <summary>Gets the parsed bundles, sorted by display name.</summary>
    public IReadOnlyList<AssetBundle> Bundles { get; }

    /// <summary>Gets one human-readable warning per zip file that could not be read.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Reads every .zip file directly inside a folder and parses each into a bundle catalog.
    /// A zip that cannot be read produces a warning instead of failing the whole load.
    /// </summary>
    /// <param name="folderPath">The folder holding the user's downloaded bundle zip files.</param>
    /// <returns>The loaded catalog; empty (with no warnings) when the folder does not exist.</returns>
    public static AssetFolderCatalog LoadFrom(string folderPath)
    {
        var zipPaths = !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath)
            ? Directory.EnumerateFiles(folderPath, "*.zip")
            : [];

        return LoadFrom(folderPath, zipPaths);
    }

    /// <summary>
    /// Parses a given set of bundle zip files into a catalog - for a folder that has no local path
    /// of its own (a folder picked on Android), whose zips each reach the disk on their own.
    /// A zip that cannot be read produces a warning instead of failing the whole load.
    /// </summary>
    /// <param name="folderPath">The name or path shown for the assets folder.</param>
    /// <param name="zipPaths">The full paths of the bundle zip files on disk.</param>
    /// <returns>The loaded catalog.</returns>
    public static AssetFolderCatalog LoadFrom(string folderPath, IEnumerable<string> zipPaths)
    {
        var bundles = new List<AssetBundle>();
        var warnings = new List<string>();

        foreach (var zipPath in (zipPaths ?? [])
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                bundles.Add(KenneyBundleReader.ReadBundle(zipPath));
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(zipPath)}: {ex.Message}");
            }
        }

        return new AssetFolderCatalog(
            folderPath ?? string.Empty,
            bundles.OrderBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings);
    }
}
