using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderReceiptProgress;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderReceiptProgressTests
{
    [Fact]
    public async Task QcTransfersAndMultipleInspectionsDoNotMultiplyReceipt()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.QCInputByQCs),
            new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 10, CreatedDate = new(2026, 10, 1), ImportWarehouseType = QcDecision.Waiter },
            new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 10, CreatedDate = new(2026, 10, 2), ImportWarehouseType = QcDecision.QCPass });
        f.Store.Seed(nameof(f.Db.WarehouseShelfLedgers),
            f.Move(1, 10, 100, StockType.Waiter), f.Move(2, 10, -100, StockType.Waiter),
            f.Move(3, 10, 70, StockType.RawMaterial), f.Move(4, 10, 30, StockType.DefectiveRawMaterial),
            // Consumption belongs to another detail; neither it nor a foreign-company row changes receipt progress.
            f.Move(5, 99, -40, StockType.RawMaterial),
            new WarehouseShelfLedger { LedgerId = 6, CompanyId = Guid.NewGuid(), VoucherDetailId = 10, DeltaKg = 9999 });
        var result = (await f.Read())!;
        var material = Assert.Single(result.Materials);
        Assert.Equal(100m, material.RecordedQuantity);
        Assert.Equal(100m, material.PostedQuantity);
        Assert.Equal(100m, material.PassedQuantity);
        Assert.Equal(100m, material.PostingPercent);
        Assert.Equal(0m, material.RemainingToPost);
        var receipt = Assert.Single(result.Receipts);
        Assert.Equal("Passed", receipt.QcStatus);
        Assert.Equal(3, receipt.Postings.Count);
        Assert.Equal(70m, Assert.Single(receipt.Postings, x => x.StockType == 3).NetQuantity);
        Assert.Equal(30m, Assert.Single(receipt.Postings, x => x.StockType == 4).NetQuantity);
        Assert.Contains("MultipleQcRecordsLatestSelected", receipt.Warnings);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task AppliedFlagDoesNotInventPosting_AndReversalNetsToZero()
    {
        var f = new Fixture();
        var result = (await f.Read())!;
        Assert.Equal(0m, Assert.Single(result.Materials).PostedQuantity);
        Assert.Contains("AppliedWithoutLedger", Assert.Single(result.Receipts).Warnings);
        f.Store.Seed(nameof(f.Db.WarehouseShelfLedgers), f.Move(1, 10, 100), f.Move(2, 10, -100));
        result = (await f.Read())!;
        Assert.Equal(0m, Assert.Single(result.Materials).PostedQuantity);
        Assert.Equal(100m, Assert.Single(result.Materials).RemainingToPost);
    }

    [Fact]
    public async Task DuplicateCodeIncludingInactivePoFailsClosed_ButForeignDuplicateDoesNot()
    {
        var f = new Fixture();
        var duplicate = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), CompanyId = f.Company, ExternalId = " ddh-1 ", IsActive = false };
        f.Store.Seed(nameof(f.Db.PurchaseOrders), f.Order, duplicate);
        var result = (await f.Read())!;
        Assert.Equal("AmbiguousOrderCode", result.AssociationStatus);
        Assert.Null(Assert.Single(result.Materials).PostedQuantity);
        Assert.Empty(result.Receipts);
        duplicate.CompanyId = Guid.NewGuid();
        Assert.Equal("Matched", (await f.Read())!.AssociationStatus);
    }

    [Fact]
    public async Task MissingCodeAndAbsentRequestAreDistinguished()
    {
        var f = new Fixture();
        f.Order.ExternalId = " ";
        var result = (await f.Read())!;
        Assert.Equal("MissingOrderCode", result.AssociationStatus);
        Assert.Null(Assert.Single(result.Materials).PostedQuantity);
        f.Order.ExternalId = "DDH-2";
        result = (await f.Read())!;
        Assert.Equal("NoRequests", result.AssociationStatus);
        Assert.Equal(0m, Assert.Single(result.Materials).PostedQuantity);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("cancelled")]
    [InlineData("inactive")]
    [InlineData("export")]
    [InlineData("partialCode")]
    public async Task RequestSourceMustMatchCompanyActiveImportAndExactCode(string mode)
    {
        var f = new Fixture();
        if (mode == "foreign") f.Request.CompanyId = Guid.NewGuid();
        if (mode == "cancelled") f.Request.ReqStatus = WarehouseRequestStatus.Cancelled;
        if (mode == "inactive") f.Request.IsActive = false;
        if (mode == "export") f.Request.ReqType = WareHouseRequestType.ExportOther;
        if (mode == "partialCode") f.Request.codeFromRequest = "DDH-10";
        var result = (await f.Read())!;
        Assert.Empty(result.Requests);
        Assert.Empty(result.Receipts);
    }

    [Fact]
    public async Task RepeatedPoLinesAreGrouped_AndMultipleLotsAndLocationsStaySeparate()
    {
        var f = new Fixture();
        var secondLine = new PurchaseOrderDetail { PurchaseOrderDetailId = Guid.NewGuid(), PurchaseOrderId = f.Order.PurchaseOrderId,
            MaterialExternalIDSnapshot = " MAT ", Material = f.Material, RequestQuantity = 100, IsActive = true };
        f.Store.Seed(nameof(f.Db.PurchaseOrderDetails), f.OrderLine, secondLine);
        var secondReceipt = new WarehouseVoucherDetail { VoucherDetailId = 11, VoucherId = 1, ProductCode = "MAT",
            UnitName = "kg", QtyKg = 50, LotNumber = "LOT-2", IsIncrease = true };
        f.Store.Seed(nameof(f.Db.WarehouseVoucherDetails), f.Detail, secondReceipt);
        var move = f.Move(2, 11, 50); move.LotNumber = "LOT-2"; move.SlotId = 2;
        f.Store.Seed(nameof(f.Db.WarehouseShelfLedgers), f.Move(1, 10, 100), move);
        var result = (await f.Read())!;
        var material = Assert.Single(result.Materials);
        Assert.Equal(200m, material.OrderedQuantity);
        Assert.Equal(150m, material.PostedQuantity);
        Assert.Equal(75m, material.PostingPercent);
        Assert.Equal(2, material.PurchaseOrderDetailIds.Count);
        Assert.Equal(2, result.Receipts.Count);
        Assert.Contains("RepeatedOrderMaterialGrouped", result.Warnings);
    }

    [Fact]
    public async Task ForeignVoucherWrongMaterialAndUnitAreNotSilentlyAllocated()
    {
        var f = new Fixture();
        f.Detail.ProductCode = "OTHER";
        var result = (await f.Read())!;
        Assert.Equal(0m, Assert.Single(result.Materials).RecordedQuantity);
        Assert.Contains("UnmatchedOrderMaterialOrUnit", Assert.Single(result.Receipts).Warnings);
        f.Voucher.CompanyId = Guid.NewGuid();
        Assert.Empty((await f.Read())!.Receipts);
    }

    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("PLPUUser", false)]
    public async Task PermissionAndAuthenticationAreRequired(string role, bool authenticated)
    {
        var f = new Fixture();
        var user = new IqcWorkflowTests.User { Roles = [role], IsAuthenticated = authenticated, CompanyId = f.Company };
        var handler = new GetPurchaseOrderReceiptProgressQueryHandler(f.Db, user, new CurrentUserPermissionService(user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(f.Order.PurchaseOrderId), default));
    }

    [Fact]
    public async Task ParentScopeIncludesInactiveOnlyExplicitly()
    {
        var f = new Fixture();
        f.Order.IsActive = false;
        Assert.Null(await f.Read());
        Assert.NotNull(await f.Handler.Handle(new(f.Order.PurchaseOrderId, true), default));
        f.Order.CompanyId = Guid.NewGuid();
        Assert.Null(await f.Handler.Handle(new(f.Order.PurchaseOrderId, true), default));
    }

    [Fact]
    public void ReceiptProjectionTranslatesWithoutQcLedgerFanOut()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new IqcWorkflowTests.User { Roles = ["PLPUUser"] };
        var handler = new GetPurchaseOrderReceiptProgressQueryHandler(db, user, new CurrentUserPermissionService(user));
        var sql = handler.ReceiptRows(db.WarehouseRequests.Where(x => x.CompanyId == user.CompanyId && x.codeFromRequest == "DDH-1"), user.CompanyId!.Value).ToQueryString();
        Assert.Contains("WarehouseVoucherDetails", sql);
        Assert.DoesNotContain("QCInputByQC", sql);
        Assert.DoesNotContain("WarehouseShelfLedger", sql);
        Assert.Contains("isIncrease", sql);
    }

    [Theory]
    [InlineData(QcDecision.Waiter, "OnHold")]
    [InlineData(QcDecision.QCFail, "Failed")]
    [InlineData(QcDecision.Special, "SpecialAccepted")]
    [InlineData(null, "NotInspected")]
    public async Task QcDecisionDoesNotInventOrMultiplyLedger(QcDecision? decision, string expected)
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.QCInputByQCs), new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 10,
            CreatedDate = new(2026, 10, 1), ImportWarehouseType = decision });
        var result = (await f.Read())!;
        Assert.Equal(expected, Assert.Single(result.Receipts).QcStatus);
        Assert.Equal(100m, Assert.Single(result.Materials).RecordedQuantity);
        Assert.Equal(0m, Assert.Single(result.Materials).PostedQuantity);
    }

    [Fact]
    public async Task UnbalancedPositiveTransferIsFlagged_NotReportedAsReliableCompletion()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfLedgers), f.Move(1, 10, 100, StockType.Waiter), f.Move(2, 10, 100));
        var result = (await f.Read())!;
        var material = Assert.Single(result.Materials);
        Assert.Equal(200m, material.PostedQuantity); // Preserve raw evidence; do not cap and hide the inconsistency.
        Assert.True(material.RequiresReview);
        Assert.Null(material.PostingPercent);
        Assert.Contains("LedgerQuantityDiffersFromVoucher", Assert.Single(result.Receipts).Warnings);
        f.Voucher.Status = " Cancelled ";
        result = (await f.Read())!;
        Assert.False(Assert.Single(result.Receipts).Included);
        Assert.Equal(0m, Assert.Single(result.Materials).PostedQuantity);
    }

    private sealed class Fixture
    {
        internal readonly IPurchaseOrderDbContext Db;
        internal readonly IqcTestStore Store;
        internal readonly IqcWorkflowTests.User User = new() { Roles = ["PLPUUser"] };
        internal Guid Company => User.CompanyId!.Value;
        internal readonly PurchaseOrder Order;
        internal readonly Material Material;
        internal readonly PurchaseOrderDetail OrderLine;
        internal readonly WarehouseRequest Request;
        internal readonly WarehouseVoucher Voucher;
        internal readonly WarehouseVoucherDetail Detail;
        internal readonly GetPurchaseOrderReceiptProgressQueryHandler Handler;
        internal Fixture()
        {
            (Db, Store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
            Order = new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = Company, ExternalId = "DDH-1", IsActive = true };
            Material = new() { CompanyId = Company, ExternalId = "MAT", Unit = "kg" };
            OrderLine = new() { PurchaseOrderDetailId = Guid.NewGuid(), PurchaseOrderId = Order.PurchaseOrderId,
                Material = Material, MaterialExternalIDSnapshot = "MAT", RequestQuantity = 100, IsActive = true };
            Request = new() { RequestId = 1, CompanyId = Company, IsActive = true, ReqType = WareHouseRequestType.ImportOther,
                codeFromRequest = " DDH-1 ", RequestCode = "PRQ-1" };
            Voucher = new() { VoucherId = 1, CompanyId = Company, RequestId = 1, VoucherType = (int)WareHouseRequestType.ImportOther };
            Detail = new() { VoucherDetailId = 10, VoucherId = 1, QtyKg = 100, UnitName = "kg", ProductCode = "MAT",
                IsApplied = true, IsIncrease = true, LotNumber = "LOT-1" };
            Store.Seed(nameof(Db.PurchaseOrders), Order);
            Store.Seed(nameof(Db.PurchaseOrderDetails), OrderLine);
            Store.Seed(nameof(Db.WarehouseRequests), Request);
            Store.Seed(nameof(Db.WarehouseVouchers), Voucher);
            Store.Seed(nameof(Db.WarehouseVoucherDetails), Detail);
            Handler = new(Db, User, new CurrentUserPermissionService(User));
        }
        internal Task<HRM.Application.Features.Purchasing.PurchaseOrders.Dtos.PurchaseOrderReceiptProgressDto?> Read() =>
            Handler.Handle(new(Order.PurchaseOrderId), default);
        internal WarehouseShelfLedger Move(long id, long detail, decimal delta, StockType type = StockType.RawMaterial) => new()
        { LedgerId = id, CompanyId = Company, VoucherId = 1, VoucherDetailId = detail, ProductCode = "MAT", LotNumber = "LOT-1",
            SlotId = 1, StockType = type, DeltaKg = delta, CreatedAt = new(2026, 10, 1) };
    }
}
