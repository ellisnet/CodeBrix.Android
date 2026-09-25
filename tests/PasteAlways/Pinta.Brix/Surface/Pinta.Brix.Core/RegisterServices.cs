using Microsoft.Extensions.DependencyInjection;
using Pinta.Brix.Services;
using System;

namespace Pinta.Brix;

/// <summary>
/// Registers the Pinta.Brix application services with the dependency-injection
/// container. Called from <c>App</c> at startup via
/// <c>SimpleServiceResolver.CreateInstance</c>.
/// </summary>
public static class RegisterServices
{
    /// <summary>Registers the shell's window-close coordination service.</summary>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so registrations can be chained.</returns>
    public static IServiceCollection AddPintaBrix(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }

        services.AddSingleton<IShellCloseService, ShellCloseService>();

        return services;
    }
}
