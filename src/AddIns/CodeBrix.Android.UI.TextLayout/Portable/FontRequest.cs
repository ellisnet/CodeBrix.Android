using System;

namespace CodeBrix.Android.UI.TextLayout.Portable;

/// <summary>
/// One typeface request of the font source contract (IFontSourcePlatform&lt;SKTypeface&gt;.GetTypefaceAsync), normalized
/// into the key the Android font source caches by: the family source as given, the app's default family at the time
/// of the request (a change of DefaultTextFontFamily must not return the old default's typeface), the CSS weight
/// (1-1000, 0 = normal 400), italic (FontStyle Oblique or Italic) and the stretch (FontStretch 1-9, 0 = normal 5).
/// </summary>
internal readonly record struct FontRequest(string Family, string DefaultFamily, int Weight, bool Italic, int Stretch)
{
    /// <summary>FontWeights.Normal.</summary>
    internal const int NormalWeight = 400;

    /// <summary>FontStretch.Normal.</summary>
    internal const int NormalStretch = 5;

    /// <summary>Normalizes the contract's arguments.</summary>
    /// <param name="family">The FontFamily source (a name, an ms-appx:/// file with #Family, or a comma-separated list).</param>
    /// <param name="defaultFamily">The app's DefaultTextFontFamily.</param>
    /// <param name="weight">The weight (0 = normal).</param>
    /// <param name="stretch">The FontStretch value (0 = normal).</param>
    /// <param name="style">The FontStyle value (0 Normal, 1 Oblique, 2 Italic).</param>
    /// <returns>The normalized request.</returns>
    internal static FontRequest Create(string family, string defaultFamily, int weight, int stretch, int style) => new(
        family?.Trim() ?? string.Empty,
        defaultFamily?.Trim() ?? string.Empty,
        weight <= 0 ? NormalWeight : Math.Clamp(weight, 1, 1000),
        style != 0,
        stretch <= 0 ? NormalStretch : Math.Clamp(stretch, 1, 9));
}
