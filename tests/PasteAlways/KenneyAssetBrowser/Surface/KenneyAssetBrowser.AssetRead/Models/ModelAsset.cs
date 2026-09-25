using System;
using System.Collections.Generic;
using System.Linq;

namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// One 3D model from a Kenney kit, grouping the format variants (GLB, OBJ, FBX, …) that
/// share a model name, plus the kit's pre-rendered preview image when one exists.
/// </summary>
public class ModelAsset
{
    /// <summary>
    /// Creates a grouped 3D model asset.
    /// </summary>
    /// <param name="name">The model name shared by the format variants.</param>
    /// <param name="variants">The <see cref="AssetKind.Model3D"/> entries for this model, one per format.</param>
    /// <param name="previewEntryPath">The archive path of the kit's preview render for this model, or <c>null</c>.</param>
    /// <param name="materialEntryPath">The archive path of the OBJ variant's .mtl material file, or <c>null</c>.</param>
    public ModelAsset(string name, IReadOnlyList<AssetEntry> variants, string previewEntryPath, string materialEntryPath)
    {
        Name = name ?? string.Empty;
        Variants = variants ?? [];
        PreviewEntryPath = previewEntryPath;
        MaterialEntryPath = materialEntryPath;
    }

    /// <summary>Gets the model name shared by the format variants.</summary>
    public string Name { get; }

    /// <summary>Gets the model entries for this model, one per format.</summary>
    public IReadOnlyList<AssetEntry> Variants { get; }

    /// <summary>Gets the archive path of the kit's preview render for this model, or <c>null</c>.</summary>
    public string PreviewEntryPath { get; }

    /// <summary>Gets the archive path of the OBJ variant's .mtl material file, or <c>null</c>.</summary>
    public string MaterialEntryPath { get; }

    /// <summary>Gets the upper-case format names of the variants (e.g. <c>GLB, OBJ, FBX</c>).</summary>
    public string FormatList =>
        string.Join(", ", Variants.Select(v => v.Extension.ToUpperInvariant()).Distinct().OrderBy(f => f, StringComparer.Ordinal));

    /// <summary>
    /// Gets the variant with the given file extension, or <c>null</c> when the model
    /// was not shipped in that format.
    /// </summary>
    /// <param name="extension">The extension without a dot, e.g. <c>glb</c>.</param>
    public AssetEntry GetVariant(string extension) =>
        Variants.FirstOrDefault(v => v.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
}
