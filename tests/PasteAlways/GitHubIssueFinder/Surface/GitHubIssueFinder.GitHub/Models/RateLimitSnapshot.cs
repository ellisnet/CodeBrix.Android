using System;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// A point-in-time view of one GitHub rate-limit pool, built from the last response's headers
/// and from the ceiling the application holds itself to.
/// </summary>
/// <param name="Limit">The pool size GitHub reports, for example 10 search calls a minute.</param>
/// <param name="Remaining">
/// How many calls may still be made: the smaller of what GitHub says is left and what the
/// application's own ceiling still allows. It reaches zero at the moment the application starts
/// waiting for the pool, which is what a display of the budget wants to show.
/// </param>
/// <param name="Ceiling">
/// The self-imposed ceiling, always at least one below <paramref name="Limit"/>, so the
/// application never spends the last call in the pool.
/// </param>
/// <param name="ResetAt">When the pool refills.</param>
public sealed record RateLimitSnapshot(int Limit, int Remaining, int Ceiling, DateTimeOffset ResetAt);
