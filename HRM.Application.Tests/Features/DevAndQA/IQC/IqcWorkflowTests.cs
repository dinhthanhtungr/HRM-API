using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcSummary;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcExcel;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcWorkflowTests
{
    [Fact]
    public async Task ExchangeRequestKeepsStatusAcrossQcSyncAndIsIdempotent()
    {
        var (db, store, user, detail) = Fixture();
        var po = SeedPo(db, store, user, detail);
        po.Status = "Completed";
        var handler = new HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange.RequestPurchaseOrderExchangeCommandHandler(
            db, user, new CurrentUserPermissionService(user), new());
        var result = await handler.Handle(new(detail.VoucherDetailId), default);
        Assert.Equal(po.PurchaseOrderId, result.PurchaseOrderId);
        Assert.Equal("ExchangeRequested", po.Status);
        Assert.Equal(user.EmployeeId, po.UpdatedBy);
        var saves = store.Saves;
        await handler.Handle(new(detail.VoucherDetailId), default);
        Assert.Equal(saves, store.Saves);
        await new IqcPurchaseOrderSync(db).SyncAsync(detail, QcDecision.QCPass, user.CompanyId!.Value, user.EmployeeId!.Value, default);
        Assert.Equal("ExchangeRequested", po.Status);
    }

    [Fact]
    public async Task ExchangeRequestRejectsMissingForeignAndCanceledOrder()
    {
        var (db, store, user, detail) = Fixture();
        var handler = new HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange.RequestPurchaseOrderExchangeCommandHandler(
            db, user, new CurrentUserPermissionService(user), new());
        var missing = await Assert.ThrowsAsync<HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException>(
            () => handler.Handle(new(detail.VoucherDetailId), default));
        Assert.Equal(404, missing.StatusCode);
        var po = SeedPo(db, store, user, detail);
        po.Status = "Canceled";
        var canceled = await Assert.ThrowsAsync<HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException>(
            () => handler.Handle(new(detail.VoucherDetailId), default));
        Assert.Equal(409, canceled.StatusCode);
        detail.Voucher.CompanyId = Guid.NewGuid();
        var foreign = await Assert.ThrowsAsync<HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException>(
            () => handler.Handle(new(detail.VoucherDetailId), default));
        Assert.Equal(404, foreign.StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("KHOUser")]
    public async Task ExchangeRequestRequiresManageIqc(string role)
    {
        var user = new User { Roles = [role] };
        var handler = new HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange.RequestPurchaseOrderExchangeCommandHandler(
            null!, user, new CurrentUserPermissionService(user), new());
        var error = await Assert.ThrowsAsync<HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException>(
            () => handler.Handle(new(1), default));
        Assert.Equal(403, error.StatusCode);
    }

    [Theory]
    [InlineData("QCUser", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("KHOUser", true, false)]
    [InlineData("SaleUser", false, false)]
    [InlineData("LabUser", true, true)]
    public void ReadAndManageCapabilities_AreIndependent(string role, bool view, bool manage)
    {
        var user = new User { Roles = [role] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(view, permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc));
        Assert.Equal(manage, permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc));
        Assert.False(permissions.HasPermission("dev-and-qa.iqc.unknown"));
    }

    [Fact]
    public async Task SaleAndWarehouseMutation_FailBeforeDatabaseAccess()
    {
        var sale = new User { Roles = ["SaleUser"] };
        var permissions = new CurrentUserPermissionService(sale);
        Assert.False((await new GetIqcSummaryQueryHandler(null!, sale, permissions).Handle(new(new()), default)).Success);
        Assert.False((await new GetIqcDetailQueryHandler(null!, sale, permissions).Handle(new(1), default)).Success);
        Assert.False((await new ExportIqcExcelQueryHandler(null!, sale, null!, permissions).Handle(new(new()), default)).Success);
        foreach (var user in new[] { sale, new User { Roles = ["KHOUser"] } })
        {
            var writer = new IqcWriter(null!, user, new(), new CurrentUserPermissionService(user), null!);
            Assert.False((await writer.CreateAsync(new() { VoucherDetailId = 1 }, default)).Success);
            Assert.False((await writer.PatchAsync(1, new(), default)).Success);
        }
    }

    [Fact]
    public async Task AnonymousAndMissingCompany_FailBeforeDatabaseAccess()
    {
        foreach (var user in new[] { new User { IsAuthenticated = false }, new User { CompanyId = null } })
        {
            Assert.False((await new GetIqcSummaryQueryHandler(null!, user, new CurrentUserPermissionService(user)).Handle(new(new()), default)).Success);
            Assert.False((await new GetIqcDetailQueryHandler(null!, user, new CurrentUserPermissionService(user)).Handle(new(1), default)).Success);
            Assert.False((await new ExportIqcExcelQueryHandler(null!, user, null!, new CurrentUserPermissionService(user)).Handle(new(new()), default)).Success);
            var writer = new IqcWriter(null!, user, new(), new CurrentUserPermissionService(user), null!);
            Assert.False((await writer.CreateAsync(new() { VoucherDetailId = 1 }, default)).Success);
            Assert.False((await writer.PatchAsync(1, new(), default)).Success);
        }
    }

    [Fact]
    public async Task CrossCompanyVoucher_CannotBeCreatedOrPatched()
    {
        var (db, store, user, detail) = Fixture();
        detail.Voucher.CompanyId = Guid.NewGuid();
        var writer = new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!);
        Assert.False((await writer.CreateAsync(new() { VoucherDetailId = detail.VoucherDetailId }, default)).Success);
        Assert.False((await writer.PatchAsync(detail.VoucherDetailId, new() { ImportWarehouseType = QcDecision.QCPass }, default)).Success);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
        Assert.False(store.Transaction.Committed);
        Assert.Equal(VoucherDetailType.Waiter, detail.VoucherType);
    }

    [Fact]
    public async Task Detail_ReturnsCreateContextWithoutQcAndHidesForeignCompany()
    {
        var (db, _, user, detail) = Fixture();
        var handler = new GetIqcDetailQueryHandler(db, user, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new(detail.VoucherDetailId), default);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.QCInputByQCId);
        Assert.Equal(detail.ProductName, result.Data.MaterialName);
        Assert.Equal(detail.QtyKg, result.Data.QtyKg);
        detail.Voucher.CompanyId = Guid.NewGuid();
        var foreign = await handler.Handle(new(detail.VoucherDetailId), default);
        Assert.True(foreign.Success);
        Assert.Null(foreign.Data);
    }

    [Fact]
    public async Task Detail_SelectsLatestStoredQc()
    {
        var (db, store, user, detail) = Fixture();
        var older = Existing(detail, user, QcDecision.QCFail);
        var latest = Existing(detail, user, QcDecision.Special);
        latest.CreatedDate = older.CreatedDate.AddDays(1);
        store.Seed(nameof(db.QCInputByQCs), older, latest);
        var result = await new GetIqcDetailQueryHandler(db, user, new CurrentUserPermissionService(user))
            .Handle(new(detail.VoucherDetailId), default);
        Assert.True(result.Success);
        Assert.Equal(latest.QCInputByQCId, result.Data!.QCInputByQCId);
        Assert.Equal(QcDecision.Special, result.Data.ImportWarehouseType);
        Assert.Equal(latest.CreatedDate, result.Data.QCCreatedDate);
    }

    [Fact]
    public async Task ExplicitPermissions_DoNotFallbackToQcRole()
    {
        var user = new User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.DevAndQA.ViewIqc] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc));
        Assert.False(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc));
        Assert.False((await new IqcWriter(null!, user, new(), permissions, null!).CreateAsync(new() { VoucherDetailId = 1 }, default)).Success);
    }

    [Fact]
    public async Task Create_UsesVoucherSnapshotCreatesCollectionAndCountsCurrentAcceptedRow()
    {
        var (db, store, user, detail) = Fixture();
        var po = SeedPo(db, store, user, detail);
        var result = await new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!).CreateAsync(new()
        {
            VoucherDetailId = detail.VoucherDetailId, MaterialName = "client spoof", MaterialExternalId = "wrong",
            ImportWarehouseType = QcDecision.Special, HasNewAttachments = true
        }, default);
        Assert.True(result.Success);
        var qc = Assert.Single(store.Added.OfType<QCInputByQC>());
        Assert.Equal(detail.ProductName, qc.MaterialName);
        Assert.Equal(detail.ProductCode, qc.MaterialExternalId);
        Assert.Equal(user.EmployeeId, qc.CreatedBy);
        Assert.NotNull(qc.AttachmentCollectionId);
        Assert.Equal(AttachmentUploadStatus.Pending, qc.AttachmentStatus);
        Assert.Equal(VoucherDetailType.Special, detail.VoucherType);
        Assert.Equal(25, po.PurchaseOrderDetails.Single(x => x.LineNo == 2).RealQuantity);
        Assert.Equal(0, po.PurchaseOrderDetails.Single(x => x.LineNo == 1).RealQuantity);
        Assert.Equal("InProgress", po.Status);
        Assert.True(store.Transaction.Committed);
    }

    [Fact]
    public async Task DuplicatePost_IgnoresInputAndResynchronizesStoredDecision()
    {
        var (db, store, user, detail) = Fixture();
        var qc = Existing(detail, user, QcDecision.QCPass);
        store.Seed(nameof(db.QCInputByQCs), qc);
        var po = SeedPo(db, store, user, detail);
        var result = await new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!).CreateAsync(new()
        {
            VoucherDetailId = detail.VoucherDetailId, ImportWarehouseType = QcDecision.QCFail, Note = "replace"
        }, default);
        Assert.True(result.Success);
        Assert.Equal(qc.QCInputByQCId, result.Data!.QCInputByQCId);
        Assert.Equal(QcDecision.QCPass, result.Data.ImportWarehouseType);
        Assert.Null(qc.Note);
        Assert.Empty(store.Added);
        Assert.Equal(25, po.PurchaseOrderDetails.Single(x => x.LineNo == 2).RealQuantity);
        Assert.Equal(1, store.Saves);
    }

    [Theory]
    [InlineData(QcDecision.QCPass, QcDecision.QCFail, 0)]
    [InlineData(QcDecision.QCFail, QcDecision.Special, 25)]
    public async Task Patch_RecalculatesAcceptedQuantityAfterDecisionChange(QcDecision before, QcDecision after, int expectedQty)
    {
        var (db, store, user, detail) = Fixture();
        var qc = Existing(detail, user, before);
        store.Seed(nameof(db.QCInputByQCs), qc);
        var po = SeedPo(db, store, user, detail);
        var result = await new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!).PatchAsync(detail.VoucherDetailId,
            new() { ImportWarehouseType = after }, default);
        Assert.True(result.Success);
        Assert.Equal(expectedQty, po.PurchaseOrderDetails.Single(x => x.LineNo == 2).RealQuantity);
        Assert.Equal((VoucherDetailType)(int)after, detail.VoucherType);
        Assert.Equal(expectedQty == 0 ? "Pending" : "InProgress", po.Status);
        Assert.Equal(2, store.Saves);
        Assert.Equal(qc.CreatedDate, result.Data!.QCCreatedDate);
        Assert.True(store.Transaction.Committed);
    }

    [Fact]
    public async Task CollectionFromOtherCompany_IsRejectedBeforeMutation()
    {
        var (db, store, user, detail) = Fixture();
        var otherDetail = new WarehouseVoucherDetail { Voucher = new() { CompanyId = Guid.NewGuid() } };
        var foreign = Existing(otherDetail, user, QcDecision.Waiter);
        foreign.AttachmentCollectionId = Guid.NewGuid();
        store.Seed(nameof(db.QCInputByQCs), foreign);
        var result = await new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!).CreateAsync(new()
        { VoucherDetailId = detail.VoucherDetailId, AttachmentCollectionId = foreign.AttachmentCollectionId }, default);
        Assert.False(result.Success);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task SaveFailure_DoesNotCommitTransaction()
    {
        var (db, store, user, detail) = Fixture();
        store.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new IqcWriter(db, user, new(), new CurrentUserPermissionService(user), null!).CreateAsync(
            new() { VoucherDetailId = detail.VoucherDetailId }, default));
        Assert.False(store.Transaction.Committed);
        Assert.True(store.Transaction.RolledBack);
    }

    private static (IIqcDbContext Db, IqcTestStore Store, User User, WarehouseVoucherDetail Detail) Fixture()
    {
        var (db, store) = IqcTestStore.CreateStore();
        var user = new User();
        var detail = new WarehouseVoucherDetail
        {
            VoucherDetailId = 1, VoucherId = 10, LineNo = 2, ProductCode = "MAT", ProductName = "Resin",
            QtyKg = 25, VoucherType = VoucherDetailType.Waiter,
            Voucher = new() { VoucherId = 10, CompanyId = user.CompanyId!.Value, RequestId = 20 }
        };
        store.Seed(nameof(db.WarehouseVoucherDetails), detail);
        store.Seed(nameof(db.WarehouseVouchers), detail.Voucher);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value });
        return (db, store, user, detail);
    }

    private static PurchaseOrder SeedPo(IIqcDbContext db, IqcTestStore store, User user, WarehouseVoucherDetail detail)
    {
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest
        { RequestId = 20, CompanyId = user.CompanyId!.Value, codeFromRequest = " PO-1 ", IsActive = true });
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId, ExternalId = "PO-1", IsActive = true,
            PurchaseOrderDetails = [
                new() { LineNo = 1, MaterialExternalIDSnapshot = detail.ProductCode, IsActive = true, RequestQuantity = 50, RealQuantity = 0 },
                new() { LineNo = 2, MaterialExternalIDSnapshot = detail.ProductCode, IsActive = true, RequestQuantity = 50, RealQuantity = 0 }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        return po;
    }

    private static QCInputByQC Existing(WarehouseVoucherDetail detail, User user, QcDecision decision) => new()
    {
        QCInputByQCId = Guid.NewGuid(), VoucherDetailId = detail.VoucherDetailId,
        WarehouseVoucherDetail = detail, CreatedBy = user.EmployeeId!.Value,
        CreatedDate = new DateTime(2026, 10, 1), ImportWarehouseType = decision
    };

    internal sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => "iqc-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = ["QCUser"];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
