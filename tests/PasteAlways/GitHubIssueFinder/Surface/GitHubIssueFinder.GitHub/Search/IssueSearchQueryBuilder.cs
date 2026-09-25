using System;
using System.Globalization;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// Builds the search query strings and request addresses a search uses. Pure text work,
/// kept apart from the service so it can be read and tested on its own.
/// </summary>
public static class IssueSearchQueryBuilder
{
    //The number of results asked for on every page, which is GitHub's maximum.
    internal const int PageSize = 100;

    /// <summary>
    /// Builds the value of the search API's q parameter, unencoded.
    /// </summary>
    /// <param name="request">What to search for.</param>
    /// <param name="repositoryFullName">
    /// A single repository in "owner/name" form to narrow the search to, used by the
    /// per-repository plan; null searches every repository the owner has.
    /// </param>
    /// <returns>The query text, for example "user:ellisnet is:open no:assignee".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">The request names no owner.</exception>
    public static string BuildQuery(IssueSearchRequest request, string repositoryFullName = null)
    {
        var owner = RequireOwner(request);

        var repository = Clean(repositoryFullName);
        var scope = repository == null ? "user:" + owner : "repo:" + repository;

        var assignee = Clean(request.Assignee);
        var assignment = assignee == null ? "no:assignee" : "assignee:" + assignee;

        //Leaving is:open out is what widens the search to closed items as well.
        return request.IncludeClosed
            ? scope + " " + assignment
            : scope + " is:open " + assignment;
    }

    /// <summary>
    /// Builds the full relative address of one page of search results, query parameters and all.
    /// </summary>
    /// <param name="request">What to search for.</param>
    /// <param name="page">The one-based page number to ask for.</param>
    /// <param name="repositoryFullName">
    /// A single repository in "owner/name" form to narrow the search to; null searches every
    /// repository the owner has.
    /// </param>
    /// <returns>The relative address, for example "search/issues?q=...&amp;per_page=100&amp;page=1".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">The request names no owner.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is below one.</exception>
    public static string BuildSearchUrl(IssueSearchRequest request, int page, string repositoryFullName = null)
    {
        if (page < 1) { throw new ArgumentOutOfRangeException(nameof(page), page, "Page numbers start at one."); }

        var query = BuildQuery(request, repositoryFullName);

        //Every reserved character is percent-escaped, spaces included, so one escaping rule
        //covers the whole parameter and the address reads the same on every platform.
        return "search/issues?q=" + Uri.EscapeDataString(query)
            + "&sort=updated&order=desc&per_page=" + PageSize.ToString(CultureInfo.InvariantCulture)
            + "&page=" + page.ToString(CultureInfo.InvariantCulture);
    }

    //The core API address that lists one owner's own repositories. It answers for an
    //organisation as readily as for a person, which is why the search owner needs no
    //separate lookup to find out which it is.
    internal static string BuildRepositoryListUrl(string owner, int page)
    {
        var cleaned = Clean(owner);
        if (cleaned == null)
        {
            throw new ArgumentException("A repository listing needs an owner login.", nameof(owner));
        }

        if (page < 1) { throw new ArgumentOutOfRangeException(nameof(page), page, "Page numbers start at one."); }

        return "users/" + Uri.EscapeDataString(cleaned) + "/repos?per_page="
            + PageSize.ToString(CultureInfo.InvariantCulture)
            + "&page=" + page.ToString(CultureInfo.InvariantCulture)
            + "&type=owner";
    }

    private static string RequireOwner(IssueSearchRequest request)
    {
        if (request == null) { throw new ArgumentNullException(nameof(request)); }

        var owner = Clean(request.Owner);
        if (owner == null)
        {
            throw new ArgumentException("A search needs the login of the owner to search.", nameof(request));
        }

        return owner;
    }

    //A login typed into a text box arrives with whatever spacing the typist left on it.
    private static string Clean(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
