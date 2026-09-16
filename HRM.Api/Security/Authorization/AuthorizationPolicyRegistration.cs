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
            options.AddExecutivePolicies();
            options.AddPurchasingPolicies();
        });

        return services;
    }
}
