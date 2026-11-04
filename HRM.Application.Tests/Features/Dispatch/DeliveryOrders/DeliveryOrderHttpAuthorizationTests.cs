using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

// Isolated HTTP authorization gates: no production auth, database or mutation handler.
public sealed class DeliveryOrderHttpAuthorizationTests
{
    [Theory]
    [InlineData("PLPUUser", 200, 200, 200)]
    [InlineData("DispatchUser", 200, 200, 200)]
    [InlineData("SaleUser", 200, 403, 403)]
    [InlineData("User", 403, 403, 403)]
    public async Task RoleOnlyPrincipal_PassesOnlyPermittedHttpGates(
        string role, int read, int manage, int cancel)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("IsolatedTest")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("IsolatedTest", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role)], "IsolatedTest"));
            await next(context);
        });
        app.UseAuthorization();
        app.MapGet("/read", () => Results.Ok()).RequireAuthorization(p => p
            .RequireAuthenticatedUser().RequireRole(DeliveryOrderAccessRules.ReadRoles.Split(',')));
        app.MapPost("/manage", () => Results.Ok()).RequireAuthorization(p => p
            .RequireAuthenticatedUser().RequireRole(DeliveryOrderAccessRules.ReadRoles.Split(','))
            .RequireRole(DeliveryOrderAccessRules.ManageRoles.Split(',')));
        app.MapPost("/cancel", () => Results.Ok()).RequireAuthorization(p => p
            .RequireAuthenticatedUser().RequireRole(DeliveryOrderAccessRules.ReadRoles.Split(','))
            .RequireRole(DeliveryOrderAccessRules.CancelRoles.Split(',')));
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var readResponse = await client.GetAsync("/read");
            using var manageResponse = await client.PostAsync("/manage", null);
            using var cancelResponse = await client.PostAsync("/cancel", null);
            Assert.Equal((HttpStatusCode)read, readResponse.StatusCode);
            Assert.Equal((HttpStatusCode)manage, manageResponse.StatusCode);
            Assert.Equal((HttpStatusCode)cancel, cancelResponse.StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
