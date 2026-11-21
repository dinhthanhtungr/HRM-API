using System.Security.Claims;
using HRM.Application.Commons.Authorization;
using Microsoft.AspNetCore.Http;

namespace HRM.Api.Security.Authorization;

internal sealed class ImsSessionAuthorizationMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var access = endpoint?.Metadata.GetMetadata<AllowImsSessionAttribute>();
        if (access is not null)
        {
            // Both response variants are account-specific, including the unrestricted one.
            context.Response.Headers.CacheControl = "no-store";
        }

        var method = context.Request.Method;
        var allowed = access is not null && (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            (access.AllowsSessionPost && HttpMethods.IsPost(method)));
        if (endpoint is not null && !allowed && HasImsRole(context.User))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        }

        return next(context);
    }

    internal static bool HasImsRole(ClaimsPrincipal principal) =>
        principal.Identities.Any(identity => identity.IsAuthenticated &&
            identity.HasClaim(claim => claim.Type == identity.RoleClaimType &&
                string.Equals(claim.Value, ApplicationRoles.InternalSystems.IMSUser, StringComparison.OrdinalIgnoreCase)));
}
