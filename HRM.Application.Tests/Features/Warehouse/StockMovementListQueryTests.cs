using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockMovementListQueryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompleteGroupDatePagingAndSummaryMatchWindowBasedReference(bool mixing)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var at = new DateTime(2026, 11, 10);
        WarehouseMovementRow Row(string key, long id, DateTime created, DateTime groupAt, string scope = "NORMAL") => new()
        {
            CompanyId = company, MovementKey = key, LedgerId = id, CreatedAt = created, MovementAt = groupAt,
            MovementType = "NORMAL_INTERNAL", IsCanonicalMovement = true, Scope = scope,
            ProductCode = "ITEM", LotNumber = id.ToString(), StockType = 3, ShelfStockCode = scope == "CT" ? "CT.0.1" : "A.1",
            BeforeKg = 10, DeltaKg = id % 2 == 0 ? 5 : -5, AfterKg = id % 2 == 0 ? 15 : 5,
            RequestCode = "WMV.1"
        };
        var foreign = Row("foreign", 10, at, at); foreign.CompanyId = Guid.NewGuid();
        store.Seed(nameof(db.WarehouseMovementRows),
            Row("transfer", 1, at, at), Row("transfer", 2, at.AddMinutes(3), at, "CT"),
            Row("same-time", 3, at, at), Row("latest", 4, at.AddMinutes(1), at.AddMinutes(1)),
            Row("legacy", 5, at.AddDays(-1), at.AddDays(-1)), foreign);
        var user = new User(company, mixing);
        var permissions = new CurrentUserPermissionService(user);
        var referenceRows = StockMovementReader.VisibleRows(db, user, permissions).ToArray();
        var referenceGroups = referenceRows.GroupBy(x => x.MovementKey)
            .Where(g => g.Min(x => x.MovementAt) >= at && g.Min(x => x.MovementAt) <= at.AddMinutes(2))
            .OrderByDescending(g => g.Min(x => x.MovementAt)).ThenByDescending(g => g.Max(x => x.LedgerId)).ToArray();
        var handler = new GetStockMovementsQueryHandler(db, user, permissions);
        for (var page = 1; page <= referenceGroups.Length + 1; page++)
        {
            var result = await handler.Handle(new() { FromDate = at, ToDate = at.AddMinutes(2), PageSize = 1, PageNumber = page }, default);
            var expected = referenceGroups.Skip(page - 1).Take(1).Select(g => StockMovementReader.Summarize(g.ToArray())).ToArray();
            Assert.Equal(referenceGroups.Length, result.Data!.TotalCount);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result.Data.Items));
        }
        // Searching by destination scope must still inspect the group's earliest source date.
        var afterSource = await handler.Handle(new() { FromDate = at.AddMinutes(2), Scope = "CT" }, default);
        Assert.Empty(afterSource.Data!.Items);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void PageReadRechecksMixingLegAddedAfterKeySelection()
    {
        var rows = new[]
        {
            new WarehouseMovementRow { MovementKey = "selected", Scope = "NORMAL" },
            new WarehouseMovementRow { MovementKey = "selected", Scope = "CT" },
            new WarehouseMovementRow { MovementKey = "other", Scope = "NORMAL" }
        }.AsQueryable();
        Assert.Empty(StockMovementListQuery.PageRows(rows, ["selected"], false));
        Assert.Equal(2, StockMovementListQuery.PageRows(rows, ["selected"], true).Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueriesTranslateAndDoNotRequestViewWindowForPageRows(bool mixing)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var companyRows = db.ReadMovementHistory(Guid.NewGuid());
        var groups = StockMovementListQuery.Groups(companyRows, mixing);
        var pageSql = groups.Select(g => new { Key = g.Key, At = g.Min(x => x.CreatedAt), Last = g.Max(x => x.LedgerId) })
            .OrderByDescending(x => x.At).ThenByDescending(x => x.Last).Take(15).Select(x => x.Key).ToQueryString();
        var rowsSql = StockMovementListQuery.PageRows(companyRows, ["selected"], mixing).ToQueryString();
        var outerProjection = rowsSql[..rowsSql.IndexOf("FROM (", StringComparison.Ordinal)];
        // An output alias AS "MovementAt" is expected; the window source column is not.
        Assert.DoesNotContain(".\"MovementAt\"", outerProjection);
        Assert.Contains("\"CreatedAt\"", outerProjection);
        Assert.Contains("GROUP BY", pageSql);
        Assert.Contains("LIMIT", pageSql);
        Assert.Contains("selected", rowsSql);
        if (!mixing) Assert.Contains("HAVING", pageSql);
    }

    private sealed class User(Guid company, bool mixing) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool HasExplicitPermissionSet => true;
        public IReadOnlyCollection<string> Permissions => mixing ? [ApplicationPermissions.Warehouse.ViewMixingStock] : [];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
