using System.Reflection;
using System.Security.Claims;
using HRM.Api.Security;
using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AuditSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class DeveloperSampleWorkMiddlewareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritesRequirePersistedDeveloperAttributionBeforeBusinessAction(bool failAudit)
    {
        var context = Context("Developer", "/api/v1/plm/sample-requests/request-id/work-schedule");
        context.Request.Method = "PATCH";
        var actorEmployee = context.User.FindFirstValue("employeeId");
        var original = context.User;
        var (sets, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        store.Seed<AuditLog>(nameof(IPLMWriteDbContext.AuditLogs));
        var database = new AuditDatabase(sets.AuditLogs, failAudit);
        using var services = new ServiceCollection().AddScoped<ApplicationDbContext>(_ => database).BuildServiceProvider();
        var service = Proxy.Create((_, company, employee) => new AuthenticatedUserDto
        {
            UserId = Guid.NewGuid(), EmployeeId = employee, CompanyId = company, Roles = ["LabAdmin"]
        });
        var executed = false;
        var middleware = new DeveloperSampleWorkMiddleware(ctx =>
        {
            Assert.Equal(1, database.Saves);
            Assert.True(ctx.User.IsInRole("LabAdmin"));
            executed = true;
            return Task.CompletedTask;
        });
        if (failAudit)
            await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, service, services.GetRequiredService<IServiceScopeFactory>()));
        else
            await middleware.InvokeAsync(context, service, services.GetRequiredService<IServiceScopeFactory>());
        Assert.Equal(!failAudit, executed);
        Assert.Same(original, context.User);
        var audit = Assert.IsType<AuditLog>(Assert.Single(store.Added));
        Assert.Equal(actorEmployee, audit.ChangedBy.ToString());
        Assert.Contains("TargetEmployeeId", audit.NewValues!.RootElement.ToString());
        Assert.Equal("DeveloperSampleWorkWriteAttempt", audit.Reason);
    }

    private sealed class AuditDatabase : ApplicationDbContext
    {
        private readonly bool _fail;
        public int Saves { get; private set; }
        public AuditDatabase(DbSet<AuditLog> audits, bool fail) : base(new DbContextOptions<ApplicationDbContext>())
        { AuditLogs = audits; _fail = fail; }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            return _fail ? throw new InvalidOperationException("Audit persistence failed") : Task.FromResult(1);
        }
    }

    [Theory]
    [InlineData("SaleUser", "/api/v1/plm/sample-requests")]
    [InlineData("LabUser", "/api/v1/plm/formulas")]
    [InlineData("Developer", "/api/v1/auth/me")]
    [InlineData("Developer", "/api/v1/dev/sample-work-preview")]
    public async Task SpoofedHeaderAndUnsupportedRoutesAreForbidden(string role, string path)
    {
        var context = Context(role, path);
        var calls = 0;
        var service = Proxy.Create((_, _, _) => { calls++; return null; });
        var middleware = new DeveloperSampleWorkMiddleware(_ => throw new Exception("Must not execute"));
        await middleware.InvokeAsync(context, service, null!);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ImsActorOrTargetIsRejectedBeforeAnyWriteOrExecution(bool imsActor)
    {
        var context = Context("Developer", "/api/v1/plm/sample-requests");
        context.Request.Method = "PATCH";
        if (imsActor) ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, "imsuser"));
        var calls = 0;
        var service = Proxy.Create((_, company, employee) =>
        {
            calls++;
            return new AuthenticatedUserDto
            {
                UserId = Guid.NewGuid(), CompanyId = company, EmployeeId = employee, Roles = ["IMSUser", "LabUser"]
            };
        });

        await new DeveloperSampleWorkMiddleware(_ => throw new Exception("Must not execute"))
            .InvokeAsync(context, service, null!);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal(imsActor ? 0 : 1, calls);
    }

    [Fact]
    public async Task UnavailableOrOutOfCompanyEmployeeIsForbidden()
    {
        var context = Context("Developer", "/api/v1/plm/sample-requests");
        var service = Proxy.Create((_, _, _) => null);
        await new DeveloperSampleWorkMiddleware(_ => throw new Exception("Must not execute"))
            .InvokeAsync(context, service, null!);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetPrincipalReplacesDeveloperAndIsRestoredAfterRequest(bool targetHasDeveloperRole)
    {
        var context = Context("Developer", "/api/v1/plm/sample-requests");
        var original = context.User;
        var targetId = Guid.NewGuid();
        var service = Proxy.Create((actor, company, employee) => new AuthenticatedUserDto
        {
            UserId = targetId, EmployeeId = employee, CompanyId = company,
            Roles = targetHasDeveloperRole ? ["SaleUser", "Developer"] : ["SaleUser"],
            UsesDatabasePermissions = true, Permissions = ["test.read"]
        });
        var called = false;
        await new DeveloperSampleWorkMiddleware(ctx =>
        {
            called = true;
            Assert.True(ctx.User.IsInRole("SaleUser"));
            Assert.Equal(targetHasDeveloperRole, ctx.User.IsInRole("Developer"));
            Assert.Equal(targetId.ToString(), ctx.User.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("test.read", ctx.User.FindFirstValue("permission"));
            return Task.CompletedTask;
        }).InvokeAsync(context, service, null!);
        Assert.True(called);
        Assert.Same(original, context.User);
    }

    private static DefaultHttpContext Context(string role, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Request.Headers[DeveloperSampleWorkMiddleware.Header] = Guid.NewGuid().ToString();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("companyId", Guid.NewGuid().ToString()), new Claim("employeeId", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
        return context;
    }

    public class Proxy : DispatchProxy
    {
        public Func<Guid, Guid, Guid, AuthenticatedUserDto?> Resolve { get; set; } = null!;
        public static IIdentityAuthenticationService Create(Func<Guid, Guid, Guid, AuthenticatedUserDto?> resolve)
        {
            var service = Create<IIdentityAuthenticationService, Proxy>();
            ((Proxy)(object)service).Resolve = resolve;
            return service;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IIdentityAuthenticationService.ResolveDeveloperSampleWorkUserAsync)
                ? Task.FromResult(Resolve((Guid)args![0]!, (Guid)args[1]!, (Guid)args[2]!))
                : throw new NotSupportedException();
    }
}
