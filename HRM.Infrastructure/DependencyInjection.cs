using HRM.Application.Abstractions.Services;
using HRM.Infrastructure.Services.ExternalIds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddAuthenticationServices();

        services.AddScoped<IExternalIdService, ExternalIdServicePostgres>();

        return services;
    }
}
