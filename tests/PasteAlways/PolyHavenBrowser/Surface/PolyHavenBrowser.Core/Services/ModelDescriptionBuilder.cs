using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Services;

/// <summary>Geometry and file facts about a downloaded model, read from its loaded glTF.</summary>
public sealed class ModelFileStats
{
    /// <summary>The total triangle count.</summary>
    public int Triangles { get; set; }

    /// <summary>The total vertex count.</summary>
    public int Vertices { get; set; }

    /// <summary>The number of renderable primitives (triangle batches).</summary>
    public int Primitives { get; set; }

    /// <summary>The number of materials.</summary>
    public int Materials { get; set; }

    /// <summary>How many of the materials carry a base-color texture.</summary>
    public int TexturedMaterials { get; set; }

    /// <summary>The total size on disk of the model's downloaded folder, in bytes.</summary>
    public long DiskBytes { get; set; }

    /// <summary>Builds the stats from a renderer-loaded model plus its download folder.</summary>
    public static ModelFileStats FromLoadedModel(LoadedModel model, string modelFolder)
    {
        if (model == null) { throw new ArgumentNullException(nameof(model)); }

        long diskBytes = 0;
        if (!string.IsNullOrWhiteSpace(modelFolder) && Directory.Exists(modelFolder))
        {
            diskBytes = new DirectoryInfo(modelFolder)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }

        return new ModelFileStats
        {
            Triangles = model.TriangleCount,
            Vertices = model.Primitives.Sum(p => p.VertexCount),
            Primitives = model.Primitives.Count,
            Materials = model.Materials.Count,
            TexturedMaterials = model.Materials.Count(m => m.BaseColorTextureRgba != null),
            DiskBytes = diskBytes,
        };
    }
}

/// <summary>
/// Synthesizes the descriptive text the app shows for a model. Poly Haven's API rarely
/// provides prose descriptions, so the catalog cell gets a short line built from the asset's
/// metadata, and the Model View gets a fuller paragraph enriched (when available) with facts
/// read from the downloaded glTF itself. A real API description, when present, always leads.
/// </summary>
public static class ModelDescriptionBuilder
{
    /// <summary>Builds the short descriptive line shown in a catalog cell.</summary>
    public static string BuildCellBlurb(PolyHavenAsset asset)
    {
        if (asset == null) { throw new ArgumentNullException(nameof(asset)); }

        if (!string.IsNullOrWhiteSpace(asset.Description)) { return asset.Description.Trim(); }

        var pieces = new List<string>();

        var category = FirstCategory(asset);
        pieces.Add(category != null
            ? $"A free {category} 3D model, ready for any PBR workflow."
            : "A free 3D model, ready for any PBR workflow.");

        var tags = (asset.Tags ?? []).Take(3).ToArray();
        if (tags.Length > 0)
        {
            pieces.Add($"Tagged {JoinWithAnd(tags)}.");
        }

        var resolution = MaxResolutionLabel(asset);
        if (resolution != null)
        {
            pieces.Add($"Textures up to {resolution}.");
        }

        return string.Join(" ", pieces);
    }

    /// <summary>
    /// Builds the full description paragraph for the Model View. When the API gave a real
    /// description it leads; the synthesized sentences follow, drawing on the asset metadata
    /// and (when provided) the stats of the downloaded glTF file.
    /// </summary>
    public static string BuildFullDescription(PolyHavenAsset asset, ModelFileStats stats)
    {
        if (asset == null) { throw new ArgumentNullException(nameof(asset)); }

        var name = string.IsNullOrWhiteSpace(asset.Name) ? asset.Id : asset.Name;
        var text = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(asset.Description))
        {
            text.Append(asset.Description.Trim());
            if (!asset.Description.Trim().EndsWith('.')) { text.Append('.'); }
            text.Append(' ');
        }

        //Opening sentence: what it is and who made it.
        var authors = JoinWithAnd((asset.Authors?.Keys ?? Enumerable.Empty<string>()).ToArray());
        var category = FirstCategory(asset);
        text.Append(category != null
            ? $"“{name}” is a free, CC0-licensed {category} 3D model from Poly Haven"
            : $"“{name}” is a free, CC0-licensed 3D model from Poly Haven");
        text.Append(string.IsNullOrEmpty(authors) ? ". " : $", created by {authors}. ");

        //Subject matter, from the tags.
        var tags = (asset.Tags ?? []).Take(5).ToArray();
        if (tags.Length > 0)
        {
            text.Append($"It is tagged {JoinWithAnd(tags)}. ");
        }

        //Facts read from the downloaded glTF itself.
        if (stats != null)
        {
            text.Append($"The downloaded glTF contains {stats.Triangles:N0} triangles across ");
            text.Append(stats.Primitives == 1 ? "a single mesh primitive" : $"{stats.Primitives:N0} mesh primitives");
            text.Append($" ({stats.Vertices:N0} vertices), with ");
            text.Append(stats.Materials == 1 ? "one material" : $"{stats.Materials:N0} materials");
            if (stats.TexturedMaterials > 0)
            {
                text.Append(stats.TexturedMaterials == 1 ? " (one textured)" : $" ({stats.TexturedMaterials:N0} textured)");
            }
            text.Append(stats.DiskBytes > 0 ? $", and takes {FormatBytes(stats.DiskBytes)} on disk. " : ". ");
        }

        var resolution = MaxResolutionLabel(asset);
        if (resolution != null)
        {
            text.Append($"Its PBR texture maps are available at resolutions up to {resolution}. ");
        }

        //Provenance and popularity.
        text.Append($"Published {asset.DatePublishedUtc.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}, ");
        text.Append($"it has been downloaded {asset.DownloadCount:N0} times");
        text.Append(asset.Donated == true ? ", and was donated to Poly Haven by its creator." : ".");

        return text.ToString();
    }

    /// <summary>Formats a download count compactly for the catalog cell (e.g. <c>12.3k</c>).</summary>
    public static string FormatCompactCount(long count) => count switch
    {
        >= 1_000_000 => (count / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (count / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Formats a byte count with a sensible unit (e.g. <c>34.2 MB</c>).</summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => (bytes / 1_073_741_824d).ToString("0.#", CultureInfo.InvariantCulture) + " GB",
        >= 1_048_576 => (bytes / 1_048_576d).ToString("0.#", CultureInfo.InvariantCulture) + " MB",
        >= 1_024 => (bytes / 1_024d).ToString("0.#", CultureInfo.InvariantCulture) + " KB",
        _ => bytes + " B",
    };

    private static string FirstCategory(PolyHavenAsset asset) =>
        asset.Categories?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim().ToLowerInvariant();

    private static string MaxResolutionLabel(PolyHavenAsset asset)
    {
        if (asset.MaxResolution == null || asset.MaxResolution.Length < 1) { return null; }

        //Poly Haven resolutions are k-multiples (1024); label them the way the site does.
        var k = asset.MaxResolution.Max() / 1024;
        return k >= 1 ? $"{k}k" : null;
    }

    private static string JoinWithAnd(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => string.Join(", ", items.Take(items.Count - 1)) + $" and {items[items.Count - 1]}",
    };
}
