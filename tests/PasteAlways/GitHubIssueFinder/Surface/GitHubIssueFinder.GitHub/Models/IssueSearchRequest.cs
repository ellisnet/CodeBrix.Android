namespace GitHubIssueFinder.GitHub;

/// <summary>
/// What to search for: one owner, an optional assignee, and whether closed items count.
/// </summary>
public sealed class IssueSearchRequest
{
    /// <summary>
    /// The GitHub login whose public repositories are searched. A user login and an
    /// organisation login are both accepted.
    /// </summary>
    public string Owner { get; set; }

    /// <summary>
    /// The login the items must be assigned to. Null or empty searches for items that
    /// have no assignee at all.
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// True to include closed issues and pull requests as well as open ones.
    /// </summary>
    public bool IncludeClosed { get; set; }
}
