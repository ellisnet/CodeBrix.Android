using System;
using System.Collections.Generic;
using System.Linq;

namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// The parsed catalog of one Kenney asset bundle (.zip): its display identity, license,
/// every file entry, plus the derived spritesheet atlases and grouped 3D models.
/// </summary>
public class AssetBundle
{
    /// <summary>
    /// Creates a parsed bundle catalog.
    /// </summary>
    /// <param name="zipPath">The full path of the bundle zip file on disk.</param>
    /// <param name="displayName">The bundle display name (license title or prettified file name).</param>
    /// <param name="version">The bundle version from the license header, or <c>null</c>.</param>
    /// <param name="licenseText">The full text of the bundle's License.txt, or <c>null</c>.</param>
    /// <param name="previewEntryPath">The archive path of the bundle's cover preview image, or <c>null</c>.</param>
    /// <param name="entries">Every file entry in the archive.</param>
    /// <param name="atlases">The spritesheet atlases parsed from TextureAtlas XML files.</param>
    /// <param name="modelAssets">The 3D models grouped across their format variants.</param>
    public AssetBundle(
        string zipPath,
        string displayName,
        string version,
        string licenseText,
        string previewEntryPath,
        IReadOnlyList<AssetEntry> entries,
        IReadOnlyList<SpriteAtlas> atlases,
        IReadOnlyList<ModelAsset> modelAssets)
    {
        ZipPath = zipPath ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Version = version;
        LicenseText = licenseText;
        PreviewEntryPath = previewEntryPath;
        Entries = entries ?? [];
        Atlases = atlases ?? [];
        ModelAssets = modelAssets ?? [];

        var lastSlash = ZipPath.Replace('\\', '/').LastIndexOf('/');
        FileName = lastSlash < 0 ? ZipPath : ZipPath.Substring(lastSlash + 1);

        Categories = Entries
            .Where(e => e.Category.Length > 0)
            .Select(e => e.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Gets the full path of the bundle zip file on disk.</summary>
    public string ZipPath { get; }

    /// <summary>Gets the zip file name (last path segment).</summary>
    public string FileName { get; }

    /// <summary>Gets the bundle display name (license title or prettified file name).</summary>
    public string DisplayName { get; }

    /// <summary>Gets the bundle version from the license header, or <c>null</c>.</summary>
    public string Version { get; }

    /// <summary>Gets the full text of the bundle's License.txt, or <c>null</c>.</summary>
    public string LicenseText { get; }

    /// <summary>Gets the archive path of the bundle's cover preview image, or <c>null</c>.</summary>
    public string PreviewEntryPath { get; }

    /// <summary>Gets every file entry in the archive.</summary>
    public IReadOnlyList<AssetEntry> Entries { get; }

    /// <summary>Gets the spritesheet atlases parsed from TextureAtlas XML files.</summary>
    public IReadOnlyList<SpriteAtlas> Atlases { get; }

    /// <summary>Gets the 3D models grouped across their format variants.</summary>
    public IReadOnlyList<ModelAsset> ModelAssets { get; }

    /// <summary>Gets the distinct entry categories (folder chains), sorted for display.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>Gets the number of raster image entries.</summary>
    public int ImageCount => Entries.Count(e => e.Kind == AssetKind.Image);

    /// <summary>Gets the number of grouped 3D models (not per-format entries).</summary>
    public int ModelCount => ModelAssets.Count;

    /// <summary>Gets a value indicating whether the bundle contains any 3D models.</summary>
    public bool HasModels => ModelAssets.Count > 0;

    /// <summary>Gets a value indicating whether the bundle contains any spritesheet atlases.</summary>
    public bool HasAtlases => Atlases.Count > 0;
}
