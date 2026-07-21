using Microsoft.Extensions.DependencyInjection;

namespace HRM.Api.Security.Authorization;

internal static class AuthorizationPolicyRegistration
{
    internal static IServiceCollection AddApplicationAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPlmPolicies();
        });

        return services;
    }
}
