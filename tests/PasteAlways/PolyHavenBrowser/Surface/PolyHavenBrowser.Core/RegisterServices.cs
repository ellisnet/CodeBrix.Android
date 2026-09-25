using System;
using Microsoft.Extensions.DependencyInjection;
using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Rendering;
using PolyHavenBrowser.Services;

namespace PolyHavenBrowser;

/// <summary>
/// Registers the PolyHavenBrowser application services with the dependency-injection
/// container. Called from <c>App</c> at startup via <c>SimpleServiceResolver.CreateInstance</c>.
/// </summary>
public static class RegisterServices
{
    /// <summary>
    /// Registers the Poly Haven API client, the model loader, the catalog service, the
    /// download service and the document backdrop service.
    /// </summary>
    public static IServiceCollection AddPolyHavenBrowser(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        services.AddPolyHavenApiClient(options =>
        {
            //Poly Haven asks API consumers to identify themselves.
            options.UserAgent = "PolyHavenBrowser/1.0 (CodeBrix.Platform sample; +https://polyhaven.com)";
        });

        //The view model asks for the interface, so the loading technology can be swapped or
        //mocked without touching it. The loader holds no state between calls.
        services.AddSingleton<IModelLoader, GltfModelLoader>();

        services.AddSingleton<ModelCatalogService>();
        services.AddSingleton<ModelDownloadService>();
        services.AddSingleton<DocumentBackdropService>();

        return services;
    }
}
