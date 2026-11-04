using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.RollbackProductionOrderStock;
using HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingStockRollbackTests
{
    [Theory]
    [InlineData("PLPUUser")]
    [InlineData("President")]
    [InlineData("Developer")]
    public async Task ReturnsStockedToNewAndPreservesPlanAndSale(string role)
    {
        var f = new Fixture();
        f.User.Roles = [role];
        var result = await f.Run();
        Assert.True(result.Success, result.Message);
        Assert.Equal("New", f.Order.Status);
        Assert.Equal(120, f.Order.TotalQuantity);
        Assert.Equal(new DateTime(2026, 11, 1), f.Order.ExpectedDate);
        Assert.Equal("Processing", f.Sale.Status);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Single(f.Timeline.Events);
        Assert.Empty(f.Store.Removed);
    }

    [Theory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("foreign-company")]
    [InlineData("no-employee")]
    [InlineData("inactive-link")]
    [InlineData("explicit-deny")]
    [InlineData("stale")]
    [InlineData("Finished")]
    [InlineData("New")]
    [InlineData("va")]
    [InlineData("schedule")]
    [InlineData("issue")]
    [InlineData("receipt")]
    public async Task RejectsUnauthorizedStaleOrDependentOrders(string scenario)
    {
        var f = new Fixture();
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default);
        switch (scenario)
        {
            case "LabUser": case "SaleUser": f.User.Roles = [scenario]; break;
            case "foreign-company": f.Order.CompanyId = Guid.NewGuid(); break;
            case "no-employee": f.User.EmployeeId = null; break;
            case "inactive-link": f.Link.IsActive = false; break;
            case "explicit-deny": f.User.HasExplicitPermissionSet = true; f.User.Permissions = []; break;
            case "stale": revision = new string('0', 64); break;
            case "Finished": case "New": f.Order.Status = scenario; revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default); break;
            case "va": f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion { CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId }); revision = await ManufacturingEditorRevision.ReadAsync(f.Db, f.Order, default); break;
            case "schedule": f.Store.Seed(nameof(f.Db.SchedualMfgs), new SchedualMfg { MfgProductionOrderId = f.Order.MfgProductionOrderId }); break;
            case "issue": f.Store.Seed(nameof(f.Db.OperationMaterialRequestLinks), new OperationMaterialRequestLink { CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId }); break;
            case "receipt": f.Store.Seed(nameof(f.Db.ProductionOutputReceiptSources), new ProductionOutputReceiptSource { CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId }); break;
        }
        var result = await f.Run(revision);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Timeline.Events);
    }

    [Fact]
    public void RollbackDoesNotAcceptImplicitHeaderEdits()
    {
        Assert.Null(ProductionOrderEditorMapping.Validate(new() { Action = "RollbackUseStock" }));
        Assert.NotNull(ProductionOrderEditorMapping.Validate(new() { Action = "RollbackUseStock", TotalQuantity = 500 }));
        Assert.Contains("RollbackUseStock", ManufacturingEditorPolicy.Actions("Stocked", true, false, false));
        Assert.DoesNotContain("RollbackUseStock", ManufacturingEditorPolicy.Actions("Stocked", false, true, true, true));
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public MfgProductionOrder Order { get; }
        public MerchandiseOrder Sale { get; }
        public MfgOrderPO Link { get; }
        public Timeline Timeline { get; } = new();
        public Fixture()
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            User.Roles = ["PLPUUser"];
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value,
                IsActive = true, Status = "Stocked", TotalQuantity = 120, ExpectedDate = new(2026, 11, 1) };
            Sale = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true, Status = "Processing" };
            Link = new() { MfgProductionOrderId = Order.MfgProductionOrderId, ProductionOrder = Order, IsActive = true,
                Detail = new() { MerchandiseOrder = Sale, IsActive = true } };
            Store.Seed(nameof(Db.MfgOrderPOs), Link);
        }
        public async Task<HRM.Application.Commons.Models.OperationResult> Run(string? revision = null)
            => await new RollbackProductionOrderStockCommandHandler(Db, User, new CurrentUserPermissionService(User),
                new Clock(), Timeline).Handle(new(Order.MfgProductionOrderId,
                    revision ?? await ManufacturingEditorRevision.ReadAsync(Db, Order, default)), default);
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 28); }
    private sealed class Timeline : IEventLogWriter
    {
        public List<EventLogCreateRequest> Events { get; } = [];
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) { Events.Add(request); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) { Events.AddRange(requests); return Task.CompletedTask; }
    }
}
