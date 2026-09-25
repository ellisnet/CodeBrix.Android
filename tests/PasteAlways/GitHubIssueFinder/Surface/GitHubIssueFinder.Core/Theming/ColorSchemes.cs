using System;
using System.Collections.Generic;

namespace GitHubIssueFinder.Theming;

/// <summary>
/// The four hand-authored colour schemes and the rules for choosing between them. The colours are
/// GitHub-flavoured but written for this application, so nothing here carries an attribution
/// obligation.
/// </summary>
public static class ColorSchemes
{
    /// <summary>The light scheme.</summary>
    public static ColorSchemePalette Light { get; } = new ColorSchemePalette
    {
        BaseIsDark = false,
        Canvas = 0xFFFFFFFF,
        CanvasSubtle = 0xFFF6F8FA,
        CanvasInset = 0xFFF6F8FA,
        Hairline = 0xFFD0D7DE,
        HairlineMuted = 0xFFD8DEE4,
        TextPrimary = 0xFF1F2328,
        TextSecondary = 0xFF59636E,
        TextTertiary = 0xFF6E7781,
        Accent = 0xFF0969DA,
        AccentEmphasis = 0xFF0969DA,
        AccentSubtle = 0xFFDDF4FF,
        Success = 0xFF1A7F37,
        SuccessEmphasis = 0xFF1F883D,
        SuccessEmphasisHover = 0xFF1A7F37,
        Attention = 0xFF9A6700,
        AttentionSubtle = 0xFFFFF8C5,
        Danger = 0xFFD1242F,
        DangerSubtle = 0xFFFFEBE9,
        Done = 0xFF8250DF,
        Neutral = 0xFF59636E,
        ButtonFace = 0xFFF6F8FA,
        ButtonFaceHover = 0xFFEEF1F4,
        ButtonFacePressed = 0xFFE7EBEF,
        OnEmphasis = 0xFFFFFFFF,
    };

    /// <summary>The light scheme with the contrast pushed up.</summary>
    public static ColorSchemePalette LightHighContrast { get; } = new ColorSchemePalette
    {
        BaseIsDark = false,
        Canvas = 0xFFFFFFFF,
        CanvasSubtle = 0xFFE7ECF0,
        CanvasInset = 0xFFFFFFFF,
        Hairline = 0xFF20252C,
        HairlineMuted = 0xFF88929D,
        TextPrimary = 0xFF0E1116,
        TextSecondary = 0xFF0E1116,
        TextTertiary = 0xFF4B535D,
        Accent = 0xFF0349B4,
        AccentEmphasis = 0xFF0349B4,
        AccentSubtle = 0xFFDFF7FF,
        Success = 0xFF055D20,
        SuccessEmphasis = 0xFF055D20,
        SuccessEmphasisHover = 0xFF044F1B,
        Attention = 0xFF744500,
        AttentionSubtle = 0xFFFCF7BE,
        Danger = 0xFFA0111F,
        DangerSubtle = 0xFFFFF0EE,
        Done = 0xFF622CBC,
        Neutral = 0xFF4B535D,
        ButtonFace = 0xFFE7ECF0,
        ButtonFaceHover = 0xFFD5DDE4,
        ButtonFacePressed = 0xFFC8D1D9,
        OnEmphasis = 0xFFFFFFFF,
    };

    /// <summary>The dark scheme.</summary>
    public static ColorSchemePalette Dark { get; } = new ColorSchemePalette
    {
        BaseIsDark = true,
        Canvas = 0xFF0D1117,
        CanvasSubtle = 0xFF161B22,
        CanvasInset = 0xFF010409,
        Hairline = 0xFF30363D,
        HairlineMuted = 0xFF21262D,
        TextPrimary = 0xFFE6EDF3,
        TextSecondary = 0xFF8B949E,
        TextTertiary = 0xFF6E7681,
        Accent = 0xFF4493F8,
        AccentEmphasis = 0xFF1F6FEB,
        AccentSubtle = 0xFF13233A,
        Success = 0xFF3FB950,
        SuccessEmphasis = 0xFF238636,
        SuccessEmphasisHover = 0xFF2EA043,
        Attention = 0xFFD29922,
        AttentionSubtle = 0xFF272215,
        Danger = 0xFFF85149,
        DangerSubtle = 0xFF301B1F,
        Done = 0xFFA371F5,
        Neutral = 0xFF8B949E,
        ButtonFace = 0xFF21262D,
        ButtonFaceHover = 0xFF30363D,
        ButtonFacePressed = 0xFF282E33,
        OnEmphasis = 0xFFFFFFFF,
    };

