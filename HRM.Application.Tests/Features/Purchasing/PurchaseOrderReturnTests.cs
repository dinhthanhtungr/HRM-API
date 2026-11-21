using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Shared.Enums;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderReturnTests
{
    [Fact]
    public async Task CreatesPendingExportForExactFailedMaterialAndLot_WithoutStockOrPoMutation()
    {
        var f = new Fixture();
        var result = await f.Create(35.25m);
        Assert.True(result.Success, result.Message);
        var request = Assert.Single(f.Store.Added.OfType<WarehouseRequest>());
        Assert.Equal(WareHouseRequestType.ExportReturn, request.ReqType);
        Assert.Equal(WarehouseRequestStatus.Pending, request.ReqStatus);
        Assert.Equal("DDH-1", request.codeFromRequest);
        var line = Assert.Single(request.WarehouseRequestDetails);
        Assert.Equal("NVL-1", line.ProductCode);
        Assert.Equal("LOT-1", line.LotNumber);
        Assert.Equal(35.25m, line.WeightKg);
        Assert.Equal(HRM.Domain.Enums.WareHouses.StockType.DefectiveRawMaterial, line.ItemStockType);
        Assert.Equal(PurchaseOrderStatus.ExchangeRequested, f.Order.Status);
        Assert.Equal("PHIẾU YÊU CẦU XUẤT HÀNG TRẢ NCC", request.RequestName);
        var document = Assert.Single(f.Store.Added.OfType<PurchaseOrderDocument>());
        Assert.Equal(f.Order.PurchaseOrderId, document.PurchaseOrderId);
        Assert.Equal(request.RequestCode, document.DocumentCode);
        Assert.Contains("Lý do: QC không đạt", document.Note);
        Assert.Equal(2, f.Store.Added.Count);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(101)] [InlineData(0.001)]
    public async Task RejectsInvalidOrExcessQuantity(decimal kg)
    {
        var f = new Fixture();
        Assert.False((await f.Create(kg)).Success);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task LatestQcWins_AndChangingToPassedInvalidatesPreviouslySelectedLine()
    {
        var f = new Fixture();
        Assert.Single((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Lines);
        f.Store.Seed(nameof(f.Db.QCInputByQCs), f.Qc(QcDecision.QCFail, 1), f.Qc(QcDecision.QCPass, 2));
        Assert.False((await f.Create(1)).Success);
        Assert.Empty((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Lines);
    }

    [Theory]
    [InlineData("foreign")] [InlineData("inactive")] [InlineData("cancelled")]
    [InlineData("export")] [InlineData("wrongCode")] [InlineData("notInPo")]
    public async Task InvalidSourceCannotBeSelected(string kind)
    {
        var f = new Fixture();
        if (kind == "foreign") f.Request.CompanyId = Guid.NewGuid();
        if (kind == "inactive") f.Request.IsActive = false;
        if (kind == "cancelled") f.Request.ReqStatus = WarehouseRequestStatus.Cancelled;
        if (kind == "export") f.Voucher.VoucherType = (int)WareHouseRequestType.ExportReturn;
        if (kind == "wrongCode") f.Request.codeFromRequest = "DDH-10";
        if (kind == "notInPo") f.Detail.ProductCode = "NVL-2";
        Assert.False((await f.Create(1)).Success);
        Assert.Empty(f.Store.Added);
    }

    [Theory]
    [InlineData("SaleUser", true)] [InlineData("PLPUUser", false)]
    public async Task PermissionAndCompanyAreEnforced(string role, bool sameCompany)
    {
        var f = new Fixture(role);
        if (!sameCompany) f.Order.CompanyId = Guid.NewGuid();
        Assert.False((await f.Create(1)).Success);
        Assert.False((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Success);
    }

    [Fact]
    public async Task DuplicateOrderCodeFailsClosed()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.PurchaseOrders), f.Order,
            new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), CompanyId = f.User.CompanyId, ExternalId = " ddh-1 ", IsActive = false });
        Assert.False((await f.Create(1)).Success);
    }

    [Theory]
    [InlineData(WarehouseRequestStatus.Pending, false)]
    [InlineData(WarehouseRequestStatus.Approved, false)]
    [InlineData(WarehouseRequestStatus.Completed, false)]
    [InlineData(WarehouseRequestStatus.Cancelled, true)]
    public async Task ExistingRequestsPreventDuplicatesOrReduceRemaining(WarehouseRequestStatus status, bool allowed)
    {
        var f = new Fixture();
        f.SeedReturn(status, 80);
        var options = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!;
        Assert.Equal(status == WarehouseRequestStatus.Cancelled ? 100m : 20m, Assert.Single(options.Lines).RemainingQuantityKg);
        Assert.Equal(allowed, (await f.Create(30)).Success);
        if (status == WarehouseRequestStatus.Completed) Assert.True((await f.Create(20)).Success);
    }

    [Fact]
    public async Task SameCodeAndLotReceiptsAreGroupedOnce_AndDifferentLotStaysSeparate()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseVoucherDetails), f.Detail,
            new WarehouseVoucherDetail { VoucherDetailId = 11, VoucherId = 1, ProductCode = "NVL-1", LotNumber = "LOT-1", QtyKg = 50, IsIncrease = true },
            new WarehouseVoucherDetail { VoucherDetailId = 12, VoucherId = 1, ProductCode = "NVL-1", LotNumber = "LOT-2", QtyKg = 20, IsIncrease = true });
        var q11 = f.Qc(QcDecision.QCFail, 1); q11.VoucherDetailId = 11;
        var q12 = f.Qc(QcDecision.QCFail, 1); q12.VoucherDetailId = 12;
        f.Store.Seed(nameof(f.Db.QCInputByQCs), f.Qc(QcDecision.QCFail, 1), q11, q12);
        var lines = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(150m, Assert.Single(lines, x => x.LotNumber == "LOT-1").FailedQuantityKg);
    }

    [Fact]
    public async Task SaveFailureRollsBack()
    {
        var f = new Fixture(); f.Store.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Create(10));
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.InProgress, QcDecision.QCPass)]
    [InlineData(PurchaseOrderStatus.Completed, QcDecision.Special)]
    [InlineData(PurchaseOrderStatus.ExchangeRequested, QcDecision.QCPass)]
    public async Task LateDefectsRequireExplicitOptIn_AndKeepAcceptedSourceAndQc(string status, QcDecision decision)
    {
        var f = new Fixture(); f.Order.Status = status;
        var qc = f.Qc(decision, 1);
        f.Store.Seed(nameof(f.Db.QCInputByQCs), qc);
        Assert.Empty((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Lines);
        Assert.False((await f.Create(10)).Success);
        var candidate = Assert.Single((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default, true)).Data!.Lines);
        Assert.Equal(3, candidate.StockType);
        Assert.Equal(100m, candidate.ReceivedQuantityKg);
        Assert.Equal(0m, candidate.FailedQuantityKg);
        var result = await f.Service.CreateAsync(f.Order.PurchaseOrderId, new([new(10, 20)], "Phát hiện lỗi sau QC", IncludeAccepted: true), default);
        Assert.True(result.Success, result.Message);
        var line = Assert.Single(Assert.Single(f.Store.Added.OfType<WarehouseRequest>()).WarehouseRequestDetails);
        Assert.Equal(HRM.Domain.Enums.WareHouses.StockType.RawMaterial, line.ItemStockType);
        Assert.Equal(decision, qc.ImportWarehouseType);
        Assert.Equal(status, f.Order.Status);
    }

    [Theory]
    [InlineData("Pending")] [InlineData("Canceled")] [InlineData("Unknown")]
    public async Task LateDefectModeDoesNotAllowUnreceivedOrderStates(string status)
    {
        var f = new Fixture(); f.Order.Status = status;
        Assert.False((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default, true)).Success);
        Assert.False((await f.Service.CreateAsync(f.Order.PurchaseOrderId, new([new(10, 20)], "reason", IncludeAccepted: true), default)).Success);
    }

    [Theory]
    [InlineData(QcDecision.Waiter)]
    [InlineData(null)]
    public async Task LateDefectModeStillExcludesUninspectedOrHeldGoods(QcDecision? decision)
    {
        var f = new Fixture(); var qc = f.Qc(QcDecision.Waiter, 1); qc.ImportWarehouseType = decision;
        f.Store.Seed(nameof(f.Db.QCInputByQCs), qc);
        Assert.Empty((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default, true)).Data!.Lines);
    }

    [Fact]
    public async Task SameLotWithFailedAndAcceptedSourcesStaysSeparate_AndOldUntypedReturnConsumesTotalOnce()
    {
        var f = new Fixture();
        var accepted = new WarehouseVoucherDetail { VoucherDetailId = 11, VoucherId = 1, ProductCode = "NVL-1", LotNumber = "LOT-1", QtyKg = 50, IsIncrease = true };
        var qc = f.Qc(QcDecision.QCPass, 1); qc.VoucherDetailId = 11;
        f.Store.Seed(nameof(f.Db.WarehouseVoucherDetails), f.Detail, accepted);
        f.Store.Seed(nameof(f.Db.QCInputByQCs), f.Qc(QcDecision.QCFail, 1), qc);
        f.SeedReturn(WarehouseRequestStatus.Completed, 120);
        var lines = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default, true)).Data!.Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(30m, lines.Sum(x => x.RemainingQuantityKg));
        Assert.Equal(0m, Assert.Single(lines, x => x.StockType == 4).RemainingQuantityKg);
        Assert.Equal(30m, Assert.Single(lines, x => x.StockType == 3).RemainingQuantityKg);
    }

    [Fact]
    public async Task ChangingQcAfterCompletedReturnCannotRestoreReturnedQuantity()
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Completed, 80);
        f.Store.Seed(nameof(f.Db.WarehouseRequestDetails), new WarehouseRequestDetail { RequestId = 2, ProductCode = "NVL-1", LotNumber = "LOT-1", WeightKg = 80, ItemStockType = HRM.Domain.Enums.WareHouses.StockType.DefectiveRawMaterial });
        f.Store.Seed(nameof(f.Db.QCInputByQCs), f.Qc(QcDecision.QCPass, 2));
        var line = Assert.Single((await f.Service.ReadAsync(f.Order.PurchaseOrderId, default, true)).Data!.Lines);
        Assert.Equal(20m, line.RemainingQuantityKg);
        Assert.False((await f.Service.CreateAsync(f.Order.PurchaseOrderId, new([new(10, 21)], "reason", IncludeAccepted: true), default)).Success);
    }

    [Fact]
    public async Task RetryAfterCommittedRequestCannotCreateAnotherOpenRequest()
    {
        var f = new Fixture(); f.Store.ReflectAddedOnSave = true;
        Assert.True((await f.Create(10)).Success);
        Assert.False((await f.Create(10)).Success);
        Assert.Single(f.Store.Added.OfType<WarehouseRequest>());
        var options = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!;
        Assert.True(options.HasOpenRequest);
        Assert.Contains("QC không đạt", Assert.Single(options.Requests).ReturnNote);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public async Task NewOperationCanRequestOnlyRemainingQuantityWhilePreviousRequestIsPending(decimal kg, bool allowed)
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Pending, 80);
        var result = await f.Service.CreateAsync(f.Order.PurchaseOrderId,
            new([new(10, kg)], "Đợt tiếp theo", ClientRequestId: Guid.NewGuid()), default);
        Assert.Equal(allowed, result.Success);
        if (allowed) Assert.Equal(20m, Assert.Single(Assert.Single(f.Store.Added.OfType<WarehouseRequest>()).WarehouseRequestDetails).WeightKg);
        else Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task SameOperationIdReturnsSavedRequestWithoutSecondWrite()
    {
        var f = new Fixture(); f.Store.ReflectAddedOnSave = true;
        var command = new CreatePurchaseReturnRequest([new(10, 30)], "QC không đạt", ClientRequestId: Guid.NewGuid());
        var first = await f.Service.CreateAsync(f.Order.PurchaseOrderId, command, default);
        var retry = await f.Service.CreateAsync(f.Order.PurchaseOrderId, command, default);
        Assert.True(first.Success, first.Message); Assert.True(retry.Success, retry.Message);
        Assert.Equal(first.Data!.RequestCode, retry.Data!.RequestCode);
        Assert.Equal(1, f.Store.Saves);
        Assert.Single(f.Store.Added.OfType<WarehouseRequest>());
    }

    [Theory]
    [InlineData(WarehouseRequestStatus.Pending)]
    [InlineData(WarehouseRequestStatus.Completed)]
    public async Task RequestStatusAloneDoesNotProveStockWasReturned(WarehouseRequestStatus status)
    {
        var f = new Fixture(); f.SeedReturn(status, 60);
        var progress = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Progress!;
        Assert.Equal(100m, progress.ReceivedQuantityKg);
        Assert.Equal(0m, progress.PostedQuantityKg);
        Assert.Equal(60m, progress.OutstandingQuantityKg);
        Assert.Equal(40m, progress.RemainingQuantityKg);
    }

    [Theory]
    [InlineData(WarehouseRequestStatus.Pending, 60, 40)]
    [InlineData(WarehouseRequestStatus.Cancelled, 0, 100)]
    public async Task ReversalsAndCancelledRequestsKeepActualAndRemainingCorrect(WarehouseRequestStatus status, decimal outstanding, decimal remaining)
    {
        var f = new Fixture(); f.SeedReturn(status, 60); f.SeedExport(60, 60);
        var progress = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!.Progress!;
        Assert.Equal(0m, progress.PostedQuantityKg);
        Assert.Equal(outstanding, progress.OutstandingQuantityKg);
        Assert.Equal(remaining, progress.RemainingQuantityKg);
    }

    [Fact]
    public async Task CancelledRequestStillCountsAlreadyPostedExportWithoutDoubleCountingRequest()
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Cancelled, 60); f.SeedExport(60, 10);
        var data = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!;
        Assert.Equal(50m, data.Progress!.PostedQuantityKg);
        Assert.Equal(0m, data.Progress.OutstandingQuantityKg);
        Assert.Equal(50m, data.Progress.RemainingQuantityKg);
        Assert.Equal(50m, Assert.Single(data.Lines).RemainingQuantityKg);
    }

    [Fact]
    public async Task PartialActualExportAndOutstandingRequestConsumeQuantityOnce()
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Pending, 60); f.SeedExport(40, 10);
        var data = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!;
        Assert.Equal(30m, data.Progress!.PostedQuantityKg);
        Assert.Equal(30m, data.Progress.OutstandingQuantityKg);
        Assert.Equal(40m, data.Progress.RemainingQuantityKg);
        Assert.Equal(40m, Assert.Single(data.Lines).RemainingQuantityKg);
        var batch = await new PurchaseReturnReadModel(f.Db).ReadAsync(f.User.CompanyId!.Value, ["DDH-1"], default);
        Assert.Equal(data.Progress, batch["DDH-1"].Progress);
    }

    [Fact]
    public async Task PostedMoreThanRequestedStillConsumesTheActualExportQuantity()
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Completed, 20); f.SeedExport(60, 0);
        var data = (await f.Service.ReadAsync(f.Order.PurchaseOrderId, default)).Data!;
        Assert.Equal(60m, data.Progress!.PostedQuantityKg);
        Assert.Equal(0m, data.Progress.OutstandingQuantityKg);
        Assert.Equal(40m, Assert.Single(data.Lines).RemainingQuantityKg);
    }

    [Fact]
    public async Task MissingReasonAndDuplicateSelectionDoNotWrite()
    {
        var f = new Fixture();
        Assert.False((await f.Service.CreateAsync(f.Order.PurchaseOrderId, new([new(10, 1)]), default)).Success);
        Assert.False((await f.Service.CreateAsync(f.Order.PurchaseOrderId, new([new(10, 1), new(10, 2)], "reason"), default)).Success);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task IqcQuantitySyncIgnoresReturnVoucherEvenWithAcceptedQc()
    {
        var f = new Fixture();
        var (db, store) = IqcTestStore.CreateContext<HRM.Application.Abstractions.Persistence.DevAndQA.IIqcDbContext>();
        var line = new PurchaseOrderDetail { MaterialExternalIDSnapshot = "NVL-1", RequestQuantity = 100, IsActive = true };
        f.Order.PurchaseOrderDetails.Add(line);
        store.Seed(nameof(db.PurchaseOrders), f.Order);
        store.Seed(nameof(db.WarehouseRequests), f.Request, new WarehouseRequest { RequestId = 2, CompanyId = f.User.CompanyId!.Value, codeFromRequest = "DDH-1", ReqType = WareHouseRequestType.ExportReturn });
        store.Seed(nameof(db.WarehouseVouchers), f.Voucher, new WarehouseVoucher { VoucherId = 2, RequestId = 2, CompanyId = f.User.CompanyId.Value, VoucherType = 12 });
        store.Seed(nameof(db.WarehouseVoucherDetails), f.Detail, new WarehouseVoucherDetail { VoucherDetailId = 20, VoucherId = 2, ProductCode = "NVL-1", QtyKg = 500, IsIncrease = false });
        var returnedQc = f.Qc(QcDecision.QCPass, 2); returnedQc.VoucherDetailId = 20;
        store.Seed(nameof(db.QCInputByQCs), f.Qc(QcDecision.QCPass, 1), returnedQc);
        await new HRM.Application.Features.DevAndQA.IQC.Services.IqcPurchaseOrderSync(db)
            .SyncAsync(f.Detail, QcDecision.QCPass, f.User.CompanyId.Value, f.User.EmployeeId!.Value, default);
        Assert.Equal(100m, line.RealQuantity);
    }

    [Fact]
    public async Task CompletingPoCannotCompleteOpenReturnRequest()
    {
        var f = new Fixture(); f.SeedReturn(WarehouseRequestStatus.Pending, 10);
        var workflow = new PurchaseOrderWorkflowService(f.Db, f.User, new Codes(), new Clock());
        var result = await workflow.CompleteAsync(f.Order.PurchaseOrderId, default);
        Assert.False(result.Success);
        Assert.Equal(PurchaseOrderStatus.ExchangeRequested, f.Order.Status);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task CompletingPoDoesNotRewriteAlreadyCompletedReturnAudit()
    {
        var f = new Fixture();
        var returned = new WarehouseRequest { RequestId = 2, CompanyId = f.User.CompanyId!.Value, codeFromRequest = "DDH-1",
            ReqType = WareHouseRequestType.ExportReturn, ReqStatus = WarehouseRequestStatus.Completed, UpdatedDate = new(2026, 11, 1) };
        f.Store.Seed(nameof(f.Db.WarehouseRequests), f.Request, returned);
        var workflow = new PurchaseOrderWorkflowService(f.Db, f.User, new Codes(), new Clock());
        Assert.True((await workflow.CompleteAsync(f.Order.PurchaseOrderId, default)).Success);
        Assert.Equal(new DateTime(2026, 11, 1), returned.UpdatedDate);
    }

    private sealed class Fixture
    {
        internal readonly IPurchaseOrderDbContext Db;
        internal readonly IqcTestStore Store;
        internal readonly IqcWorkflowTests.User User;
        internal readonly PurchaseOrder Order;
        internal readonly WarehouseRequest Request;
        internal readonly WarehouseVoucher Voucher;
        internal readonly WarehouseVoucherDetail Detail;
        internal readonly PurchaseOrderReturnService Service;
        internal Fixture(string role = "PLPUUser")
        {
            User = new() { Roles = [role] };
            (Db, Store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
            Order = new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = User.CompanyId, ExternalId = "DDH-1", IsActive = true, Status = PurchaseOrderStatus.ExchangeRequested };
            Request = new() { RequestId = 1, CompanyId = User.CompanyId!.Value, ReqType = WareHouseRequestType.ImportOther, codeFromRequest = "DDH-1" };
            Voucher = new() { VoucherId = 1, RequestId = 1, CompanyId = User.CompanyId.Value, VoucherType = (int)WareHouseRequestType.ImportOther };
            Detail = new() { VoucherDetailId = 10, VoucherId = 1, ProductCode = "NVL-1", ProductName = "Material", LotNumber = "LOT-1", QtyKg = 100, IsIncrease = true };
            Store.Seed(nameof(Db.PurchaseOrders), Order);
            Store.Seed(nameof(Db.PurchaseOrderDetails), new PurchaseOrderDetail { PurchaseOrderId = Order.PurchaseOrderId, IsActive = true,
                Material = new Material { CompanyId = User.CompanyId!.Value, ExternalId = "NVL-1" }, MaterialExternalIDSnapshot = "NVL-1" });
            Store.Seed(nameof(Db.WarehouseRequests), Request);
            Store.Seed(nameof(Db.WarehouseVouchers), Voucher);
            Store.Seed(nameof(Db.WarehouseVoucherDetails), Detail);
            Store.Seed(nameof(Db.QCInputByQCs), Qc(QcDecision.QCFail, 1));
            Store.Seed(nameof(Db.Employees), new Employee { EmployeeId = User.EmployeeId!.Value, CompanyId = User.CompanyId.Value, IsActive = true });
            Service = new(Db, User, new CurrentUserPermissionService(User), new Codes(), new Clock());
        }
        internal QCInputByQC Qc(QcDecision decision, int day) => new() { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 10, ImportWarehouseType = decision, CreatedDate = new(2026, 11, day) };
        internal Task<HRM.Application.Commons.Models.OperationResult<PurchaseReturnRequestDto>> Create(decimal kg) => Service.CreateAsync(Order.PurchaseOrderId, new([new(10, kg)], "QC không đạt"), default);
        internal void SeedReturn(WarehouseRequestStatus status, decimal kg)
        {
            Store.Seed(nameof(Db.WarehouseRequests), Request, new WarehouseRequest { RequestId = 2, RequestCode = "PRQ-return", CompanyId = User.CompanyId!.Value,
                codeFromRequest = "DDH-1", ReqType = WareHouseRequestType.ExportReturn, ReqStatus = status });
            Store.Seed(nameof(Db.WarehouseRequestDetails), new WarehouseRequestDetail { RequestId = 2, ProductCode = "NVL-1", LotNumber = "LOT-1", WeightKg = kg });
        }
        internal void SeedExport(decimal exported, decimal reversed)
        {
            var voucher = new WarehouseVoucher { VoucherId = 2, RequestId = 2, CompanyId = User.CompanyId!.Value, VoucherType = 12 };
            var detail = new WarehouseVoucherDetail { VoucherDetailId = 20, VoucherId = 2, ProductCode = "NVL-1", LotNumber = "LOT-1", IsIncrease = false, QtyKg = exported };
            Store.Seed(nameof(Db.WarehouseVouchers), Voucher, voucher);
            Store.Seed(nameof(Db.WarehouseVoucherDetails), Detail, detail);
            Store.Seed(nameof(Db.WarehouseShelfLedgers),
                new WarehouseShelfLedger { LedgerId = 1, CompanyId = User.CompanyId.Value, VoucherId = 2, VoucherDetailId = 20, ProductCode = "NVL-1", LotNumber = "LOT-1", DeltaKg = -exported },
                new WarehouseShelfLedger { LedgerId = 2, CompanyId = User.CompanyId.Value, VoucherId = 2, VoucherDetailId = 20, ProductCode = "NVL-1", LotNumber = "LOT-1", DeltaKg = reversed },
                new WarehouseShelfLedger { LedgerId = 3, CompanyId = Guid.NewGuid(), VoucherId = 2, VoucherDetailId = 20, ProductCode = "NVL-1", LotNumber = "LOT-1", DeltaKg = -999 });
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 9); }
    private sealed class Codes : IExternalIdService
    {
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => Task.FromResult("PRQ-new");
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => Task.FromResult("PRQ-new");
    }
}
