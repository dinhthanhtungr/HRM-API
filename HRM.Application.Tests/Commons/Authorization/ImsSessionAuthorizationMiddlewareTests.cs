using System.Security.Claims;
using HRM.Api.Security.Authorization;
using Microsoft.AspNetCore.Http;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class ImsSessionAuthorizationMiddlewareTests
{
    [Theory]
    [InlineData("GET", false, true)]
    [InlineData("HEAD", false, true)]
    [InlineData("POST", false, false)]
    [InlineData("PATCH", false, false)]
    [InlineData("DELETE", false, false)]
    [InlineData("POST", true, true)]
    [InlineData("PUT", true, false)]
    public async Task ReviewedEndpointsDefaultToReadOnly(string method, bool sessionPost, bool allowed)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "IMSUser"), new Claim(ClaimTypes.Role, "ACCUser")], "Bearer")),
        };
        context.Request.Method = method;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowImsSessionAttribute { AllowsSessionPost = sessionPost }),
            "reviewed"));
        var middleware = new ImsSessionAuthorizationMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(allowed ? StatusCodes.Status204NoContent : StatusCodes.Status403Forbidden,
            context.Response.StatusCode);
    }

    [Theory]
    [InlineData("IMSUser", false, false)]
    [InlineData("imsuser", false, false)]
    [InlineData("IMSUser", true, true)]
    [InlineData("ACCUser", false, true)]
    [InlineData("ACCUser", true, true)]
    public async Task ImsLimitsPrivilegedSessionsToReviewedEndpoints(string role, bool marked, bool forwarded)
    {
        var reachedEndpoint = false;
        var middleware = new ImsSessionAuthorizationMiddleware(_ =>
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Role, "Developer")], "Bearer")),
        };
        context.Request.Method = HttpMethods.Get;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            marked ? new EndpointMetadataCollection(new AllowImsSessionAttribute()) : EndpointMetadataCollection.Empty,
            "test"));

        await middleware.InvokeAsync(context);

        Assert.Equal(marked || !forwarded ? "no-store" : "", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(forwarded, reachedEndpoint);
        Assert.Equal(forwarded ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
