using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockJourneyVoucherTitleTests
{
    [Theory]
    [InlineData("production", "ProductionReceipt")]
    [InlineData("fallback", "ImportFinishedGood")]
    [InlineData("foreignProduction", "ImportFinishedGood")]
    [InlineData("foreignRequest", null)]
    [InlineData("foreignVoucher", null)]
    [InlineData("unknown", null)]
    [InlineData("multipleVouchers", null)]
    public async Task JourneyUsesTheSharedCompanyScopedVoucherClassification(string scenario, string? expected)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var voucher = new WarehouseVoucher { CompanyId = company, VoucherId = 2, RequestId = 1,
            VoucherType = 1, VoucherCode = "PNK-1", Status = "APPLIED" };
        var request = new WarehouseRequest { CompanyId = company, RequestId = 1, RequestCode = "PRQ-1",
            ReqType = WareHouseRequestType.ImportFinishedGood };
        if (scenario == "foreignVoucher") voucher.CompanyId = Guid.NewGuid();
        if (scenario == "foreignRequest") request.CompanyId = Guid.NewGuid();
        if (scenario == "unknown") voucher.VoucherType = 999;
        store.Seed(nameof(db.WarehouseVouchers), voucher);
        store.Seed(nameof(db.WarehouseRequests), request);
        if (scenario is "production" or "foreignProduction")
            store.Seed(nameof(db.ProductionOutputReceiptLedgers), new ProductionOutputReceiptLedger
            { CompanyId = company, VoucherId = 2, ProductionOutput = new() { CompanyId = scenario == "production" ? company : Guid.NewGuid() } });
        var row = new WarehouseMovementRow { CompanyId = company, LedgerId = 1, MovementKey = "receipt",
            MovementId = Guid.NewGuid(), IsCanonicalMovement = true, MovementType = "NORMAL_IN", Scope = "NORMAL",
            ProductCode = "ITEM", LotNumber = "LOT", StockType = 1, ShelfStockCode = "H.0.1",
            DeltaKg = 100, AfterKg = 100, VoucherId = 2, VoucherDetailId = 3 };
        if (scenario == "multipleVouchers")
            store.Seed(nameof(db.WarehouseMovementRows), row, new WarehouseMovementRow { CompanyId = company,
                LedgerId = 2, MovementKey = "receipt", ProductCode = "ITEM", LotNumber = "LOT", VoucherId = 99,
                DeltaKg = 10, AfterKg = 10, Scope = "NORMAL" });
        else store.Seed(nameof(db.WarehouseMovementRows), row);
        var user = new User(company);
        var permissions = new CurrentUserPermissionService(user);
        var handler = new GetStockJourneyQueryHandler(db, user, permissions, new WarehouseVoucherBusinessReader(db, user, permissions));
        var result = await handler.Handle(new("receipt", 1), default);
        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Events.Items);
        Assert.Equal(expected, item.VoucherBusinessType?.Code);
        Assert.Equal("Receipt", item.Movement.BusinessType.Code); // Classification/filter remains stable.
        Assert.Equal(scenario == "multipleVouchers" ? 110m : 100m, item.Movement.InKg);
        Assert.Equal(0, store.Saves);
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
