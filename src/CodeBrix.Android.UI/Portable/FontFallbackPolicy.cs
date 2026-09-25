using System;

namespace CodeBrix.Android.UI.Portable;

/// <summary>Where a resolved typeface came from (see <see cref="FontFallbackPolicy"/>).</summary>
internal enum FontResolutionSource
{
    /// <summary>A font file named by the FontFamily itself (an app or library asset).</summary>
    FamilyAsset,

    /// <summary>
    /// The app's default text font file (FeatureConfiguration.Font.DefaultTextFontFamily): the
    /// FontFamily names no font file that loads (a family name such as Core's default "Segoe UI").
    /// </summary>
    DefaultAsset,

    /// <summary>
    /// The Android default typeface: neither the FontFamily nor the app's default text font
    /// names a font file that loads (the app configured no default font).
    /// </summary>
    AndroidDefault,
}

/// <summary>The outcome of <see cref="FontFallbackPolicy.Resolve"/>.</summary>
/// <param name="Source">Where the typeface comes from.</param>
/// <param name="AssetPath">The asset path that loaded (null for <see cref="FontResolutionSource.AndroidDefault"/>).</param>
internal readonly record struct FontResolution(FontResolutionSource Source, string AssetPath);

/// <summary>
/// The CodeBrix font rule on Android: never fall back to a system font. A FontFamily that
/// names a font file (ms-appx, including the CodeBrix font packages) loads that file; a
/// family NAME - including Core's default "Segoe UI" - resolves to the app's configured
/// default text font file (every CodeBrix app sets FeatureConfiguration.Font.
/// DefaultTextFontFamily to one), so measurement and the native text views use the same
/// face as the desktop heads. Only an app that configured no default font file at all gets
/// the Android default typeface (and the caller logs one warning naming the rule).
/// </summary>
internal static class FontFallbackPolicy
{
    /// <summary>
    /// Resolves a FontFamily source. <paramref name="tryLoadAsset"/> is asked for each font-file
    /// entry, in order (the family's own entries first, then the default family's), and
    /// returns true when that asset loaded.
    /// </summary>
    /// <param name="familySource">The FontFamily source (a comma-separated fallback list).</param>
    /// <param name="defaultFamilySource">The app's default text font family (FeatureConfiguration.Font.DefaultTextFontFamily).</param>
    /// <param name="tryLoadAsset">Loads one asset path; true on success.</param>
    internal static FontResolution Resolve(string familySource, string defaultFamilySource, Func<string, bool> tryLoadAsset)
    {
        ArgumentNullException.ThrowIfNull(tryLoadAsset);

        foreach (var entry in FontFamilySource.Parse(familySource))
        {
            if (entry.IsAsset && tryLoadAsset(entry.AssetPath))
            {
                return new FontResolution(FontResolutionSource.FamilyAsset, entry.AssetPath);
            }
        }

        if (!string.Equals(familySource, defaultFamilySource, StringComparison.Ordinal))
        {
            foreach (var entry in FontFamilySource.Parse(defaultFamilySource))
            {
                if (entry.IsAsset && tryLoadAsset(entry.AssetPath))
                {
                    return new FontResolution(FontResolutionSource.DefaultAsset, entry.AssetPath);
                }
            }
        }

        return new FontResolution(FontResolutionSource.AndroidDefault, null);
    }
}
