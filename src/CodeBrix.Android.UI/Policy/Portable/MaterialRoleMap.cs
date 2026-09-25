using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Policy;

/// <summary>How the theme bridge writes a Material colour role into a Fluent key.</summary>
internal enum RoleWrite
{
    /// <summary>
    /// The framework's brush instance is re-coloured in place, so every element that already resolved it (and
    /// every alias of it) repaints at once.
    /// </summary>
    InPlace,

    /// <summary>
    /// The key gets a brush of its own in the framework theme dictionary (its Fluent brush is shared with other
    /// keys that must keep their colour); the handlers that read the key pick it up on their next refresh.
    /// </summary>
    OwnBrush,
}

/// <summary>One curated Fluent key and the Material 3 colour role the theme bridge writes into it.</summary>
/// <param name="Key">The Fluent resource key.</param>
/// <param name="Role">The Material theme attribute (e.g. "colorPrimary").</param>
/// <param name="Alpha">The opacity applied to the role colour (1 = opaque).</param>
/// <param name="Overlay">An optional second role laid over the first (a state layer), or null.</param>
/// <param name="OverlayAlpha">The opacity of the overlay role.</param>
/// <param name="Write">How the colour is written.</param>
internal sealed record MaterialRoleEntry(string Key, string Role, double Alpha, string Overlay, double OverlayAlpha, RoleWrite Write);

/// <summary>
/// The theme bridge's curated key set (plan 2.11 (a), D-P14): Material 3 colour roles written into the Fluent
/// keys that decide an unthemed app's look - the accent family, text, backgrounds, strokes and the default /
/// accent Button families (default Button = tonal: secondaryContainer; AccentButtonStyle = filled: primary).
/// Keys an app defines itself are never touched (an app key is a different brush in the app's dictionary, which
/// shadows the framework's), so the app's scheme always wins; dynamic colour (the wallpaper palette, API 31+)
/// only ever reaches keys the app does not set.
/// </summary>
internal static class MaterialRoleMap
{
    private static readonly IReadOnlyList<MaterialRoleEntry> _entries = Build();

    /// <summary>The curated entries.</summary>
    internal static IReadOnlyList<MaterialRoleEntry> Entries => _entries;

