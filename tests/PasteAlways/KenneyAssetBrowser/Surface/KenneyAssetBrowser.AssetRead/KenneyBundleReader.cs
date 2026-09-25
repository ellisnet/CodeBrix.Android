using KenneyAssetBrowser.AssetRead.Models;
using KenneyAssetBrowser.AssetRead.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KenneyAssetBrowser.AssetRead;

/// <summary>
/// Parses a Kenney asset bundle (.zip) into an <see cref="AssetBundle"/> catalog:
/// entry listing, license identity, cover preview, spritesheet atlases and grouped 3D models.
/// </summary>
public static class KenneyBundleReader
{
    //Bundle cover images that may sit at the archive root, in preference order
    private static readonly string[] CoverPreviewNames =
    [
        "Preview.png",
        "Preview_KenneyNL.png",
        "Sample.png",
    ];

    //The format shown first in a 3D viewer; GLB is the single-binary format the viewer loads
    private static readonly string[] ModelFormatOrder = ["glb", "gltf", "obj", "fbx", "dae", "stl"];

    /// <summary>
    /// Reads a bundle zip file and parses its full catalog.
    /// </summary>
    /// <param name="zipPath">The full path of the bundle zip file on disk.</param>
    /// <returns>The parsed bundle catalog.</returns>
    public static AssetBundle ReadBundle(string zipPath)
    {
        using var archive = new BundleArchive(zipPath);
        return ReadBundle(archive);
    }

    /// <summary>
    /// Parses the full catalog of an already-open bundle archive.
    /// </summary>
    /// <param name="archive">The open bundle archive.</param>
    /// <returns>The parsed bundle catalog.</returns>
    public static AssetBundle ReadBundle(BundleArchive archive)
    {
        if (archive == null) { throw new ArgumentNullException(nameof(archive)); }

        var entries = archive.Entries;

        var licenseText = archive.ReadEntryText(
            entries.FirstOrDefault(e => e.Category.Length == 0 &&
                e.FileName.Equals("License.txt", StringComparison.OrdinalIgnoreCase))?.EntryPath);

        var displayName = KenneyNames.TryParseLicenseTitle(licenseText, out var title, out var version)
            ? title
            : KenneyNames.PrettifyBundleFileName(archive.ZipPath);

        var previewEntryPath = CoverPreviewNames.FirstOrDefault(archive.HasEntry);

        return new AssetBundle(
            archive.ZipPath,
            displayName,
            version,
            licenseText,
            previewEntryPath,
            entries,
            ParseAtlases(archive, entries),
            GroupModelAssets(entries));
    }

    //Parses every XML entry that turns out to be a TextureAtlas whose sheet image exists
    private static List<SpriteAtlas> ParseAtlases(BundleArchive archive, IReadOnlyList<AssetEntry> entries)
    {
        var atlases = new List<SpriteAtlas>();
        foreach (var entry in entries.Where(e => e.Extension == "xml"))
        {
            var xmlText = archive.ReadEntryText(entry.EntryPath);
            if (!SpriteAtlasParser.TryParse(xmlText, entry.EntryPath, out var atlas)) { continue; }

            if (!archive.HasEntry(atlas.ImageEntryPath))
            {
                //Some Kenney atlases declare a stale imagePath (e.g. "sprites.png" while the
                //  sheet beside the XML is "spaceShooter2_spritesheet.png"); fall back to the
                //  same-stem sibling image
                var siblingImagePath = entry.EntryPath.Substring(0, entry.EntryPath.Length - 3) + "png";
                if (!archive.HasEntry(siblingImagePath)) { continue; }
                atlas = new SpriteAtlas(atlas.XmlEntryPath, siblingImagePath, atlas.Regions);
            }

            atlases.Add(atlas);
        }

        return atlases;
    }

    //Groups 3D model entries that share a file-name stem into one ModelAsset per model
    private static List<ModelAsset> GroupModelAssets(IReadOnlyList<AssetEntry> entries)
    {
        var modelEntries = entries.Where(e => e.Kind == AssetKind.Model3D).ToList();
        if (modelEntries.Count == 0) { return []; }

        var previewsByName = entries
            .Where(e => e.Kind == AssetKind.Image &&
                e.EntryPath.StartsWith("Previews/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().EntryPath, StringComparer.OrdinalIgnoreCase);

        var materialsByName = entries
            .Where(e => e.Kind == AssetKind.Material)
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().EntryPath, StringComparer.OrdinalIgnoreCase);

        return modelEntries
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ModelAsset(
                group.Key,
                group.OrderBy(e => FormatRank(e.Extension)).ToList(),
                previewsByName.TryGetValue(group.Key, out var preview) ? preview : null,
                materialsByName.TryGetValue(group.Key, out var material) ? material : null))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int FormatRank(string extension)
    {
        var rank = Array.IndexOf(ModelFormatOrder, extension);
        return rank < 0 ? int.MaxValue : rank;
    }
}
