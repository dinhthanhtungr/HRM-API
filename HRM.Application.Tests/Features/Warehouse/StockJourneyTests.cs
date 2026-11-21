using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockJourneyTests
{
    private readonly Guid company = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 11, 1);

    [Fact]
    public async Task JourneyKeepsTransfersTogetherCrossesStockTypesAndEndsWithActualCurrentLocations()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var receipt = Row(1, "receipt", 0, 100); receipt.VoucherId = 1;
        var from = Row(2, "transfer", 100, 60); from.MovementType = "NORMAL_TO_CT";
        var to = Row(3, "transfer", 0, 40, "CT.0.1"); to.MovementType = "NORMAL_TO_CT"; to.MovementAt = from.MovementAt;
        var use = Row(4, "use", 40, 30, "CT.0.1");
        var oldType = Row(5, "type-change", 30, 0, "CT.0.1"); oldType.MovementType = "CT_INTERNAL";
        var newType = Row(6, "type-change", 0, 30, "CT.0.1"); newType.StockType = 4; newType.MovementType = "CT_INTERNAL"; newType.MovementAt = oldType.MovementAt;
        var addition = Row(7, "addition", 60, 80);
        var otherLot = Row(8, "other-lot", 0, 999); otherLot.LotNumber = "OTHER";
        var foreign = Row(9, "foreign", 0, 999); foreign.CompanyId = Guid.NewGuid();
        store.Seed(nameof(db.WarehouseMovementRows), receipt, from, to, use, oldType, newType, addition, otherLot, foreign);
        store.Seed(nameof(db.WarehouseShelfStocks), Stock("A.1", 80), Stock("CT.0.1", 30, false, StockType.DefectiveRawMaterial));
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher { CompanyId = company, VoucherId = 1, VoucherCode = "PNK-1" });
        var baseline = await Handler(db).Handle(new("transfer", 2), default);
        using var activity = new System.Diagnostics.Activity("journey-timing-test").Start();
        activity.SetTag(HRM.Application.Features.Warehouse.Services.WarehouseMovementDiagnosticTags.Enabled, true);
        var result = await Handler(db).Handle(new("transfer", 2), default);
        var phases = HRM.Application.Features.Warehouse.Services.WarehouseMovementDiagnosticTags.ValidateServerTiming(
            activity.GetTagItem(HRM.Application.Features.Warehouse.Services.WarehouseMovementDiagnosticTags.ServerTiming) as string, "wj")!;
        foreach (var phase in new[] { "scope", "key", "seed", "statistics", "page", "rows", "context", "actors", "vouchers", "verification", "business", "locations", "allocations", "mapping", "handler" })
            Assert.Contains("wj_" + phase + ";dur=", phases);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(baseline.Data!.Events), System.Text.Json.JsonSerializer.Serialize(result.Data!.Events));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(baseline.Data.CurrentLocations), System.Text.Json.JsonSerializer.Serialize(result.Data.CurrentLocations));
        Assert.True(result.Success);
        var journey = result.Data!;
        Assert.Equal(5, journey.Events.TotalCount);
        Assert.Equal(new[] { "receipt", "transfer", "use", "type-change", "addition" }, journey.Events.Items.Select(x => x.Movement.MovementKey));
        Assert.Equal("Receipt", journey.Events.Items[0].Movement.BusinessType.Code);
        var transfer = journey.Events.Items[1];
        Assert.Equal(2, transfer.Lines.Count);
        Assert.Equal(0, transfer.Movement.NetKg);
        Assert.Contains(journey.Events.Items[3].Lines, x => x.StockType == 4);
        Assert.Equal(110, journey.CurrentLocations.Sum(x => x.QtyKg));
        Assert.False(journey.CurrentLocations.Single(x => x.ShelfCode == "CT.0.1").IsActive);
        Assert.Equal(receipt.MovementAt, journey.FirstRecordedAt);
        Assert.Equal(addition.MovementAt, journey.LastRecordedAt);
    }

    [Fact]
    public async Task MissingLotNeverCombinesUnrelatedUnidentifiedBatches()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var row = Row(1, "missing", 0, 100); row.LotNumber = null;
        store.Seed(nameof(db.WarehouseMovementRows), row, Row(2, "known", 0, 200));
        var result = await Handler(db).Handle(new("missing", 1), default);
        Assert.True(result.Success);
        Assert.False(result.Data!.CanTraceLot);
        Assert.Empty(result.Data.Events.Items);
        Assert.Empty(result.Data.CurrentLocations);
    }

    [Fact]
    public async Task SeedMustBelongToRequestedMovementAndCompany()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var foreign = Row(2, "foreign", 0, 100); foreign.CompanyId = Guid.NewGuid();
        store.Seed(nameof(db.WarehouseMovementRows), Row(1, "own", 0, 100), foreign);
        Assert.False((await Handler(db).Handle(new("foreign", 2), default)).Success);
        Assert.False((await Handler(db).Handle(new("own", 2), default)).Success);
        Assert.False((await Handler(db).Handle(new("other", 1), default)).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixingPermissionProtectsEntireTransferCurrentStockAndLinkedAllocations(bool ctLegHasOtherLot)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var normal = Row(1, "receipt", 0, 100);
        var from = Row(2, "transfer", 100, 60); from.VoucherId = 2; from.VoucherDetailId = 20;
        var to = Row(3, "transfer", 0, 40, "CT.0.1");
        if (ctLegHasOtherLot) to.LotNumber = "OTHER";
        store.Seed(nameof(db.WarehouseMovementRows), normal, from, to);
        store.Seed(nameof(db.WarehouseShelfStocks), Stock("A.1", 60), Stock("CT.0.1", 40));
        store.Seed(nameof(db.OperationMaterialBuffers), Buffer(1, 20, 2));
        var user = new User(company) { HasExplicitPermissionSet = true };
        var result = await Handler(db, user).Handle(new("receipt", 1), default);
        Assert.True(result.Data!.IsScopeLimited);
        Assert.Single(result.Data.Events.Items);
        Assert.Equal("A.1", Assert.Single(result.Data.CurrentLocations).ShelfCode);
        Assert.Empty(result.Data.ProductionAllocations);
        Assert.False((await Handler(db, user).Handle(new("transfer", 2), default)).Success);
    }

    [Fact]
    public async Task ProductionDestinationRequiresActualVoucherDetailLinkAndMatchingLotCompany()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var issue = Row(1, "issue", 100, 60); issue.VoucherId = 2; issue.VoucherDetailId = 20;
        store.Seed(nameof(db.WarehouseMovementRows), issue);
        var valid = Buffer(1, 20, 2);
        var foreign = Buffer(2, 20, 2); foreign.CompanyId = Guid.NewGuid();
        var otherLot = Buffer(3, 20, 2); otherLot.LotNumber = "OTHER";
        var wrongDocument = Buffer(4, 20, 99);
        var noLink = Buffer(5, 99, 2);
        store.Seed(nameof(db.OperationMaterialBuffers), valid, foreign, otherLot, wrongDocument, noLink);
        var result = await Handler(db).Handle(new("issue", 1), default);
        var allocation = Assert.Single(result.Data!.ProductionAllocations);
        Assert.Equal("VA-1", allocation.VaCode);
        Assert.Equal(5, allocation.UsedKg);
        Assert.Equal(35, allocation.RemainingKg);
    }

    [Fact]
    public async Task GroupTimestampIncludesOtherLotsButTieBreakUsesOnlyTheRequestedLot()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var otherLot = Row(1, "multi-lot", 0, 100);
        otherLot.LotNumber = "OTHER";
        var selectedLot = Row(30, "multi-lot", 0, 20);
        // The view repeats the whole movement timestamp on each ledger row.
        selectedLot.MovementAt = otherLot.CreatedAt;
        var between = Row(10, "between", 0, 10);
        store.Seed(nameof(db.WarehouseMovementRows), otherLot, selectedLot, between);
        var result = (await Handler(db).Handle(new("multi-lot", 30), default)).Data!;
        Assert.Equal(otherLot.CreatedAt, result.FirstRecordedAt);
        Assert.Equal(between.CreatedAt, result.LastRecordedAt);
        Assert.Equal(new[] { "multi-lot", "between" }, result.Events.Items.Select(x => x.Movement.MovementKey));
        Assert.Equal(30, Assert.Single(result.Events.Items[0].Lines).LedgerId);
    }

    [Fact]
    public async Task EqualMovementTimesUseTheFirstLedgerOfTheRequestedLotAsTieBreak()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var otherLot = Row(1, "multi-lot", 0, 100);
        otherLot.LotNumber = "OTHER";
        var selectedLot = Row(30, "multi-lot", 0, 20);
        selectedLot.MovementAt = otherLot.CreatedAt;
        var competing = Row(10, "competing", 0, 10);
        competing.CreatedAt = competing.MovementAt = otherLot.CreatedAt;
        store.Seed(nameof(db.WarehouseMovementRows), otherLot, selectedLot, competing);
        var result = (await Handler(db).Handle(new("multi-lot", 30), default)).Data!;
        Assert.Equal(new[] { "competing", "multi-lot" }, result.Events.Items.Select(x => x.Movement.MovementKey));
    }

    [Fact]
    public async Task HistoryPagesChronologicallyWithoutDroppingTheCurrentState()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseMovementRows), Enumerable.Range(1, 45).Select(i => Row(i, "event-" + i, i - 1, i)).ToArray());
        store.Seed(nameof(db.WarehouseShelfStocks), Stock("A.1", 45));
        var first = (await Handler(db).Handle(new("event-45", 45), default)).Data!;
        var second = (await Handler(db).Handle(new("event-45", 45, 2), default)).Data!;
        Assert.Equal(40, first.Events.Items.Count);
        Assert.True(first.Events.HasNextPage);
        Assert.Equal(5, second.Events.Items.Count);
        Assert.Equal("event-41", second.Events.Items[0].Movement.MovementKey);
        Assert.False(second.Events.HasNextPage);
        Assert.Equal(45, Assert.Single(second.CurrentLocations).QtyKg);
    }

    private GetStockJourneyQueryHandler Handler(IWarehouseReadDbContext db, User? user = null)
    {
        user ??= new User(company);
        var permissions = new CurrentUserPermissionService(user);
        return new(db, user, permissions, new HRM.Application.Features.Warehouse.Services.WarehouseVoucherBusinessReader(db, user, permissions));
    }

    private WarehouseMovementRow Row(int id, string key, decimal before, decimal after, string shelf = "A.1") => new()
    {
        CompanyId = company, LedgerId = id, MovementKey = key, ProductCode = "NVL-1", LotNumber = "LOT-1",
        MovementAt = Start.AddMinutes(id), CreatedAt = Start.AddMinutes(id), StockType = 3, IsCanonicalMovement = true,
        ShelfStockCode = shelf, Scope = shelf.StartsWith("CT.") ? "CT" : "NORMAL", DeltaKg = after - before, BeforeKg = before, AfterKg = after,
        MovementType = after > before ? "NORMAL_IN" : "NORMAL_OUT"
    };
    private WarehouseShelfStock Stock(string shelf, decimal qty, bool active = true, StockType type = StockType.RawMaterial) => new()
    {
        CompanyId = company, Code = "NVL-1", LotNo = "LOT-1", QtyKg = qty, StockType = type, ShelfStockCode = shelf,
        WarehouseShelves = new WarehouseShelves { CompanyId = company, SlotCode = shelf, IsActive = active }
    };
    private OperationMaterialBuffer Buffer(int id, long detail, long voucher) => new()
    {
        BufferId = id, CompanyId = company, ProductCode = "NVL-1", LotNumber = "LOT-1", VoucherDetailId = detail,
        VoucherId = voucher, VaExternalId = "VA-1", MfgExternalId = "MFG-1", IssuedQtyKg = 40, UsedQtyKg = 5, RemainingQtyKg = 35
    };
    private sealed class User(Guid company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Production.PLPUUser];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
