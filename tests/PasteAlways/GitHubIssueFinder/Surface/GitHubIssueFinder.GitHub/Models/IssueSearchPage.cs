using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// One page of search results as it arrives from GitHub. A search yields these one at a time
/// so the application can show rows while the rest of the pages are still on their way.
/// </summary>
public sealed class IssueSearchPage
{
    /// <summary>The items on this page, in the order GitHub returned them.</summary>
    public IReadOnlyList<IssueItem> Items { get; set; }

    /// <summary>The one-based page number this page answers.</summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// The total number of matches GitHub reported for the query, or null when the count
    /// is not known for this page.
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// True when GitHub reported that the search timed out on its side and the results
    /// may be partial.
    /// </summary>
    public bool IncompleteResults { get; set; }

    /// <summary>
    /// The repository this page was searched in, in "owner/name" form, when the search fell
    /// back to searching one repository at a time; null for a whole-owner search.
    /// </summary>
    public string RepositoryFullName { get; set; }
}
