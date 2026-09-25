using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PdfSideBySide.PdfRender.Rendering;

namespace PdfSideBySide.PdfRender;

/// <summary>
/// Dependency-injection registration for the PdfSideBySide rendering library.
/// </summary>
public static class RegisterServices
{
    /// <summary>
    /// Registers everything a screen needs to compare two PDF documents: the
    /// <see cref="IPdfComparisonFactory"/> that makes a comparison, and the
    /// <see cref="IPageRenderer"/> that rasterizes its pages. The renderer is transient because
    /// each one owns a rasterizer and a page cache that its holder disposes.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPdfRender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPdfComparisonFactory, PdfComparisonFactory>();
        services.TryAddTransient<IPageRenderer>(_ => new PageRenderer());

        return services;
    }
}
