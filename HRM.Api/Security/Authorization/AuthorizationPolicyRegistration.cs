using Microsoft.Extensions.DependencyInjection;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.DeliveryOtd;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;

namespace HRM.Api.Security.Authorization;

internal static class AuthorizationPolicyRegistration
{
    internal static IServiceCollection AddApplicationAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(SensitiveDataAuthorization.PolicyName, policy => policy.RequireUnrestrictedAccount());
            options.AddPolicy(ApplicationPermissions.Reports.ViewDeliveryOtd, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.Resource is HttpContext http &&
                    DeliveryOtdAccess.CanRead(http.RequestServices.GetRequiredService<ICurrentUser>(),
                        http.RequestServices.GetRequiredService<ICurrentUserPermissionService>())));
            options.AddPlmPolicies();
            options.AddPolicy(ApplicationPermissions.Reports.ViewDeliveryFinish, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.Resource is HttpContext http &&
                    FinishDashboardScope.CanRead(http.RequestServices.GetRequiredService<ICurrentUser>(),
                        http.RequestServices.GetRequiredService<ICurrentUserPermissionService>())));
            options.AddExecutivePolicies();
            options.AddPurchasingPolicies();
        });

        return services;
    }
}
