using System.Diagnostics;
using System.Security.Claims;
using HRM.Api.Diagnostics;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class WarehouseMovementTimingTests
{
    [Fact]
    public async Task PageThreeIsUnchangedAndAllQueryPhasesArePresentWhenEnabled()
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseMovementRows), Enumerable.Range(1, 40).Select(id => new WarehouseMovementRow
        {
            CompanyId = company, LedgerId = id, MovementKey = $"movement-{id}", IsCanonicalMovement = true,
            MovementType = "NORMAL_OUT", Scope = "NORMAL", ProductCode = "ITEM", LotNumber = "LOT",
            DeltaKg = -1, BeforeKg = 1, AfterKg = 0, ShelfStockCode = "A.1",
            MovementAt = new DateTime(2026, 11, 18), CreatedAt = new DateTime(2026, 11, 18), RequestCode = "PRQ-1"
        }).ToArray());
        var user = new User(company);
        var handler = new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user));
        var query = new GetStockMovementsQuery { PageNumber = 3, PageSize = 15 };
        var baseline = await handler.Handle(query, default);
        using var activity = new Activity("enabled-timing").Start();
        activity.SetTag(WarehouseMovementDiagnosticTags.Enabled, true);
        var measured = await handler.Handle(query, default);
        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(measured));
        Assert.Equal(40, measured.Data!.TotalCount);
        Assert.Equal(10, measured.Data.Items.Count);
        var phases = WarehouseMovementDiagnosticTags.ValidateServerTiming(activity.GetTagItem(WarehouseMovementDiagnosticTags.ServerTiming) as string)!;
        foreach (var name in new[] { "scope", "suggestions", "count", "page", "rows", "context", "actors", "vouchers", "requests", "mapping", "handler" })
            Assert.Contains("wm_" + name + ";dur=", phases);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData("Development", false, true, false, "GET", "/api/v1/warehouse/movements", 200, true)]
    [InlineData("Production", false, true, true, "GET", "/api/v1/warehouse/movements", 200, false)]
    [InlineData("Production", true, true, true, "GET", "/api/v1/warehouse/movements", 200, true)]
    [InlineData("Production", true, true, false, "GET", "/api/v1/warehouse/movements", 200, false)]
    [InlineData("Development", false, false, false, "GET", "/api/v1/warehouse/movements", 200, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements", 400, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements", 403, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements", 500, false)]
    [InlineData("Development", false, true, true, "POST", "/api/v1/warehouse/movements", 200, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements/key", 200, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements/key/journey/1", 200, true)]
    [InlineData("Production", true, true, false, "GET", "/api/v1/warehouse/movements/key/journey/1", 200, false)]
    [InlineData("Production", true, true, true, "GET", "/api/v1/warehouse/movements/key/journey/1", 200, true)]
    [InlineData("Development", false, false, false, "GET", "/api/v1/warehouse/movements/key/journey/1", 200, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements/key/journey/0", 200, false)]
    [InlineData("Development", false, true, true, "GET", "/api/v1/warehouse/movements/key/journey/1", 403, false)]
    public async Task HeadersRespectEnvironmentOptInRoleRouteMethodAndResponse(
        string env, bool enabled, bool authenticated, bool developer, string method, string path, int status, bool expected)
    {
        var (context, response) = Create(method, path, authenticated, developer);
        var operation = path.Contains("/journey/") ? "wj" : "wm";
        var phase = operation == "wj" ? "statistics" : "count";
        context.Response.Headers["Server-Timing"] = "other;dur=7";
        using var parent = new Activity("parent").Start();
        var middleware = new WarehouseMovementRequestTimingMiddleware(async ctx =>
        {
            WarehouseMovementRequestTimingMiddleware.MarkDispatch(ctx);
            using (var timing = new WarehouseMovementQueryTiming(operation))
            {
                timing.RecordScope();
                await timing.RunAsync(phase, () => Task.FromResult(1));
                await timing.RunAsync("PRIVATE-SQL-OR-CODE", () => Task.FromResult(1));
                timing.Run("mapping", () => 1);
            }
            ctx.Response.StatusCode = status;
            await response.StartHeaders();
        }, new Environment { EnvironmentName = env }, Config(enabled));
        await middleware.InvokeAsync(context);
        Assert.Same(parent, Activity.Current);
        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.StartsWith("other;dur=7", header);
        Assert.Equal(expected, header.Contains(operation + "_total;dur="));
        if (expected)
        {
            Assert.Contains(operation + "_" + phase + ";dur=", header);
            Assert.Contains(operation + "_mapping;dur=", header);
            Assert.Contains(operation + "_handler;dur=", header);
            Assert.Contains(operation + "_pre_dispatch;dur=", header);
        }
        Assert.DoesNotContain("PRIVATE", header);
        Assert.DoesNotContain("pageNumber", header);
        Assert.DoesNotContain("pageSize", header);
    }

    [Theory]
    [InlineData("wm_count;dur=-1")]
    [InlineData("wm_count;dur=NaN")]
    [InlineData("wm_count;dur=1;desc=private")]
    [InlineData("wm_private;dur=1")]
    [InlineData("wm_count;dur=1\r\n")]
    public void RejectsUntrustedMetricPayload(string value)
        => Assert.Null(WarehouseMovementDiagnosticTags.ValidateServerTiming(value));

    [Fact]
    public async Task ConcurrentRequestsDoNotMixPhases()
    {
        async Task<string> Read(string phase)
        {
            var (context, response) = Create("GET", "/api/v1/warehouse/movements", true, true);
            var middleware = new WarehouseMovementRequestTimingMiddleware(async _ =>
            {
                using (var timing = new WarehouseMovementQueryTiming())
                    await timing.RunAsync(phase, async () => { await Task.Yield(); return 1; });
                await response.StartHeaders();
            }, new Environment(), Config(false));
            await middleware.InvokeAsync(context);
            return context.Response.Headers["Server-Timing"].ToString();
        }
        var results = await Task.WhenAll(Read("count"), Read("page"));
        Assert.Contains("wm_count", results[0]);
        Assert.DoesNotContain("wm_page", results[0]);
        Assert.Contains("wm_page", results[1]);
        Assert.DoesNotContain("wm_count", results[1]);
    }

    [Fact]
    public async Task ExceptionPropagatesAndRestoresParentActivity()
    {
        var (context, _) = Create("GET", "/api/v1/warehouse/movements", true, true);
        using var parent = new Activity("parent").Start();
        var middleware = new WarehouseMovementRequestTimingMiddleware(_ => throw new OperationCanceledException(), new Environment(), Config(false));
        await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
        Assert.Same(parent, Activity.Current);
        Assert.False(context.Response.Headers.ContainsKey("Server-Timing"));
    }

    private static IConfiguration Config(bool enabled) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { [WarehouseMovementRequestTimingMiddleware.ConfigurationKey] = enabled.ToString() }).Build();

    private static (DefaultHttpContext, Response) Create(string method, string path, bool authenticated, bool developer)
    {
        var response = new Response();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?pageNumber=3&pageSize=15");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(developer ? [new Claim(ClaimTypes.Role, ApplicationRoles.Developer)] : [], authenticated ? "test" : null));
        return (context, response);
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class User(Guid company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class Response : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }
        public void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public async Task StartHeaders()
        {
            foreach (var item in _starting.AsEnumerable().Reverse()) await item.Callback(item.State);
            HasStarted = true;
        }
    }
}