    /// <summary>The dark scheme on a softer, lighter ground.</summary>
    public static ColorSchemePalette DarkDimmed { get; } = new ColorSchemePalette
    {
        BaseIsDark = true,
        Canvas = 0xFF22272E,
        CanvasSubtle = 0xFF2D333B,
        CanvasInset = 0xFF1C2128,
        Hairline = 0xFF444C56,
        HairlineMuted = 0xFF373E47,
        TextPrimary = 0xFFADBAC7,
        TextSecondary = 0xFF768390,
        TextTertiary = 0xFF636E7B,
        Accent = 0xFF539BF5,
        AccentEmphasis = 0xFF316DCA,
        AccentSubtle = 0xFF273549,
        Success = 0xFF57AB5A,
        SuccessEmphasis = 0xFF347D39,
        SuccessEmphasisHover = 0xFF46954A,
        Attention = 0xFFC69026,
        AttentionSubtle = 0xFF37342A,
        Danger = 0xFFE5534B,
        DangerSubtle = 0xFF3F2E32,
        Done = 0xFF986EE2,
        Neutral = 0xFF768390,
        ButtonFace = 0xFF373E47,
        ButtonFaceHover = 0xFF444C56,
        ButtonFacePressed = 0xFF3D444D,
        OnEmphasis = 0xFFFFFFFF,
    };

    /// <summary>Every choice the picker offers, in the order it offers them.</summary>
    public static IReadOnlyList<ColorScheme> Choices { get; } =
    [
        ColorScheme.SystemDefault,
        ColorScheme.Light,
        ColorScheme.LightHighContrast,
        ColorScheme.Dark,
        ColorScheme.DarkDimmed,
    ];

    /// <summary>
    /// Reads the operating system's light or dark preference out of the color it says it would
    /// paint a window with, which is the form every head reports that preference in. The weights
    /// are the usual perceived-brightness ones, and a ground below the mid point is a dark one.
    /// </summary>
    /// <param name="red">The red component of the desktop's window background.</param>
    /// <param name="green">The green component of the desktop's window background.</param>
    /// <param name="blue">The blue component of the desktop's window background.</param>
    /// <returns>True when the operating system prefers a dark appearance.</returns>
    public static bool PrefersDark(byte red, byte green, byte blue)
    {
        var brightness = (red * 0.299d) + (green * 0.587d) + (blue * 0.114d);
        return brightness < 128d;
    }

    /// <summary>
    /// Turns a choice into the scheme actually drawn. Every choice but
    /// <see cref="ColorScheme.SystemDefault"/> is itself; that one becomes Light or Dark.
    /// </summary>
    /// <param name="choice">What the user picked.</param>
    /// <param name="osPrefersDark">True when the operating system prefers a dark appearance.</param>
    /// <returns>The scheme to draw, which is never <see cref="ColorScheme.SystemDefault"/>.</returns>
    public static ColorScheme Resolve(ColorScheme choice, bool osPrefersDark) =>
        choice == ColorScheme.SystemDefault
            ? (osPrefersDark ? ColorScheme.Dark : ColorScheme.Light)
            : choice;

    /// <summary>
    /// Reads the colours of a resolved scheme.
    /// </summary>
    /// <param name="resolved">A scheme that has already been through <see cref="Resolve"/>.</param>
    /// <returns>The palette for that scheme.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="resolved"/> is <see cref="ColorScheme.SystemDefault"/>, which is a choice
    /// rather than a scheme, or is not a known value.
    /// </exception>
    public static ColorSchemePalette Get(ColorScheme resolved) => resolved switch
    {
        ColorScheme.Light => Light,
        ColorScheme.LightHighContrast => LightHighContrast,
        ColorScheme.Dark => Dark,
        ColorScheme.DarkDimmed => DarkDimmed,
        _ => throw new ArgumentOutOfRangeException(nameof(resolved), resolved,
            "Resolve the choice before asking for its colours."),
    };

    /// <summary>
    /// The name the picker shows for a choice. The system entry names the scheme it currently
    /// resolves to, so the list always says what the application is actually wearing.
    /// </summary>
    /// <param name="choice">The choice being named.</param>
    /// <param name="osPrefersDark">True when the operating system prefers a dark appearance.</param>
    /// <returns>The display name, for example "Dark Dimmed" or "System default (Dark)".</returns>
    public static string DisplayName(ColorScheme choice, bool osPrefersDark) => choice switch
    {
        ColorScheme.SystemDefault => osPrefersDark ? "System default (Dark)" : "System default (Light)",
        ColorScheme.Light => "Light",
        ColorScheme.LightHighContrast => "Light High Contrast",
        ColorScheme.Dark => "Dark",
        ColorScheme.DarkDimmed => "Dark Dimmed",
        _ => choice.ToString(),
    };

    /// <summary>
    /// Reads a persisted scheme name back into a choice, falling back to
    /// <see cref="ColorScheme.SystemDefault"/> for anything unrecognised.
    /// </summary>
    /// <param name="name">The name that was stored.</param>
    /// <returns>The choice the name stands for.</returns>
    public static ColorScheme Parse(string name) =>
        Enum.TryParse(name, ignoreCase: false, out ColorScheme parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ColorScheme.SystemDefault;
}
