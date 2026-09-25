namespace GitHubIssueFinder.GitHub;

/// <summary>
/// Tells a plain issue apart from a pull request. The GitHub search API returns both
/// from an issue search and marks a pull request with its own member in the response.
/// </summary>
public enum IssueKind
{
    /// <summary>A plain issue.</summary>
    Issue,

    /// <summary>A pull request.</summary>
    PullRequest,
}
