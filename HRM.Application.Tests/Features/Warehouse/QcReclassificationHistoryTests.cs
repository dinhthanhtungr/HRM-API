using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class QcReclassificationHistoryTests
{
    [Fact]
    public void QueryHasTransactionTriggerDimensionAndBalanceGuardsAndComposesBeforePaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var sql = db.ReadMovementHistory(Guid.NewGuid()).Where(x => x.ProductCode == "ITEM")
            .GroupBy(x => x.MovementKey).Select(g => new { Key = g.Key, Count = g.Count() }).Take(1).ToQueryString();
        Assert.Contains("a.xmin = b.xmin", sql);
        Assert.Contains("StockType changed OUT: 6 -> 3", sql);
        Assert.Contains("StockType changed IN: 6 -> 3", sql);
        Assert.Contains("b.\"ledgerId\" = a.\"ledgerId\" + 1", sql);
        Assert.Contains("a.\"lotNumber\" = b.\"lotNumber\"", sql);
        Assert.Contains("b.\"deltaKg\" = -a.\"deltaKg\"", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("LIMIT", sql);
    }

    [WarehouseReadOnlyFact]
    public async Task VerifiedLegacyQcPairIsOneEventAndOldLinksResolveWithoutChangingData()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "QcReclassificationReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var original = await db.WarehouseMovementRows.AsNoTracking().Where(x => x.LedgerId == 61529 || x.LedgerId == 61530).ToArrayAsync();
        Assert.Equal(2, original.Length);
        var company = original[0].CompanyId;
        var normalized = await db.ReadMovementHistory(company).Where(x => x.LedgerId == 61529 || x.LedgerId == 61530).ToArrayAsync();
        Assert.Single(normalized.Select(x => x.MovementKey).Distinct());
        Assert.All(normalized, x => Assert.Equal("QC_WAIT_TO_RAW_PASS", x.MovementType));
        Assert.Equal(original.Sum(x => x.DeltaKg), normalized.Sum(x => x.DeltaKg));
        Assert.Empty(await db.ReadMovementHistory(Guid.NewGuid()).Where(x => x.LedgerId == 61529).ToArrayAsync());
        // Original receipt/issue for this lot have vouchers and must never become a QC pair.
        var ordinary = await db.ReadMovementHistory(company).Where(x => x.LedgerId == 61310 || x.LedgerId == 61629).ToArrayAsync();
        Assert.All(ordinary, x => Assert.NotEqual("QC_WAIT_TO_RAW_PASS", x.MovementType));
        var user = new User(company);
        var permissions = new CurrentUserPermissionService(user);
        var list = new GetStockMovementsQueryHandler(db, user, permissions);
        var page = (await list.Handle(new() { Keyword = "NVL_KH_199", FromDate = new DateTime(2026, 7, 20),
            ToDate = new DateTime(2026, 7, 21), BusinessType = "QcRawPass", PageSize = 1 }, default)).Data!;
        var item = Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(2, item.LedgerCount);
        Assert.Equal("QcRawPass", item.BusinessType.Code);
        Assert.Equal(0m, item.NetKg);
        Assert.Equal(1017m, item.InKg);
        Assert.Equal(1017m, item.OutKg);
        foreach (var key in new[] { item.MovementKey, "LEGACY_LEDGER:61529", "LEGACY_LEDGER:61530" })
        {
            var detail = (await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new(key), default)).Data!;
            Assert.Equal(2, detail.Lines.Count);
            Assert.Equal("QcRawPass", detail.Movement.BusinessType.Code);
        }
        var journey = (await new GetStockJourneyQueryHandler(db, user, permissions, new HRM.Application.Features.Warehouse.Services.WarehouseVoucherBusinessReader(db, user, permissions)).Handle(new(item.MovementKey, 61529), default)).Data!;
        Assert.Single(journey.Events.Items, x => x.Movement.BusinessType.Code == "QcRawPass");
        var older = (await list.Handle(new() { Keyword = "NVL_NH_519", FromDate = new DateTime(2026, 5, 19),
            ToDate = new DateTime(2026, 5, 20), PageSize = 100 }, default)).Data!;
        Assert.Equal(3, older.TotalCount); // receipt + one QC change + one shelf transfer
        var singleQc = Assert.Single(older.Items, x => x.BusinessType.Code == "QcRawPass");
        Assert.Equal(1, singleQc.LedgerCount);
        Assert.Equal(500m, singleQc.BeforeKg);
        Assert.Equal(500m, singleQc.AfterKg);
        Assert.Equal(0m, singleQc.NetKg);
        Assert.Equal("W.0.1", Assert.Single(singleQc.FromShelves));
        var transfer = Assert.Single(older.Items, x => x.BusinessType.Code == "InternalTransfer");
        Assert.Equal(2, transfer.LedgerCount);
        Assert.Equal(500m, transfer.InKg);
        Assert.Equal(500m, transfer.OutKg);
        Assert.Equal(0m, transfer.NetKg);
        Assert.Equal("W.0.1", Assert.Single(transfer.FromShelves));
        Assert.Equal("F.2.5", Assert.Single(transfer.ToShelves));
        var oldTransferLink = (await new GetStockMovementByKeyQueryHandler(db, user, permissions)
            .Handle(new("LEGACY_LEDGER:25098"), default)).Data!;
        Assert.Equal(transfer.MovementKey, oldTransferLink.Movement.MovementKey);
        Assert.Equal(2, oldTransferLink.Lines.Count);
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
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
