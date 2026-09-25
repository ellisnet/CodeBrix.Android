namespace GitHubIssueFinder.GitHub;

/// <summary>
/// One label attached to an issue or pull request, carrying the colour GitHub shows for it
/// so the application can draw the same pill the user sees on the website.
/// </summary>
public sealed class IssueLabel
{
    /// <summary>The label text, for example "bug".</summary>
    public string Name { get; set; }

    /// <summary>
    /// The label colour as six hexadecimal digits with no leading hash, for example "d73a4a".
    /// Empty or unparseable when GitHub supplies no colour.
    /// </summary>
    public string ColorHex { get; set; }

    /// <summary>The label description, or null when the label has none.</summary>
    public string Description { get; set; }
}
