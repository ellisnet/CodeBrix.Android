using System;
using Microsoft.Extensions.DependencyInjection;
using PolyHavenBrowser.Display;
using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Services;

namespace PolyHavenBrowser;

/// <summary>
/// Registers the PolyHavenBrowser application services with the dependency-injection
/// container. Called from <c>App</c> at startup via <c>SimpleServiceResolver.CreateInstance</c>.
/// </summary>
public static class RegisterServices
{
    /// <summary>Registers the Poly Haven API client and the sample-asset service.</summary>
    public static IServiceCollection AddPolyHavenBrowser(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        services.AddPolyHavenApiClient(options =>
        {
            //Poly Haven asks API consumers to identify themselves.
            options.UserAgent = "PolyHavenBrowser/1.0 (CodeBrix.Platform sample; +https://polyhaven.com)";
        });

        services.AddSingleton<SampleAssetService>();

        //The 3D rendering backends. The selector owns the list of engines (OpenGL + Vulkan),
        //knows which are supported on the running platform, and creates them on demand for
        //the view model's rendering-engine dropdown.
        services.AddSingleton<IModelRenderEngineSelector, ModelRenderEngineSelector>();

        return services;
    }
}
