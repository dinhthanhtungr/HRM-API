using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockMovementTests
{
    private readonly Guid company = Guid.NewGuid();

    [Fact]
    public async Task TransferIsOneMovementWithBothLegsAndNoVoucherDuplication()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseMovementRows), Row("transfer", 1, -100), Row("transfer", 2, 100, "CT"));
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher { VoucherId = 1, CompanyId = company });
        var user = new User(company);
        var result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new(), default);
        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(2, item.LedgerCount);
        Assert.Equal(0, item.NetKg);
        Assert.Equal(100, item.InKg);
        Assert.Equal(100, item.OutKg);
        Assert.Equal("ToMixing", item.BusinessType.Code);
        Assert.Null(item.BeforeKg);
        Assert.Null(item.AfterKg);
        Assert.Single(item.Goods);
    }

    [Fact]
    public async Task ScopeFilterStillReturnsCompleteTransfer()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseMovementRows), Row("transfer", 1, -100), Row("transfer", 2, 100, "CT"));
        var user = new User(company);
        var result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new() { Scope = "CT" }, default);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(2, item.LedgerCount);
        Assert.Equal(0, item.NetKg);
    }

    [Fact]
    public async Task NoMixingPermissionHidesWholeTransferIncludingDetail()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseMovementRows), Row("transfer", 1, -100), Row("transfer", 2, 100, "CT"));
        var user = new User(company) { HasExplicitPermissionSet = true };
        var permissions = new CurrentUserPermissionService(user);
        var result = await new GetStockMovementsQueryHandler(db, user, permissions).Handle(new(), default);
        Assert.Empty(result.Data!.Items);
        Assert.False((await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new("transfer"), default)).Success);
    }

    [Fact]
    public async Task OtherCompanyCannotBeReadThroughListOrDetail()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var foreign = Row("foreign", 1, 50); foreign.CompanyId = Guid.NewGuid();
        store.Seed(nameof(db.WarehouseMovementRows), foreign);
        var user = new User(company);
        var permissions = new CurrentUserPermissionService(user);
        Assert.Empty((await new GetStockMovementsQueryHandler(db, user, permissions).Handle(new(), default)).Data!.Items);
        Assert.False((await new GetStockMovementByKeyQueryHandler(db, user, permissions).Handle(new("foreign"), default)).Success);
    }

    [Theory]
    [InlineData("Warehouse.InventoryCheck", "InventoryCheck")]
    [InlineData("Warehouse.AdjustUI", "Adjustment")]
    public async Task CorrectionsWithoutVoucherRemainVisible(string source, string expected)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var row = Row("correction", 1, -7); row.AppSource = source; row.MovementType = "NORMAL_OUT"; row.VoucherId = null;
        store.Seed(nameof(db.WarehouseMovementRows), row);
        var user = new User(company);
        var result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new() { BusinessType = expected }, default);
        Assert.Equal(expected, Assert.Single(result.Data!.Items).BusinessType.Code);
    }

    [Fact]
    public async Task LegacyRowsStaySeparateAndPagingIsByMovement()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var first = Row("LEGACY_LEDGER:1", 1, -10); first.IsCanonicalMovement = false; first.VoucherId = null; first.MovementType = "LEGACY_UNGROUPED";
        var second = Row("LEGACY_LEDGER:2", 2, 10); second.IsCanonicalMovement = false; second.VoucherId = null; second.MovementType = "LEGACY_UNGROUPED";
        store.Seed(nameof(db.WarehouseMovementRows), first, second);
        var user = new User(company);
        var result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new() { PageSize = 1 }, default);
        Assert.Equal(2, result.Data!.TotalCount);
        var item = Assert.Single(result.Data.Items);
        Assert.False(item.IsGrouped);
        Assert.Equal("LEGACY_LEDGER:2", item.MovementKey);
    }

    [Fact]
    public async Task BusinessFilterUsesWholeGroupRatherThanOnlyTheMatchingShelf()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var first = Row("correction", 1, -10); first.AppSource = "Warehouse.InventoryCheck"; first.MovementType = "NORMAL_INTERNAL";
        var second = Row("correction", 2, 10); second.AppSource = "Warehouse.AdjustUI"; second.MovementType = "NORMAL_INTERNAL";
        store.Seed(nameof(db.WarehouseMovementRows), first, second);
        var user = new User(company);
        var handler = new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user));
        Assert.Empty((await handler.Handle(new() { BusinessType = "InternalTransfer" }, default)).Data!.Items);
        var actual = await handler.Handle(new() { BusinessType = "InventoryCheck" }, default);
        Assert.Equal("InventoryCheck", Assert.Single(actual.Data!.Items).BusinessType.Code);
    }

    [Theory]
    [InlineData(false, "Developer")]
    [InlineData(true, "SaleUser")]
    public async Task UnauthorizedReadersFailClosed(bool authenticated, string role)
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var user = new User(company) { IsAuthenticated = authenticated, Roles = [role] };
        Assert.False((await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new(), default)).Success);
    }

    [Fact]
    public async Task FlatSummaryIncludesLotBalancesAndScopedNamesWithoutExtraDetailCalls()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var actor = Guid.NewGuid();
        var row = Row("single", 1, -7);
        row.CreatedBy = actor; row.LotNumber = "LOT-1"; row.StockType = 3;
        row.BeforeKg = 20; row.AfterKg = 13; row.VoucherId = 5;
        store.Seed(nameof(db.WarehouseMovementRows), row);
        store.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee
            { EmployeeId = actor, CompanyId = company, FullName = "Warehouse operator" });
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher
            { VoucherId = 5, CompanyId = company, VoucherCode = "PXK-5" });
        var user = new User(company) { Roles = [ApplicationRoles.Production.PLPUUser] };
        var result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new(), default);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(20, item.BeforeKg);
        Assert.Equal(13, item.AfterKg);
        Assert.Equal("LOT-1", Assert.Single(item.Goods).LotNumber);
        Assert.Equal("Warehouse operator", Assert.Single(item.CreatedByNames));
        Assert.Equal("PXK-5", Assert.Single(item.ReferenceCodes));

        store.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee
            { EmployeeId = actor, CompanyId = Guid.NewGuid(), FullName = "Foreign operator" });
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher
            { VoucherId = 5, CompanyId = Guid.NewGuid(), VoucherCode = "FOREIGN" });
        result = await new GetStockMovementsQueryHandler(db, user, new CurrentUserPermissionService(user)).Handle(new(), default);
        item = Assert.Single(result.Data!.Items);
        Assert.Empty(item.CreatedByNames);
        Assert.Empty(item.ReferenceCodes);
    }

    private WarehouseMovementRow Row(string key, long ledgerId, decimal delta, string scope = "NORMAL") => new()
    {
        CompanyId = company, MovementKey = key, LedgerId = ledgerId, MovementAt = new DateTime(2026, 11, 10),
        CreatedAt = new DateTime(2026, 11, 10),
        IsCanonicalMovement = true, MovementType = "NORMAL_TO_CT", Scope = scope,
        ShelfStockCode = scope == "CT" ? "CT.0.1" : "A.1", ProductCode = "NVL-1", DeltaKg = delta, VoucherId = 1
    };

    private sealed class User(Guid company) : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Developer];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
