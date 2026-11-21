using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class WarehouseReadOnlyFactAttribute : FactAttribute
{
    public WarehouseReadOnlyFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")))
            Skip = "Opt-in SELECT-only verification against an existing database; no setup, seed or schema changes.";
    }
}

public sealed class StockMovementReadOnlyPostgresTests
{
    [WarehouseReadOnlyFact]
    public async Task ExistingViewSupportsFilteredPagingAndCompleteDetail()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "WarehouseMovementReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync();
        await using (var check = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await check.ExecuteScalarAsync());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
        await using var db = new ApplicationDbContext(options);
        var sample = await db.WarehouseMovementRows.AsNoTracking().OrderByDescending(x => x.MovementAt).FirstOrDefaultAsync();
        Assert.NotNull(sample);
        var user = new User(sample.CompanyId);
        var permissions = new CurrentUserPermissionService(user);
        var list = new GetStockMovementsQueryHandler(db, user, permissions);
        foreach (var category in new string?[] { null, "ToMixing", "InventoryCheck", "Adjustment" })
        {
            var result = await list.Handle(new() { PageSize = 3, BusinessType = category, FromDate = new DateTime(2026, 1, 1) }, default);
            Assert.True(result.Success);
            Assert.NotEmpty(result.Data!.Items);
            foreach (var item in result.Data.Items)
            {
                var detail = await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new(item.MovementKey), default);
                Assert.True(detail.Success);
                Assert.Equal(detail.Data!.Movement.MovementAt, item.MovementAt);
                Assert.Equal(item.LedgerCount, detail.Data!.Lines.Count);
                Assert.Equal(item.NetKg, detail.Data.Lines.Sum(x => x.DeltaKg));
                Assert.Equal(item.Goods, detail.Data.Movement.Goods);
                Assert.Equal(item.CreatedByNames, detail.Data.Movement.CreatedByNames);
                Assert.Equal(item.ReferenceCodes, detail.Data.Movement.ReferenceCodes);
                if (item.LedgerCount == 1)
                {
                    Assert.Equal(detail.Data.Lines[0].BeforeKg, item.BeforeKg);
                    Assert.Equal(detail.Data.Lines[0].AfterKg, item.AfterKg);
                }
                else
                {
                    Assert.Null(item.BeforeKg);
                    Assert.Null(item.AfterKg);
                }
            }
        }
        var keyword = sample.ProductCode;
        var search = await list.Handle(new() { Keyword = keyword, PageSize = 2, Scope = sample.Scope }, default);
        Assert.True(search.Success);
        Assert.NotEmpty(search.Data!.Items);
        var foreign = new User(Guid.NewGuid());
        Assert.False((await new GetStockMovementByKeyQueryHandler(db, foreign, new CurrentUserPermissionService(foreign)).Handle(new(sample.MovementKey), default)).Success);
        Assert.Empty(db.ChangeTracker.Entries());
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
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet => false;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
