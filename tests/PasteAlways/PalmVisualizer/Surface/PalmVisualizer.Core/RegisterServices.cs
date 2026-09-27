using System;
using Microsoft.Extensions.DependencyInjection;
using PalmVisualizer.Camera;
using PalmVisualizer.Rendering;
using PalmVisualizer.Vision;

namespace PalmVisualizer;

/// <summary>
/// Registers the PalmVisualizer application services with the dependency-injection
/// container. Called from <c>App</c> at startup via <c>SimpleServiceResolver.CreateInstance</c>.
/// </summary>
public static class RegisterServices
{
    /// <summary>
    /// Registers the webcam capture service, the palm tracker and the visualizer session
    /// factory - the three collaborators the view model resolves.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddPalmVisualizer(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        services.AddSingleton<IWebcamCaptureService, WebcamCaptureService>();
        services.AddSingleton<IPalmTracker, PalmTracker>();
        services.AddSingleton<IVisualizerSessionFactory, VisualizerSessionFactory>();

        return services;
    }
}
