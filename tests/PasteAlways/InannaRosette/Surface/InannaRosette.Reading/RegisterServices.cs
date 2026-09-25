using System;
using InannaRosette.Reading.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InannaRosette.Reading;

/// <summary>
/// Dependency-injection registration for the InannaRosette reading library.
/// </summary>
public static class RegisterServices
{
    /// <summary>
    /// Registers everything a screen needs to turn a laid-out rosette into finished work: the
    /// <see cref="IReadingInterpreter"/> that writes the prose, the <see cref="IReadingSerializer"/>
    /// that saves and reloads a reading as JSON, and the <see cref="IPdfReportBuilder"/> that
    /// renders the printable report. All three are stateless, so all three are singletons.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddReading(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReadingInterpreter, ReadingInterpreter>();
        services.TryAddSingleton<IReadingSerializer, ReadingSerializer>();

        // The builder's only constructor parameter is optional (the page-size and author
        // options), which the container cannot supply, so it is built by hand.
        services.TryAddSingleton<IPdfReportBuilder>(_ => new PdfReportBuilder());

        return services;
    }
}
