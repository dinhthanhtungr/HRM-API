using ClosedXML.Excel;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.Documents.Excels;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderExcelReceiptAmountTests
{
    [Theory]
    [InlineData("NCC", VoucherDetailType.Waiter)]
    [InlineData("NCC", VoucherDetailType.QCFail)]
    [InlineData("PO", VoucherDetailType.Waiter)]
    public async Task ExcelAmountUsesWarehouseReceiptsEvenWhenQcNotAccepted(string orderType, VoucherDetailType qcType)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH-1", CompanyId = user.CompanyId,
            OrderType = orderType, PurchaseOrderDetails = [new()
            {
                PurchaseOrderDetailId = Guid.NewGuid(), LineNo = 1, MaterialExternalIDSnapshot = "NVL_TEST",
                RequestQuantity = 20m, RealQuantity = 0m, UnitPriceAgreed = 100m, TotalPriceAgreed = 2000m
            }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest
        {
            RequestId = 1, CompanyId = user.CompanyId!.Value, codeFromRequest = po.ExternalId
        });
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher
        {
            VoucherId = 1, RequestId = 1, CompanyId = user.CompanyId.Value,
            VoucherType = (int)WareHouseRequestType.ImportOther
        });
        store.Seed(nameof(db.WarehouseVoucherDetails), new WarehouseVoucherDetail
        {
            VoucherDetailId = 1, VoucherId = 1, ProductCode = "NVL_TEST", VoucherType = qcType
        });
        store.Seed(nameof(db.WarehouseShelfLedgers),
            new WarehouseShelfLedger { VoucherDetailId = 1, CompanyId = user.CompanyId.Value, DeltaKg = 10m, CreatedAt = new(2026, 10, 1) },
            new WarehouseShelfLedger { VoucherDetailId = 1, CompanyId = Guid.NewGuid(), DeltaKg = 999m, CreatedAt = new(2026, 10, 1) });

        var service = new PurchaseOrderListExportService(db, user, new PurchaseOrderExcelRenderer(), new CurrentUserPermissionService(user));
        var bytes = await service.ExportAsync(new GetPurchaseOrdersQuery(), default);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Purchase Orders");
        if (orderType == "PO")
        {
            Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
            return;
        }
        Assert.Equal(10m, sheet.Cell(2, 15).GetValue<decimal>());
        Assert.Equal(0m, sheet.Cell(2, 16).GetValue<decimal>());
        Assert.Equal(1000m, sheet.Cell(2, 20).GetValue<decimal>());
    }

    [Theory]
    [InlineData("NCC", "DDH-1", "NVL_001", true)]
    [InlineData("NCC", "ddh-1", "nvl_001", true)]
    [InlineData("NCC", "DDH-1", "NVL_VPP_001", false)]
    [InlineData("NCC", "DDH-1", "nvl_bb_001", false)]
    [InlineData("NCC", "DDH-1", "VT_001", false)]
    [InlineData("NCC", "po-1", "NVL_001", false)]
    [InlineData("PO", "DDH-1", "NVL_001", false)]
    public async Task ExportFiltersMatchPlpuScope(string type, string code, string materialCode, bool included)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        store.Seed(nameof(db.PurchaseOrders), new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), ExternalId = code, CompanyId = user.CompanyId, OrderType = type,
            PurchaseOrderDetails = [new() { IsActive = true, MaterialExternalIDSnapshot = materialCode, RequestQuantity = 10m }]
        });
        var service = new PurchaseOrderListExportService(db, user, new PurchaseOrderExcelRenderer(), new CurrentUserPermissionService(user));
        var bytes = await service.ExportAsync(new GetPurchaseOrdersQuery(), default);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(included ? 2 : 1, workbook.Worksheet("Purchase Orders").LastRowUsed()!.RowNumber());
    }

    [Theory]
    [InlineData(10, 12, 150, 9, 12)]
    [InlineData(10, null, 150, 9, 15)]
    [InlineData(10, null, null, 9, 9)]
    [InlineData(0, null, 150, 9, 9)]
    public void PriceFallbackMatchesPlpu(int quantity, int? price, int? total, int? snapshot, int expected)
        => Assert.Equal(expected, PurchaseOrderExportRules.ResolvePurchaseOrderUnitPrice(quantity, price, total, snapshot));
}
