using KenneyAssetBrowser.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace KenneyAssetBrowser;

/// <summary>
/// Registers the KenneyAssetBrowser application services with the dependency-injection
/// container. Called from <c>App</c> at startup via <c>SimpleServiceResolver.CreateInstance</c>.
/// </summary>
public static class RegisterServices
{
    /// <summary>Registers the asset catalog service.</summary>
    public static IServiceCollection AddKenneyAssetBrowser(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        services.AddSingleton<AssetCatalogService>();

        return services;
    }
}
