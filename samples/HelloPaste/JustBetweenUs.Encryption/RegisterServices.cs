using JustBetweenUs.Encryption.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace JustBetweenUs.Encryption;

public static class RegisterServices
{
    public static IServiceCollection AddEncryption(this IServiceCollection services)
    {
        if (services == null) { throw new ArgumentNullException(nameof(services)); }
        services.AddSingleton<IEncryptionService, EncryptionService>();
        return services;
    }
}
