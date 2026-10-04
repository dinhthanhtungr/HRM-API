using HRM.Application.Commons.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HRM.Api.Security.Authorization;

internal static class ExecutiveAuthorizationPolicies
{
    internal static void AddExecutivePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ExecutivePolicies.ViewSampleRequestPricingOverview, policy =>
            policy.RequireRole(ApplicationRoleSets.Pricing.Managers));

        options.AddPolicy(ExecutivePolicies.ManageProductPricingReview, policy =>
            policy.RequireRole(ApplicationRoleSets.Pricing.Managers));
    }
}
