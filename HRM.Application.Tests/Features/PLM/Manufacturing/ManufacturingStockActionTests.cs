using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingStockActionTests
{
    [Fact]
    public async Task Stock_UpdatesLinkedOrderAndExistingScheduleWithoutFormulaOrReservation()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, Request()), default);
        Assert.True(result.Success);
        Assert.Equal("Stocked", f.Order.Status);
        Assert.Equal("Processing", f.Sale.Status);
        Assert.Null(f.Schedule.DeliveryPlanDate);
        Assert.Equal(120m, f.Order.TotalQuantity);
        Assert.Equal(2, f.Order.NumOfBatches);
        Assert.Equal(f.User.EmployeeId, f.Order.UpdatedBy);
        Assert.Equal(2, f.Timeline.Events.Count);
        Assert.Empty(f.Store.Added);
        Assert.Equal(1, f.Store.Saves);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("Stocked")]
    [InlineData("Scheduling")]
    [InlineData("Finished")]
    [InlineData("FormulaRequested")]
    public async Task Stock_RequiresNewAndDoesNotRepeatMutation(string status)
    {
        var f = new Fixture();
        f.Order.Status = status;
        Assert.False((await f.Handler.Handle(new(f.Order.MfgProductionOrderId, Request()), default)).Success);
        Assert.Equal(status, f.Order.Status);
        Assert.Empty(f.Timeline.Events);
        Assert.Equal(0, f.Store.Saves);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(-1, 2, 0)]
    [InlineData(120, 0, 0)]
    [InlineData(120, 2, 999)]
    public async Task Stock_ValidatesFormerFrontendRulesOnServer(int quantity, int batches, int step)
    {
        var f = new Fixture();
        var request = new UseProductionOrderStockRequest
        { TotalQuantity = quantity, NumOfBatches = batches, StepOfProduct = (StepOfProduct)step };
        Assert.False((await f.Handler.Handle(new(f.Order.MfgProductionOrderId, request), default)).Success);
        Assert.Equal("New", f.Order.Status);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("sale")]
    [InlineData("company")]
    [InlineData("inactive")]
    [InlineData("missing-link")]
    public async Task Stock_DeniesUnauthorizedOrOutOfScopeReferences(string reason)
    {
        var f = new Fixture();
        if (reason == "sale") f.User.Roles = [ApplicationRoles.Sales.SaleUser];
        if (reason == "company") f.Sale.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Detail.IsActive = false;
        if (reason == "missing-link") f.Store.Seed<MfgOrderPO>(nameof(f.Db.MfgOrderPOs));
        Assert.False((await f.Handler.Handle(new(f.Order.MfgProductionOrderId, Request()), default)).Success);
        Assert.Equal("New", f.Order.Status);
        Assert.Empty(f.Timeline.Events);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task Stock_SaveFailureReturnsGenericFailureAndInvokesRollback()
    {
        var f = new Fixture();
        f.Store.ThrowOnSave = true;
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, Request()), default);
        Assert.False(result.Success);
        Assert.DoesNotContain("Simulated", result.Message ?? string.Empty);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task FormulaOptions_StandardWinsAndDifferentVuAddsPendingImprovement()
    {
        var f = new Fixture();
        f.Order.ProductId = Guid.NewGuid();
        f.Order.FormulaId = Guid.NewGuid();
        f.Order.CreatedDate = new(2026, 10, 10);
        f.Order.PlpuNote = "PLPU hiện tại";
        f.Order.LabNote = "QA/sản xuất hiện tại";
        f.Order.Requirement = "Lưu ý khách hiện tại";
        var va = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), ExternalId = "VA001",
            CompanyId = f.Order.CompanyId, IsActive = true };
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula
        { ProductId = f.Order.ProductId, CompanyId = f.Order.CompanyId, ManufacturingFormulaId = va.ManufacturingFormulaId,
            ManufacturingFormula = va, ValidFrom = new(2026, 10, 1) });
        f.Store.Seed(nameof(f.Db.Formulas), new Formula
        { FormulaId = f.Order.FormulaId.Value, ProductId = f.Order.ProductId, CompanyId = f.Order.CompanyId, IsActive = true,
            Product = new() { ProductId = f.Order.ProductId, CompanyId = f.Order.CompanyId, IsActive = true, Requirement = "QA từ yêu cầu phối mẫu" } });
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), new MfgProductionOrder
        { MfgProductionOrderId = Guid.NewGuid(), ProductId = f.Order.ProductId, CompanyId = f.Order.CompanyId,
            IsActive = true, FormulaId = Guid.NewGuid(), CreatedDate = new(2026, 10, 9) });
        var options = await ManufacturingFormulaOptionReader.ReadAsync(f.Db, f.Order, default);
        Assert.Equal(2, options.Count);
        Assert.Equal(ManufacturingFormulaOrigin.Improvement, options[0].Origin);
        Assert.True(options[0].RequiresFormulaReview);
        Assert.Equal(ManufacturingFormulaOrigin.Standard, options[1].Origin);
        Assert.True(options[1].RequiresFormulaReview);
        Assert.Equal(f.Order.PlpuNote, options[0].PlpuNote);
        Assert.Equal("QA từ yêu cầu phối mẫu", options[0].LabNote);
        Assert.Equal(f.Order.Requirement, options[0].Requirement);
        Assert.Null(options[1].PlpuNote);
        Assert.Null(options[1].LabNote);
        Assert.Null(options[1].Requirement);
    }

    [Theory]
    [InlineData(2025, ManufacturingFormulaOrigin.ProductionOld)]
    [InlineData(2026, ManufacturingFormulaOrigin.Production)]
    public async Task FormulaOptions_HistoryNotesBelongToSourceOrder(int year, ManufacturingFormulaOrigin origin)
    {
        var f = new Fixture();
        f.Order.FormulaId = null;
        f.Order.PlpuNote = "Do not use current order notes";
        var source = new MfgProductionOrder
        {
            MfgProductionOrderId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            ProductId = f.Order.ProductId, IsActive = true,
            PlpuNote = "PLPU nguồn", LabNote = "QA/sản xuất nguồn", Requirement = "Lưu ý khách nguồn"
        };
        var va = new ManufacturingFormula
        {
            ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            IsActive = true, ExternalId = "VA-SOURCE", CreatedDate = new(year, 9, 1), Note = "Formula note"
        };
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion
        {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = source.MfgProductionOrderId,
            MfgProductionOrder = source, ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va
        });
        var option = Assert.Single(await ManufacturingFormulaOptionReader.ReadAsync(f.Db, f.Order, default));
        Assert.Equal(origin, option.Origin);
        Assert.Equal(source.MfgProductionOrderId, option.SourceProductionOrderId);
        Assert.Equal(va.Note, option.Note);
        Assert.Equal(source.PlpuNote, option.PlpuNote);
        Assert.Equal(source.LabNote, option.LabNote);
        Assert.Equal(source.Requirement, option.Requirement);
        source.PlpuNote = null;
        source.LabNote = "";
        source.Requirement = " ";
        option = Assert.Single(await ManufacturingFormulaOptionReader.ReadAsync(f.Db, f.Order, default));
        Assert.Null(option.PlpuNote);
        Assert.Equal("", option.LabNote);
        Assert.Equal(" ", option.Requirement);
    }

    [Fact]
    public async Task FormulaOptions_VuFallbackIsProductAndCompanyScoped()
    {
        var f = new Fixture();
        f.Order.ProductId = Guid.NewGuid();
        f.Order.FormulaId = Guid.NewGuid();
        f.Store.Seed(nameof(f.Db.Formulas), new Formula
        { FormulaId = f.Order.FormulaId.Value, ProductId = f.Order.ProductId, CompanyId = Guid.NewGuid(), IsActive = true });
        Assert.Empty(await ManufacturingFormulaOptionReader.ReadAsync(f.Db, f.Order, default));
    }

    [Fact]
    public async Task StockAction_RejectsStaleRevisionInsideTransactionWithoutSideEffects()
    {
        var f = new Fixture();
        var revision = await HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders.ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        f.Order.LabNote = "Another editor saved";
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, Request()) { ExpectedRevision = revision }, default);
        Assert.False(result.Success);
        Assert.Equal("New", f.Order.Status);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Timeline.Events);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    private static UseProductionOrderStockRequest Request() => new()
    { TotalQuantity = 120, NumOfBatches = 2, StepOfProduct = Enum.GetValues<StepOfProduct>()[0], ExpectedDate = null };

    [Fact]
    public async Task StandardNotes_UseLatestScopedActualMfg_NotStandardChangeReason()
    {
        var f = new Fixture();
        f.Order.ProductId = Guid.NewGuid(); f.Order.FormulaId = null;
        var va = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            ExternalId = "VA-NOTES", IsActive = true, Note = "Production instruction" };
        var old = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            ProductId = f.Order.ProductId, IsActive = true, LabNote = "Old QA" };
        var latest = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            ProductId = f.Order.ProductId, IsActive = true, LabNote = "Latest QA", PlpuNote = "Latest PLPU",
            Requirement = "Customer", QcCheck = "QC check" };
        var foreign = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            ProductId = f.Order.ProductId, IsActive = true, LabNote = "FOREIGN" };
        ProductionSelectVersion Row(MfgProductionOrder m, DateTime? date) => new() {
            ProductionSelectVersionId = Guid.NewGuid(), CompanyId = f.Order.CompanyId, MfgProductionOrder = m,
            MfgProductionOrderId = m.MfgProductionOrderId, ManufacturingFormulaId = va.ManufacturingFormulaId,
            ManufacturingFormula = va, ValidFrom = date };
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula {
            ProductId = f.Order.ProductId, CompanyId = f.Order.CompanyId, ManufacturingFormulaId = va.ManufacturingFormulaId,
            ManufacturingFormula = va, Note = "Reason to mark standard" });
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), Row(old, new(2026, 1, 1)), Row(latest, new(2026, 2, 1)),
            Row(foreign, new(2026, 3, 1)), Row(old, null));
        var source = Assert.Single(await ManufacturingFormulaOptionReader.ReadAsync(f.Db, f.Order, default));
        Assert.Equal(latest.MfgProductionOrderId, source.SourceProductionOrderId);
        Assert.Equal("Production instruction", source.Note);
        Assert.Equal("Latest QA", source.LabNote);
        Assert.Equal("Latest PLPU", source.PlpuNote);
        Assert.Equal("QC check", source.QcCheck);
        Assert.Equal("Customer", source.Requirement);
    }
    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public MfgProductionOrder Order { get; }
        public MerchandiseOrder Sale { get; }
        public MerchandiseOrderDetail Detail { get; }
        public SchedualMfg Schedule { get; }
        public Timeline Timeline { get; } = new();
        public UseProductionOrderStockCommandHandler Handler { get; }
        public Fixture()
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, Status = "New" };
            Sale = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true, Status = "Approved" };
            Detail = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Sale.MerchandiseOrderId, MerchandiseOrder = Sale, IsActive = true };
            Schedule = new() { MfgProductionOrderId = Order.MfgProductionOrderId, DeliveryPlanDate = new(2026, 10, 12) };
            Store.Seed(nameof(Db.MfgOrderPOs), new MfgOrderPO
            { MfgProductionOrderId = Order.MfgProductionOrderId, ProductionOrder = Order, Detail = Detail, IsActive = true });
            Store.Seed(nameof(Db.MerchandiseOrders), Sale);
            Store.Seed(nameof(Db.SchedualMfgs), Schedule);
            Handler = new(Db, User, new CurrentUserPermissionService(User), new Clock(), Timeline, PromisedDeliveryTestFactory.Create(Db, User).Notifier);
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 10, 12, 0, 0); }
    private sealed class Timeline : IEventLogWriter
    {
        public List<EventLogCreateRequest> Events { get; } = [];
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) { Events.Add(request); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) { Events.AddRange(requests); return Task.CompletedTask; }
    }
}