    /// <summary>The Material attribute names the entries read.</summary>
    /// <returns>The distinct role names.</returns>
    internal static IReadOnlyCollection<string> Roles()
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            roles.Add(entry.Role);
            if (entry.Overlay != null)
            {
                roles.Add(entry.Overlay);
            }
        }

        return roles;
    }

    /// <summary>
    /// The ARGB colour of an entry from its role colours: the role with the entry's opacity, with the overlay role
    /// laid over it at its opacity (Material state layers).
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="roles">The role colours (ARGB) by attribute name.</param>
    /// <returns>The colour, or null when a role is missing.</returns>
    internal static int? ColorOf(MaterialRoleEntry entry, IReadOnlyDictionary<string, int> roles)
    {
        if (entry == null || roles == null || !roles.TryGetValue(entry.Role, out var baseColor))
        {
            return null;
        }

        var color = baseColor;
        if (entry.Overlay != null)
        {
            if (!roles.TryGetValue(entry.Overlay, out var overlay))
            {
                return null;
            }

            color = Blend(color, overlay, entry.OverlayAlpha);
        }

        return WithAlpha(color, entry.Alpha);
    }

    /// <summary>Lays <paramref name="top"/> at <paramref name="alpha"/> over <paramref name="bottom"/> (opaque result when the bottom is opaque).</summary>
    /// <param name="bottom">The ARGB underneath.</param>
    /// <param name="top">The ARGB on top.</param>
    /// <param name="alpha">The top's opacity, 0-1.</param>
    /// <returns>The blended ARGB.</returns>
    internal static int Blend(int bottom, int top, double alpha)
    {
        alpha = Math.Clamp(alpha, 0, 1);
        int Channel(int shift) => (int)Math.Round((((bottom >> shift) & 0xFF) * (1 - alpha)) + (((top >> shift) & 0xFF) * alpha));
        var a = (bottom >> 24) & 0xFF;
        return (a << 24) | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }

    /// <summary>The colour with its alpha multiplied by <paramref name="alpha"/>.</summary>
    /// <param name="argb">The ARGB colour.</param>
    /// <param name="alpha">The opacity factor, 0-1.</param>
    /// <returns>The ARGB colour.</returns>
    internal static int WithAlpha(int argb, double alpha)
    {
        if (alpha >= 1)
        {
            return argb;
        }

        var a = (int)Math.Round(((argb >> 24) & 0xFF) * Math.Clamp(alpha, 0, 1));
        return (a << 24) | (argb & 0x00FFFFFF);
    }

    private static IReadOnlyList<MaterialRoleEntry> Build()
    {
        var list = new List<MaterialRoleEntry>();
        void InPlace(string key, string role, double alpha = 1) => list.Add(new MaterialRoleEntry(key, role, alpha, null, 0, RoleWrite.InPlace));
        void Own(string key, string role, double alpha = 1, string overlay = null, double overlayAlpha = 0) => list.Add(new MaterialRoleEntry(key, role, alpha, overlay, overlayAlpha, RoleWrite.OwnBrush));

        // Accent family <- primary / onPrimary.
        InPlace("AccentFillColorDefaultBrush", "colorPrimary");
        InPlace("AccentFillColorSecondaryBrush", "colorPrimary", 0.9);
        InPlace("AccentFillColorTertiaryBrush", "colorPrimary", 0.8);
        InPlace("AccentTextFillColorPrimaryBrush", "colorPrimary");
        InPlace("AccentTextFillColorSecondaryBrush", "colorPrimary");
        InPlace("AccentTextFillColorTertiaryBrush", "colorPrimary");
        InPlace("TextOnAccentFillColorPrimaryBrush", "colorOnPrimary");
        InPlace("TextOnAccentFillColorSecondaryBrush", "colorOnPrimary", 0.7);
        InPlace("SystemControlHighlightAccentBrush", "colorPrimary");

        // Text <- onSurface / onSurfaceVariant.
        InPlace("TextFillColorPrimaryBrush", "colorOnSurface");
        InPlace("TextFillColorSecondaryBrush", "colorOnSurfaceVariant");
        InPlace("TextFillColorTertiaryBrush", "colorOnSurfaceVariant", 0.8);
        InPlace("TextFillColorDisabledBrush", "colorOnSurface", 0.38);

        // Backgrounds <- surface and the surface containers.
        InPlace("ApplicationPageBackgroundThemeBrush", "colorSurface");
        InPlace("SolidBackgroundFillColorBaseBrush", "colorSurface");
        InPlace("SolidBackgroundFillColorSecondaryBrush", "colorSurfaceContainerLow");
        InPlace("SolidBackgroundFillColorTertiaryBrush", "colorSurfaceContainer");
        InPlace("SolidBackgroundFillColorQuarternaryBrush", "colorSurfaceContainerHigh");
        InPlace("CardBackgroundFillColorDefaultBrush", "colorSurfaceContainerLow");

        // Strokes <- outline / outlineVariant.
        InPlace("ControlStrokeColorDefaultBrush", "colorOutlineVariant");
        InPlace("ControlStrongStrokeColorDefaultBrush", "colorOutline");
        InPlace("CardStrokeColorDefaultBrush", "colorOutlineVariant");
        InPlace("DividerStrokeColorDefaultBrush", "colorOutlineVariant");
        InPlace("SurfaceStrokeColorDefaultBrush", "colorOutlineVariant");
        InPlace("SystemControlForegroundBaseLowBrush", "colorOutlineVariant");

        // The default Button is a tonal Material button (D-P14): secondaryContainer with onSecondaryContainer,
        // hover / pressed state layers of 8 % / 10 %, no stroke; disabled = onSurface at 12 % / 38 %.
        Own("ButtonBackground", "colorSecondaryContainer");
        Own("ButtonBackgroundPointerOver", "colorSecondaryContainer", 1, "colorOnSecondaryContainer", 0.08);
        Own("ButtonBackgroundPressed", "colorSecondaryContainer", 1, "colorOnSecondaryContainer", 0.10);
        Own("ButtonBackgroundDisabled", "colorOnSurface", 0.12);
        Own("ButtonForeground", "colorOnSecondaryContainer");
        Own("ButtonForegroundPointerOver", "colorOnSecondaryContainer");
        Own("ButtonForegroundPressed", "colorOnSecondaryContainer");
        Own("ButtonForegroundDisabled", "colorOnSurface", 0.38);
        Own("ButtonBorderBrush", "colorSecondaryContainer");
        Own("ButtonBorderBrushPointerOver", "colorSecondaryContainer", 1, "colorOnSecondaryContainer", 0.08);
        Own("ButtonBorderBrushPressed", "colorSecondaryContainer", 1, "colorOnSecondaryContainer", 0.10);
        Own("ButtonBorderBrushDisabled", "colorOnSurface", 0.12);

        // AccentButtonStyle is a filled Material button (D-P14): primary with onPrimary.
        Own("AccentButtonBackground", "colorPrimary");
        Own("AccentButtonBackgroundPointerOver", "colorPrimary", 1, "colorOnPrimary", 0.08);
        Own("AccentButtonBackgroundPressed", "colorPrimary", 1, "colorOnPrimary", 0.10);
        Own("AccentButtonBackgroundDisabled", "colorOnSurface", 0.12);
        Own("AccentButtonForeground", "colorOnPrimary");
        Own("AccentButtonForegroundPointerOver", "colorOnPrimary");
        Own("AccentButtonForegroundPressed", "colorOnPrimary");
        Own("AccentButtonForegroundDisabled", "colorOnSurface", 0.38);
        Own("AccentButtonBorderBrush", "colorPrimary");
        Own("AccentButtonBorderBrushPointerOver", "colorPrimary", 1, "colorOnPrimary", 0.08);
        Own("AccentButtonBorderBrushPressed", "colorPrimary", 1, "colorOnPrimary", 0.10);
        Own("AccentButtonBorderBrushDisabled", "colorOnSurface", 0.12);
        return list;
    }
}
