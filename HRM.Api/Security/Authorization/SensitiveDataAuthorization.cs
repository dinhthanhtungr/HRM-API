using HRM.Application.Commons.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HRM.Api.Security.Authorization;

internal static class SensitiveDataAuthorization
{
    internal const string PolicyName = "SensitiveData.UnrestrictedAccount";

    internal static AuthorizationPolicyBuilder RequireUnrestrictedAccount(this AuthorizationPolicyBuilder policy) =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => !context.User.IsInRole(ApplicationRoles.InternalSystems.IMSUser));
}
