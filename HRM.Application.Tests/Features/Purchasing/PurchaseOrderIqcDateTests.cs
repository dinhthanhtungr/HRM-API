using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderIqcDateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListAndDetailUseLatestIqcWithoutLedgerAndIgnoreManualDate(bool hasIqc)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var company = user.CompanyId!.Value;
        var po = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH-1", CompanyId = company,
            IsActive = true, RealDeliveryDate = new(2099, 1, 1) };
        store.Seed(nameof(db.PurchaseOrders), po);
        store.Seed(nameof(db.WarehouseRequests),
            new WarehouseRequest { RequestId = 1, CompanyId = company, IsActive = true, codeFromRequest = " DDH-1 " },
            new WarehouseRequest { RequestId = 2, CompanyId = Guid.NewGuid(), IsActive = true, codeFromRequest = "DDH-1" },
            new WarehouseRequest { RequestId = 3, CompanyId = company, IsActive = false, codeFromRequest = "DDH-1" });
        store.Seed(nameof(db.WarehouseVouchers), Enumerable.Range(1, 3).Select(i => new WarehouseVoucher {
            VoucherId = i, RequestId = i, CompanyId = company, VoucherType = (int)WareHouseRequestType.ImportMaterial }).ToArray());
        store.Seed(nameof(db.WarehouseVoucherDetails), Enumerable.Range(1, 3).Select(i => new WarehouseVoucherDetail {
            VoucherDetailId = i, VoucherId = i }).ToArray());
        var latest = new DateTime(2026, 10, 19, 15, 0, 0);
        store.Seed(nameof(db.QCInputByQCs), hasIqc ? new[] {
            new QCInputByQC { VoucherDetailId = 1, CreatedDate = latest.AddDays(-1) },
            new QCInputByQC { VoucherDetailId = 1, CreatedDate = latest },
            new QCInputByQC { VoucherDetailId = 2, CreatedDate = latest.AddDays(5) },
            new QCInputByQC { VoucherDetailId = 3, CreatedDate = latest.AddDays(6) }
        } : []);
        var reader = new PurchaseOrderReceiptReader(db);
        var detail = await new GetPurchaseOrderByIdQueryHandler(db, user, reader).Handle(new(po.PurchaseOrderId), default);
        var list = await new GetPurchaseOrdersQueryHandler(db, user, reader).Handle(new(), default);
        Assert.Equal(hasIqc ? latest : (DateTime?)null, detail!.RealDeliveryDate);
        Assert.Equal(detail.RealDeliveryDate, Assert.Single(list.Items).RealDeliveryDate);
        Assert.Equal(new DateTime(2099, 1, 1), po.RealDeliveryDate);
        Assert.Equal(0, store.Saves);
    }
}
