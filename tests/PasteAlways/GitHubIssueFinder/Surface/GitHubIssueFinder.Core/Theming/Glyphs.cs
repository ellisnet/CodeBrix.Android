namespace GitHubIssueFinder.Theming;

/// <summary>
/// The symbol-font codepoints the application draws. Every one of them was checked on a running
/// head before it was written down here, because the shipped symbols font is dense but not
/// contiguous and a codepoint it does not cover draws a visible box.
/// The page's own fixed chrome writes the same codepoints as XAML escapes; these constants are
/// for the glyphs a view model chooses at run time.
/// </summary>
public static class Glyphs
{
    /// <summary>An open issue, an open pull request and the application mark: an open ring.</summary>
    public const string OpenIssue = "\uF138";

    /// <summary>A closed issue: a check.</summary>
    public const string ClosedIssue = "\uF13E";

    /// <summary>A merged pull request: a check.</summary>
    public const string MergedPullRequest = "\uF13E";

    /// <summary>An issue closed as not planned: a circle with a slash through it.</summary>
    public const string NotPlanned = "\uF140";

    /// <summary>A pull request closed without being merged: a cross.</summary>
    public const string ClosedPullRequest = "\uF13D";

    /// <summary>A draft pull request: a filled circle.</summary>
    public const string DraftPullRequest = "\uF137";

    /// <summary>The wait for a rate-limit reset: a clock.</summary>
    public const string Waiting = "\uE823";

    /// <summary>A failure: a cross in a circle.</summary>
    public const string Error = "\uEA39";
}
