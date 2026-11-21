using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaEditLockTests
{
    [Theory]
    [InlineData("Scheduling", false)]
    [InlineData("Running", false)]
    [InlineData("Done", true)]
    [InlineData("Finished", true)]
    public async Task LockDependsOnRequestNotMfgStatus(string status, bool hasRequest)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var order = new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = Guid.NewGuid(), Status = status, ExternalId = "MFG1" };
        if (hasRequest) SeedRequest(db, store, order, WarehouseRequestStatus.Pending);
        Assert.Equal(hasRequest, await ManufacturingAccess.FormulaEditLockAsync(db, order, default) is not null);
    }

    [Theory]
    [InlineData(WarehouseRequestStatus.Pending, true)]
    [InlineData(WarehouseRequestStatus.Approved, true)]
    [InlineData(WarehouseRequestStatus.Completed, true)]
    [InlineData(WarehouseRequestStatus.Cancelled, false)]
    public async Task OnlyCancelledWithoutIssueUnlocks(WarehouseRequestStatus status, bool locked)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var order = new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = Guid.NewGuid(), ExternalId = "MFG1" };
        SeedRequest(db, store, order, status);
        Assert.Equal(locked, await ManufacturingAccess.FormulaEditLockAsync(db, order, default) is not null);
    }

    [Fact]
    public async Task CancelledRequestStillLocksAfterActualIssue()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var order = new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = Guid.NewGuid(), ExternalId = "MFG1" };
        SeedRequest(db, store, order, WarehouseRequestStatus.Cancelled);
        store.Seed(nameof(db.TempStockIssueLedgers), new TempStockIssueLedger { CompanyId = order.CompanyId, RequestCode = "XK1", VaCode = "VA1", QtyKg = 1 });
        Assert.NotNull(await ManufacturingAccess.FormulaEditLockAsync(db, order, default));
    }

    [Fact]
    public async Task CancelledRequestWithAppliedStockDecreaseRemainsLocked()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var order = new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = Guid.NewGuid(), ExternalId = "MFG1" };
        store.Seed(nameof(db.OperationMaterialRequestLinks), new OperationMaterialRequestLink { CompanyId = order.CompanyId,
            MfgProductionOrderId = order.MfgProductionOrderId, RequestCode = "XK1" });
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest { CompanyId = order.CompanyId, RequestId = 1,
            RequestCode = "XK1", ReqType = WareHouseRequestType.ExportRawMaterial, ReqStatus = WarehouseRequestStatus.Cancelled,
            WarehouseVouchers = [new WarehouseVoucher { CompanyId = order.CompanyId,
                Details = [new WarehouseVoucherDetail { IsApplied = true, IsIncrease = false, QtyKg = 1 }] }] });
        Assert.NotNull(await ManufacturingAccess.FormulaEditLockAsync(db, order, default));
    }

    [Fact]
    public async Task ForeignCompanyRequestCannotLockOrder()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var order = new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = Guid.NewGuid(), ExternalId = "MFG1" };
        SeedRequest(db, store, new MfgProductionOrder { CompanyId = Guid.NewGuid(), MfgProductionOrderId = order.MfgProductionOrderId }, WarehouseRequestStatus.Pending);
        Assert.Null(await ManufacturingAccess.FormulaEditLockAsync(db, order, default));
    }

    private static void SeedRequest(IManufacturingDbContext db, ProductionOrderTestStore store, MfgProductionOrder order, WarehouseRequestStatus status)
    {
        store.Seed(nameof(db.OperationMaterialRequestLinks), new OperationMaterialRequestLink { CompanyId = order.CompanyId, MfgProductionOrderId = order.MfgProductionOrderId, RequestId = 1, RequestCode = "XK1" });
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest { CompanyId = order.CompanyId, RequestId = 1, RequestCode = "XK1", ReqType = WareHouseRequestType.ExportRawMaterial, ReqStatus = status });
    }
}
