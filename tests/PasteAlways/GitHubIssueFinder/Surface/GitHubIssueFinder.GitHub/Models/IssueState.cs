namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The display state of an issue or pull request, derived from the state, the state reason
/// and, for a pull request, whether it is a draft and whether it was merged.
/// </summary>
public enum IssueState
{
    /// <summary>The item is open.</summary>
    Open,

    /// <summary>The item is closed: a completed issue, or a pull request closed without merging.</summary>
    Closed,

    /// <summary>The issue was closed with the "not planned" reason.</summary>
    NotPlanned,

    /// <summary>The pull request was merged.</summary>
    Merged,

    /// <summary>The pull request is open and still a draft.</summary>
    Draft,
}
