using CodeBrix.Platform.Simple;
using GitHubIssueFinder.Theming;

namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// One entry of the scheme picker. The system entry names the scheme it currently resolves to, so
/// its text depends on the operating system's preference; when that changes the owner puts a new
/// entry in its place rather than renaming this one, because a picker's closed face reads its
/// item once and never listens for a rename.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class ColorSchemeOptionViewModel : SimpleViewModel
{
    /// <summary>
    /// Builds one picker entry.
    /// </summary>
    /// <param name="scheme">The choice this entry stands for.</param>
    /// <param name="osPrefersDark">True when the operating system prefers a dark appearance.</param>
    public ColorSchemeOptionViewModel(ColorScheme scheme, bool osPrefersDark)
    {
        Scheme = scheme;
        DisplayName = ColorSchemes.DisplayName(scheme, osPrefersDark);
    }

    /// <summary>The choice this entry stands for.</summary>
    public ColorScheme Scheme { get; }

    /// <summary>The text the picker shows, for example "System default (Dark)".</summary>
    public string DisplayName { get; }

    /// <summary>The picker shows this entry by its display name.</summary>
    /// <returns>The display name.</returns>
    public override string ToString() => DisplayName;
}
