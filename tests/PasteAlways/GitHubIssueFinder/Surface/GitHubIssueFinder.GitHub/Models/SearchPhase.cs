namespace GitHubIssueFinder.GitHub;

/// <summary>
/// What a search is doing at the moment it reported progress.
/// </summary>
public enum SearchPhase
{
    /// <summary>The first request has not come back yet.</summary>
    Starting,

    /// <summary>Pages of results are being read.</summary>
    Fetching,

    /// <summary>The search is holding off until a rate-limit pool refills.</summary>
    WaitingForQuota,

    /// <summary>The owner's repositories are being listed, ahead of searching them one at a time.</summary>
    ListingRepositories,

    /// <summary>Every page has been read.</summary>
    Completed,

    /// <summary>The caller cancelled before every page had been read.</summary>
    Cancelled,

    /// <summary>The search stopped because of an error.</summary>
    Failed,
}
