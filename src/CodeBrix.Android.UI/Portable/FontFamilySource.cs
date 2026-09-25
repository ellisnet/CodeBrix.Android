using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// One entry of a XAML FontFamily source (the value is a comma-separated fallback list,
/// e.g. <c>"ms-appx:///Fonts/My.ttf#My Font, Segoe UI"</c>).
/// </summary>
/// <param name="Source">The entry as written (trimmed).</param>
/// <param name="AssetPath">The Android asset path of the font file, or null for a family name.</param>
/// <param name="FamilyName">The family name: the <c>#fragment</c> of a font file entry, or the name itself.</param>
internal readonly record struct FontFamilyEntry(string Source, string AssetPath, string FamilyName)
{
    /// <summary>Gets a value indicating whether the entry names a font file (an app or library asset).</summary>
    internal bool IsAsset => AssetPath != null;
}

/// <summary>
/// Parses XAML FontFamily source strings.
/// </summary>
internal static class FontFamilySource
{
    /// <summary>Splits a FontFamily source into its fallback entries, in order.</summary>
    internal static IReadOnlyList<FontFamilyEntry> Parse(string source)
    {
        var result = new List<FontFamilyEntry>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return result;
        }

        foreach (var part in source.Split(','))
        {
            var entry = part.Trim();
            if (entry.Length == 0)
            {
                continue;
            }

            string familyName = null;
            var hash = entry.IndexOf('#');
            if (hash >= 0 && hash < entry.Length - 1)
            {
                familyName = entry.Substring(hash + 1).Trim();
            }

            if (AppAssetPath.TryGetAssetPath(entry, out var assetPath))
            {
                result.Add(new FontFamilyEntry(entry, assetPath, familyName));
            }
            else
            {
                result.Add(new FontFamilyEntry(entry, null, entry.Trim('"', '\'')));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the asset path of the <c>.ttf.manifest</c> that describes the weight, style and
    /// stretch variants of a CodeBrix font package font (the manifest sits beside the font).
    /// </summary>
    internal static string GetManifestPath(string fontAssetPath)
    {
        ArgumentNullException.ThrowIfNull(fontAssetPath);
        return fontAssetPath + ".manifest";
    }
}
