using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Policy;

/// <summary>A Material 3 type role (the metrics CodeBrix.Android gives a framework TextBlock style).</summary>
/// <param name="Role">The Material role name (e.g. "bodyMedium").</param>
/// <param name="Size">The font size in sp.</param>
/// <param name="LineHeight">The line height in sp.</param>
/// <param name="Weight">The font weight (400 regular, 500 medium).</param>
/// <param name="Tracking">The letter spacing in sp.</param>
internal sealed record TypeRole(string Role, double Size, double LineHeight, int Weight, double Tracking)
{
    /// <summary>The letter spacing in em (Android's TextView.LetterSpacing unit).</summary>
    internal double TrackingEm => Size > 0 ? Tracking / Size : 0;
}

/// <summary>
/// The type scale (plan 2.11): the framework's built-in TextBlock styles map onto Material 3 type roles, and the
/// unit rule for FontSize - sp (the user's font scale applies) when IsTextScaleFactorEnabled (the WinUI default,
/// true), dp otherwise (D-O4 proposal). An explicit FontSize (334 of 396 corpus TextBlocks) and the app's default
/// font family win over the role; the role supplies size, line height, weight and tracking only where the
/// framework style set them.
/// </summary>
internal static class TypeScale
{
    private static readonly Dictionary<string, TypeRole> _roles = new(StringComparer.Ordinal)
    {
        ["CaptionTextBlockStyle"] = new TypeRole("labelSmall", 11, 16, 500, 0.5),
        ["BodyTextBlockStyle"] = new TypeRole("bodyMedium", 14, 20, 400, 0.25),
        ["BodyStrongTextBlockStyle"] = new TypeRole("titleSmall", 14, 20, 500, 0.1),
        ["SubtitleTextBlockStyle"] = new TypeRole("titleMedium", 16, 24, 500, 0.15),
        ["TitleTextBlockStyle"] = new TypeRole("headlineSmall", 24, 32, 400, 0),
        ["TitleLargeTextBlockStyle"] = new TypeRole("headlineMedium", 28, 36, 400, 0),
        ["DisplayTextBlockStyle"] = new TypeRole("displaySmall", 36, 44, 400, 0),
    };

    /// <summary>True (default) to give the framework's TextBlock styles their Material type roles.</summary>
    internal static bool MapBuiltInStyles { get; set; } = true;

    /// <summary>
    /// A font scale to use instead of the system's (null = the system's): a diagnostics and test knob, since
    /// only the system settings change the real one.
    /// </summary>
    internal static double? FontScaleOverride { get; set; }

    /// <summary>The framework style keys that have a role.</summary>
    internal static IEnumerable<string> StyleKeys => _roles.Keys;

    /// <summary>The role of a framework TextBlock style key, or null.</summary>
    /// <param name="styleKey">The style's resource key.</param>
    /// <returns>The role.</returns>
    internal static TypeRole RoleOf(string styleKey) => styleKey != null && _roles.TryGetValue(styleKey, out var role) ? role : null;

    /// <summary>
    /// The pixel size of a font: FontSize DIPs times the density, times the user's font scale when
    /// <paramref name="isTextScaleFactorEnabled"/> (sp), or not (dp).
    /// </summary>
    /// <param name="fontSize">The element's FontSize (DIPs).</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <param name="fontScale">The user's font scale (1 = default).</param>
    /// <param name="isTextScaleFactorEnabled">The element's IsTextScaleFactorEnabled.</param>
    /// <returns>The size in pixels.</returns>
    internal static float TextSizePx(double fontSize, double density, double fontScale, bool isTextScaleFactorEnabled)
    {
        var scale = isTextScaleFactorEnabled && fontScale > 0 && !double.IsNaN(fontScale) ? fontScale : 1.0;
        return (float)(fontSize * density * scale);
    }

    /// <summary>The effective font scale: the override, else the system's.</summary>
    /// <param name="systemFontScale">The system's font scale.</param>
    /// <returns>The scale to use.</returns>
    internal static double EffectiveFontScale(double systemFontScale) =>
        FontScaleOverride ?? (systemFontScale > 0 && !double.IsNaN(systemFontScale) ? systemFontScale : 1.0);

    /// <summary>Puts every switch back to its default.</summary>
    internal static void Reset()
    {
        MapBuiltInStyles = true;
        FontScaleOverride = null;
    }
}
