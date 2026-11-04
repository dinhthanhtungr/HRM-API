using HRM.Domain.Enums.Orders;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderParityTests
{
    [Fact]
    public async Task DetailAndSummary_KeepQcAcceptedQuantity_WhenLedgerDiffers()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(),
            ExternalId = "DDH-1",
            CompanyId = user.CompanyId,
            IsActive = true,
            PurchaseOrderSnapshot = new() { TotalPrice = 1100m, SupplierContactSnapshot = "Contact" },
            PurchaseOrderDetails = [new() { PurchaseOrderDetailId = Guid.NewGuid(), IsActive = true, LineNo = 1,
                MaterialExternalIDSnapshot = "MAT", RequestQuantity = 10m, RealQuantity = 6m, UnitPriceAgreed = 100m, TotalPriceAgreed = 1000m }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest { RequestId = 1, IsActive = true, CompanyId = user.CompanyId!.Value, codeFromRequest = po.ExternalId });
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher { VoucherId = 1, RequestId = 1, CompanyId = user.CompanyId.Value, VoucherType = (int)WareHouseRequestType.ImportOther });
        store.Seed(nameof(db.WarehouseVoucherDetails), new WarehouseVoucherDetail { VoucherDetailId = 1, VoucherId = 1, ProductCode = "MAT" });
        store.Seed(nameof(db.WarehouseShelfLedgers), new WarehouseShelfLedger { VoucherDetailId = 1, CompanyId = user.CompanyId.Value, DeltaKg = 10m, CreatedAt = new(2026, 10, 1) });
        var reader = new PurchaseOrderReceiptReader(db);
        var detail = await new GetPurchaseOrderByIdQueryHandler(db, user, reader).Handle(new(po.PurchaseOrderId), default);
        Assert.Equal(6m, Assert.Single(detail!.Items).RealQuantity);
        Assert.Equal(600m, detail.RealTotalPrice);
        Assert.Equal("Contact", detail.PurchaseOrderSnapshot!.SupplierContactSnapshot);
        var list = await new GetPurchaseOrdersQueryHandler(db, user, reader).Handle(new(), default);
        Assert.Equal(600m, Assert.Single(list.Items).RealTotalPrice);
        Assert.Equal(detail.TotalPrice, list.Items[0].TotalPrice);
        Assert.Equal(1000m, list.Items[0].Subtotal);
        Assert.Equal(6m, Assert.Single(list.Items[0].Details).RealQuantity);
        po.CompanyId = Guid.NewGuid();
        Assert.Null(await new GetPurchaseOrderByIdQueryHandler(db, user, reader).Handle(new(po.PurchaseOrderId), default));
        Assert.Empty((await new GetPurchaseOrdersQueryHandler(db, user, reader).Handle(new(), default)).Items);
    }

    [Theory]
    [InlineData("NVL", WareHouseRequestType.ImportOther, StockType.Waiter)]
    [InlineData("VT", WareHouseRequestType.ImportMaterial, StockType.Material)]
    public async Task Print_CreatesLegacyWarehouseRequest_AndSkipsExisting(string category, WareHouseRequestType type, StockType stock)
    {
        var (db, store) = PurchaseTestStore.Create();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(),
            ExternalId = "DDH-1",
            CompanyId = user.CompanyId,
            CreatedBy = user.EmployeeId,
            IsActive = true,
            PurchaseOrderDetails = [new() { IsActive = true, MaterialExternalIDSnapshot = "MAT", RequestQuantity = 25m, Package = "10 bags", Material = new Material { Category = new Category { Types = category } } }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var service = new PurchaseOrderPrintService(db, user, new Ids(), new Clock());
        await service.EnsureForPrintAsync(po.PurchaseOrderId, default);
        var request = Assert.Single(store.Added.OfType<WarehouseRequest>());
        Assert.Equal(type, request.ReqType);
        Assert.Equal(stock, Assert.Single(request.WarehouseRequestDetails).ItemStockType);
        Assert.Equal(10, request.WarehouseRequestDetails.Single().BagNumber);
        Assert.Equal("DDH-1", request.codeFromRequest);
        Assert.True(store.Transaction.Committed);
        store.Seed(nameof(db.WarehouseRequests), request);
        await service.EnsureForPrintAsync(po.PurchaseOrderId, default);
        Assert.Single(store.Added.OfType<WarehouseRequest>());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Completed")]
    public async Task Complete_CompletesEveryLinkedRequest_WithinCompany(string initialStatus)
    {
        var (db, store) = PurchaseTestStore.Create();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH-1", CompanyId = user.CompanyId, Status = initialStatus, IsActive = true };
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var own = new WarehouseRequest { RequestId = 1, codeFromRequest = " DDH-1 ", CompanyId = user.CompanyId!.Value, ReqStatus = WarehouseRequestStatus.Pending, IsActive = true };
        var foreign = new WarehouseRequest { RequestId = 2, codeFromRequest = "DDH-1", CompanyId = Guid.NewGuid(), ReqStatus = WarehouseRequestStatus.Pending, IsActive = true };
        store.Seed(nameof(db.PurchaseOrders), po); store.Seed(nameof(db.WarehouseRequests), own, foreign);
        var result = await new PurchaseOrderWorkflowService(db, user, new Ids(), new Clock()).CompleteAsync(po.PurchaseOrderId, default);
        Assert.True(result.Success);
        Assert.Equal("Completed", po.Status);
        Assert.Equal(WarehouseRequestStatus.Completed, own.ReqStatus);
        Assert.Equal(WarehouseRequestStatus.Pending, foreign.ReqStatus);
        Assert.True(store.Transaction.Committed);
    }

    [Theory]
    [InlineData("Purchaser", true)]
    [InlineData("SaleUser", false)]
    [InlineData("QCUser", false)]
    public async Task Documents_RequirePurchasingCapability(string role, bool allowed)
    {
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var (db, _) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var service = new PurchaseOrderDocumentService(db, null!, user, new CurrentUserPermissionService(user));
        if (allowed) Assert.False((await service.GetListAsync(Guid.NewGuid(), null)).Success);
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetListAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Documents_RejectAnonymousAndForeignParentBeforeStorage()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        store.Seed(nameof(db.PurchaseOrders), po);
        var doc = new PurchaseOrderDocument { PurchaseOrderDocumentId = Guid.NewGuid(), PurchaseOrderId = po.PurchaseOrderId, PurchaseOrder = po, IsActive = true };
        store.Seed(nameof(db.PurchaseOrderDocuments), doc);
        var service = new PurchaseOrderDocumentService(db, null!, user, new CurrentUserPermissionService(user));
        Assert.False((await service.GetByIdAsync(po.PurchaseOrderId, doc.PurchaseOrderDocumentId)).Success);
        Assert.False((await service.GetAttachmentContentAsync(po.PurchaseOrderId, doc.PurchaseOrderDocumentId, Guid.NewGuid())).Success);
        var anonymous = new IqcWorkflowTests.User { IsAuthenticated = false, Roles = ["Purchaser"] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new PurchaseOrderDocumentService(null!, null!, anonymous, new CurrentUserPermissionService(anonymous)).GetListAsync(po.PurchaseOrderId, null));
    }

    [Fact]
    public void Export_AllocatesPartialReceiptsAndExcess_AndUsesWarehouseReceiptsForAmounts()
    {
        var first = new PurchaseOrderExportLine { PurchaseOrderDetailId = Guid.NewGuid(), POExternalId = "DDH-1", MaterialCode = "MAT", LineNo = 1, OrderedQuantity = 10m };
        var second = new PurchaseOrderExportLine { PurchaseOrderDetailId = Guid.NewGuid(), POExternalId = "DDH-1", MaterialCode = "MAT", LineNo = 2, OrderedQuantity = 5m };
        var receipt = new PurchaseOrderReceiptExportRow { POExternalId = " ddh-1 ", ProductCode = "mat", ReceivedQuantity = 20m, QCResult = QcDecision.Special };
        PurchaseOrderExportRules.ApplyPurchaseOrderReceiptQcQuantities(receipt);
        var rows = PurchaseOrderExportRules.AllocatePurchaseOrderReceipts([first, second], [receipt]);
        Assert.Equal(10m, rows.Where(x => x.PurchaseOrderDetailId == first.PurchaseOrderDetailId).Sum(x => x.AcceptedQuantity));
        Assert.Equal(10m, rows.Where(x => x.PurchaseOrderDetailId == second.PurchaseOrderDetailId).Sum(x => x.AcceptedQuantity));
        Assert.Equal(20m, rows.Sum(x => x.ReceivedQuantity));
        Assert.Equal(2000m, PurchaseOrderExportRules.ResolveRealTotalAmount(rows.Sum(x => x.ReceivedQuantity), 100m));
        Assert.Equal(0m, PurchaseOrderExportRules.ResolveRealTotalAmount(0m, 100m));
    }
    [Theory]
    [InlineData(PurchaseOrderDocumentType.TDS_MSDS, AttachmentSlot.MaterialTds)]
    [InlineData(PurchaseOrderDocumentType.COA, AttachmentSlot.MaterialCoa)]
    [InlineData(PurchaseOrderDocumentType.Acceptance, AttachmentSlot.Other)]
    public void LegacyDocumentCodes_NeverBecomeInternalMailOrComplaint(PurchaseOrderDocumentType type, AttachmentSlot slot)
    {
        Assert.Equal(slot, PurchaseOrderDocumentService.ToStorageSlot(type));
        Assert.NotEqual(AttachmentSlot.InternalMail, PurchaseOrderDocumentService.ToStorageSlot(type));
        Assert.NotEqual(AttachmentSlot.Complaint, PurchaseOrderDocumentService.ToStorageSlot(type));
    }

    [Fact]
    public void PatchWithSoftDelete_PreservesTheSubmittedCancellationNote()
    {
        var po = new PurchaseOrder { Comment = "Old note", PLPUComment = "Old internal note" };
        var request = new UpdatePurchaseOrderNotesRequest { IsActive = false, Comment = "Reason for cancellation", ClearFields = ["plpuComment"] };
        Assert.Null(PurchaseOrderNoteRules.Validate(request));
        PurchaseOrderNoteRules.Apply(po, request);
        Assert.Equal("Reason for cancellation", po.Comment);
        Assert.Null(po.PLPUComment);
    }

    [Fact]
    public void PatchWithSoftDelete_StillRejectsInvalidClearFields()
    {
        Assert.NotNull(PurchaseOrderNoteRules.Validate(new() { IsActive = false, ClearFields = ["companyId"] }));
        Assert.NotNull(PurchaseOrderNoteRules.Validate(new() { IsActive = false, Comment = "note", ClearFields = ["comment"] }));
    }

    private sealed class Ids : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult(prefix + "-1");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult(prefix + "-1");
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 12); }
    public class PurchaseTestStore : IqcTestStore
    {
        public static (IPurchaseOrderDbContext Db, PurchaseTestStore Store) Create()
        {
            var db = DispatchProxy.Create<IPurchaseOrderDbContext, PurchaseTestStore>();
            return (db, (PurchaseTestStore)db);
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == "BeginTransactionAsync" ? Task.FromResult<IDbContextTransaction>(Transaction) : base.Invoke(method, args);
    }
}
