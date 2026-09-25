namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// What the status line is currently saying, which decides its colour and whether a glyph sits in
/// front of it.
/// </summary>
public enum SearchStatusKind
{
    /// <summary>Nothing is happening.</summary>
    Idle,

    /// <summary>A search is running.</summary>
    Working,

    /// <summary>A search is holding until a rate-limit window resets.</summary>
    Waiting,

    /// <summary>A search finished normally.</summary>
    Done,

    /// <summary>A search was cancelled.</summary>
    Cancelled,

    /// <summary>A search failed.</summary>
    Failed,
}
