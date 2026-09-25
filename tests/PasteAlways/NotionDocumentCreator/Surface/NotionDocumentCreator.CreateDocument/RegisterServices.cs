using Microsoft.Extensions.DependencyInjection;
using NotionDocumentCreator.CreateDocument.Services;
using System;

namespace NotionDocumentCreator.CreateDocument;

/// <summary>
/// Dependency-injection registration for the NotionDocumentCreator document pipeline.
/// </summary>
public static class RegisterServices
{
    /// <summary>
    /// Registers the NotionDocumentCreator document-creation services with the DI container.
    /// </summary>
    public static IServiceCollection AddCreateDocument(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }
        services.AddSingleton<INotionDocumentService, NotionDocumentService>();
        return services;
    }
}
