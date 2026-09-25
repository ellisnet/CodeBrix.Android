using System;
using System.Collections.Generic;
using System.Threading;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// Searches a GitHub owner's public repositories for issues and pull requests, yielding
/// results a page at a time so a caller can show rows while the rest is still arriving.
/// </summary>
public interface IGitHubIssueSearchService
{
    /// <summary>
    /// Runs the search, yielding each page of results as it arrives.
    /// </summary>
    /// <param name="request">What to search for.</param>
    /// <param name="progress">Receives a report at every step, including rate-limit waits; optional.</param>
    /// <param name="cancellationToken">Stops the search between pages.</param>
    /// <returns>The pages of results, in the order they were read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="GitHubApiException">GitHub refused the call or answered with something unusable.</exception>
    IAsyncEnumerable<IssueSearchPage> SearchAsync(IssueSearchRequest request,
        IProgress<SearchProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The search rate-limit pool as of the last response, or null before the first response.
    /// </summary>
    RateLimitSnapshot LastSearchRateLimit { get; }

    /// <summary>
    /// The core rate-limit pool as of the last response, or null before the first response.
    /// </summary>
    RateLimitSnapshot LastCoreRateLimit { get; }
}
