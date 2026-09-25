namespace GitHubIssueFinder.Theming;

/// <summary>
/// The colour schemes the user can choose between. <see cref="SystemDefault"/> is not a scheme
/// of its own: it resolves to <see cref="Light"/> or <see cref="Dark"/> from what the operating
/// system prefers, and follows the operating system while it stays selected. Every other value
/// overrides the operating system completely.
/// </summary>
public enum ColorScheme
{
    /// <summary>Follow the operating system, which means Light or Dark.</summary>
    SystemDefault,

    /// <summary>The light scheme.</summary>
    Light,

    /// <summary>The light scheme with the contrast pushed up.</summary>
    LightHighContrast,

    /// <summary>The dark scheme.</summary>
    Dark,

    /// <summary>The dark scheme on a softer, lighter ground.</summary>
    DarkDimmed,
}
