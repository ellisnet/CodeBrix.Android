using System;
using System.Globalization;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// One progress report from a running search. <see cref="ToString"/> renders the sentence
/// the application shows on its status line, so a caller can bind the report directly.
/// </summary>
/// <param name="Phase">What the search is doing.</param>
/// <param name="Fetched">How many items have been handed to the caller so far.</param>
/// <param name="Total">The total GitHub reported for the query, or null while it is unknown.</param>
/// <param name="PagesFetched">How many pages have been read so far.</param>
/// <param name="WaitRemaining">How long is left of a rate-limit wait, or null when nothing is waiting.</param>
/// <param name="WaitUntil">When a rate-limit wait ends, or null when nothing is waiting.</param>
/// <param name="Search">The search rate-limit pool, or null before the first response.</param>
/// <param name="Core">The core rate-limit pool, or null before the first response.</param>
public sealed record SearchProgress(
    SearchPhase Phase,
    int Fetched,
    int? Total,
    int PagesFetched,
    TimeSpan? WaitRemaining,
    DateTimeOffset? WaitUntil,
    RateLimitSnapshot Search,
    RateLimitSnapshot Core)
{
    /// <summary>
    /// Renders this report as the one-line status sentence, for example
    /// "Fetched 300 of 1,240 · page 4". The completed sentence carries the item count only;
    /// the repository count and the elapsed time belong to the caller, which knows both.
    /// </summary>
    /// <returns>The status sentence for the current phase.</returns>
    public override string ToString()
    {
        switch (Phase)
        {
            case SearchPhase.Starting:
                return "Contacting GitHub...";

            case SearchPhase.ListingRepositories:
                return $"Listing repositories ({PagesFetched} {(PagesFetched == 1 ? "page" : "pages")} so far)";

            case SearchPhase.WaitingForQuota:
                return $"Fetched {CountText()} · {WaitText()}";

            case SearchPhase.Completed:
                return $"Done: {Number(Fetched)} items.";

            case SearchPhase.Cancelled:
                return $"Cancelled after {CountText()}.";

            case SearchPhase.Failed:
                return "Search failed.";

            default:
                return $"Fetched {CountText()} · page {PagesFetched}";
        }
    }

    private string CountText() =>
        Total.HasValue
            ? $"{Number(Fetched)} of {Number(Total.Value)}"
            : Number(Fetched);

    private string WaitText()
    {
        if (!WaitRemaining.HasValue) { return "waiting for the search quota to reset"; }
        var seconds = (int)Math.Ceiling(Math.Max(0d, WaitRemaining.Value.TotalSeconds));
        return $"waiting {seconds} s for the search quota to reset";
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
