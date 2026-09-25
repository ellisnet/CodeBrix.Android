using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Dependency-injection registration for the Poly Haven API client.
/// </summary>
public static class PolyHavenServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IPolyHavenApiClientFactory"/> backed by
    /// <see cref="IHttpClientFactory"/> (via a named <see cref="HttpClient"/>,
    /// <see cref="DefaultPolyHavenClientFactory.HttpClientName"/>). Resolve the factory and
    /// call <see cref="IPolyHavenApiClientFactory.GetClient"/> for a client, disposing each
    /// client when finished with it.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configureOptions">An optional callback to configure the client options.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPolyHavenApiClient(
        this IServiceCollection services,
        Action<PolyHavenClientOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PolyHavenClientOptions();
        configureOptions?.Invoke(options);

        services.AddHttpClient(DefaultPolyHavenClientFactory.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.All,
            });

        services.TryAddSingleton<IPolyHavenApiClientFactory>(serviceProvider =>
            new DefaultPolyHavenClientFactory(
                serviceProvider.GetRequiredService<IHttpClientFactory>(), options));

        return services;
    }
}
