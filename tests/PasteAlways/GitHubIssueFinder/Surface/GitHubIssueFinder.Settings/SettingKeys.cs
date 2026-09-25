namespace GitHubIssueFinder.Settings;

/// <summary>
/// The keys GitHubIssueFinder stores settings under. Every key is spelled out here so a
/// misspelling at a call site is a compile error rather than a silently missing value.
/// </summary>
public static class SettingKeys
{
    /// <summary>The GitHub user or organisation the last search ran against.</summary>
    public const string Owner = "GitHubIssueFinder.Settings.Owner";

    /// <summary>The assignee the last search ran against; empty means unassigned items.</summary>
    public const string Assignee = "GitHubIssueFinder.Settings.Assignee";

    /// <summary>Whether closed issues and pull requests are included in a search.</summary>
    public const string IncludeClosed = "GitHubIssueFinder.Settings.IncludeClosed";

    /// <summary>The colour scheme the user picked, stored by its enum name.</summary>
    public const string ColorScheme = "GitHubIssueFinder.Settings.ColorScheme";
}
