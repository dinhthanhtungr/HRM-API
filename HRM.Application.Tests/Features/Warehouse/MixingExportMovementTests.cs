using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Npgsql;
using System.Text.Json;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class MixingExportMovementTests
{
    [WarehouseReadOnlyFact]
    public async Task ExistingMixingExportHasSameNumbersInFilteredListDetailAndJourney()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "MixingExportLabelReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var request = await db.WarehouseRequests.AsNoTracking().FirstAsync(x => x.RequestCode == "PRQ261001839");
        var row = await db.WarehouseMovementRows.AsNoTracking().SingleAsync(x => x.CompanyId == request.CompanyId
            && x.RequestCode == request.RequestCode && x.ProductCode == "NVL_KH_199");
        var user = new User(request.CompanyId);
        var permissions = new CurrentUserPermissionService(user);
        var page = (await new GetStockMovementsQueryHandler(db, user, permissions).Handle(new()
            { Keyword = "NVL_KH_199", BusinessType = "ExportToMixing", PageSize = 100 }, default)).Data!;
        var item = Assert.Single(page.Items, x => x.MovementKey == row.MovementKey);
        Assert.Equal("ExportToMixing", item.BusinessType.Code);
        Assert.Equal(row.DeltaKg, item.NetKg);
        Assert.Equal(row.BeforeKg, item.BeforeKg);
        Assert.Equal(row.AfterKg, item.AfterKg);
        var detail = (await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new(row.MovementKey), default)).Data!;
        Assert.Equal("ExportToMixing", detail.Movement.BusinessType.Code);
        var journey = (await new GetStockJourneyQueryHandler(db, user, permissions, new WarehouseVoucherBusinessReader(db, user, permissions)).Handle(new(row.MovementKey, row.LedgerId), default)).Data!;
        Assert.Contains(journey.Events.Items, x => x.Movement.MovementKey == row.MovementKey && x.Movement.BusinessType.Code == "ExportToMixing");
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("valid", "ExportToMixing")]
    [InlineData("otherName", "Decrease")]
    [InlineData("otherType", "Decrease")]
    [InlineData("foreign", "Decrease")]
    [InlineData("missing", "Decrease")]
    [InlineData("transfer", "ToMixing")]
    [InlineData("adjust", "Adjustment")]
    public async Task ListFilterAndDetailAgreeWithoutChangingLedgerOrGuessingReceipt(string scenario, string expected)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var row = new WarehouseMovementRow { CompanyId = company, MovementKey = "one", LedgerId = 1,
            MovementType = "NORMAL_OUT", IsCanonicalMovement = true, Scope = "NORMAL", ProductCode = "NVL-1",
            LotNumber = "LOT", ShelfStockCode = "W.0.1", BeforeKg = 492, AfterKg = 0, DeltaKg = -492,
            RequestCode = "PRQ-1", MovementAt = new DateTime(2026, 10, 29), CreatedAt = new DateTime(2026, 10, 29) };
        var request = new WarehouseRequest { CompanyId = company, RequestCode = "PRQ-1",
            ReqType = WareHouseRequestType.ExportOther, RequestName = "Phiếu xuất kho cho CT" };
        switch (scenario)
        {
            case "otherName": request.RequestName = "Phiếu xuất khác"; break;
            case "otherType": request.ReqType = (WareHouseRequestType)1; break;
            case "foreign": request.CompanyId = Guid.NewGuid(); break;
            case "missing": row.RequestCode = "UNKNOWN"; break;
            case "transfer": row.MovementType = "NORMAL_TO_CT"; break;
            case "adjust": row.AppSource = "Warehouse.AdjustUI"; break;
        }
        store.Seed(nameof(db.WarehouseMovementRows), row);
        store.Seed(nameof(db.WarehouseRequests), request);
        var user = new User(company);
        var permissions = new CurrentUserPermissionService(user);
        var handler = new GetStockMovementsQueryHandler(db, user, permissions);
        foreach (var filter in new string?[] { null, expected })
        {
            var page = (await handler.Handle(new() { BusinessType = filter }, default)).Data!;
            var item = Assert.Single(page.Items);
            Assert.Equal(expected, item.BusinessType.Code);
            Assert.Equal(-492m, item.NetKg);
            Assert.Equal(0m, item.InKg);
            Assert.Equal(492m, item.BeforeKg);
            Assert.Equal(0m, item.AfterKg);
            Assert.Empty(item.ToShelves);
        }
        var excluded = expected == "ExportToMixing" ? "Decrease" : "ExportToMixing";
        Assert.Empty((await handler.Handle(new() { BusinessType = excluded }, default)).Data!.Items);
        var detail = (await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new("one"), default)).Data!;
        Assert.Equal(expected, detail.Movement.BusinessType.Code);
        var journey = (await new GetStockJourneyQueryHandler(db, user, permissions, new WarehouseVoucherBusinessReader(db, user, permissions)).Handle(new("one", 1), default)).Data!;
        Assert.Equal(expected, Assert.Single(journey.Events.Items).Movement.BusinessType.Code);
        Assert.Equal(scenario == "transfer" ? "NORMAL_TO_CT" : "NORMAL_OUT", row.MovementType);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void RequestClassifierTranslatesWithCompanyTypeAndExactNameGuards()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var codes = StockMovementRequestClassifier.MixingExportCodes(db, Guid.NewGuid());
        var sql = db.WarehouseMovementRows.GroupBy(x => x.MovementKey).Where(g => g.Any(x =>
            x.MovementType == "NORMAL_OUT" && x.DeltaKg < 0 && codes.Contains(x.RequestCode!))).Select(g => g.Key).ToQueryString();
        Assert.Contains("WarehouseRequest", sql);
        Assert.Contains("reqType", sql);
        Assert.Contains("requestName", sql);
        Assert.DoesNotContain("UnitCost", sql);
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
