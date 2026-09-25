using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The one registration extension the library offers, so an application adds the search
/// service with a single line in its composition root.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGitHubIssueSearchService"/> as a singleton, so one connection
    /// pool and one pair of rate-limit throttles serve the whole application.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="options">
    /// How the service identifies itself and how it paces its calls; null takes the defaults.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddGitHubIssueSearch(this IServiceCollection services,
        GitHubSearchOptions options = null)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        var effective = options ?? new GitHubSearchOptions();
        services.AddSingleton<IGitHubIssueSearchService>(_ => new GitHubIssueSearchService(effective));
        return services;
    }
}
