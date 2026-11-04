using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

public sealed partial class ProductionOrderCreationTests
{
    [Fact]
    public async Task Internal_CreatesOnlyMfgAndTimeline_EvenWithSchedulingAndDetailId()
    {
        var fixture = new Fixture();
        var result = await fixture.Internal.Handle(new(new()
        {
            ProductId = fixture.Product.ProductId, TotalQuantityRequest = 20, BagType = "Bag",
            InitialStatus = " Scheduling ", MerchandiseOrderDetailId = fixture.Detail.MerchandiseOrderDetailId,
            RequiredDate = Fixture.Now, UnitPriceAgreed = 100
        }), default);
        Assert.True(result.Success, result.Message);
        var order = Assert.IsType<MfgProductionOrder>(Assert.Single(fixture.Store.Added));
        Assert.Equal(result.Data, order.MfgProductionOrderId);
        Assert.Equal("Scheduling", order.Status);
        Assert.Equal("DB-PRODUCT", order.ProductNameSnapshot);
        Assert.Equal("P001", order.ProductExternalIdSnapshot);
        Assert.Equal(fixture.User.CompanyId, order.CompanyId);
        Assert.Equal(fixture.User.EmployeeId, order.CreatedBy);
        Assert.Single(fixture.Timeline.Logs);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.True(fixture.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Inform_WithoutActivePositiveItems_CreatesNewWithoutFormulaScheduleOrReservation()
    {
        var fixture = new Fixture();
        var request = fixture.Request();
        request.TotalQuantity = 200;
        request.FormulaItems = [new() { Quantity = 1, IsActive = false }, new() { Quantity = 0 }];
        var result = await fixture.Inform.Handle(new(request), default);
        Assert.True(result.Success, result.Message);
        var order = Assert.Single(fixture.Store.Added.OfType<MfgProductionOrder>());
        Assert.Equal("New", order.Status);
        Assert.Equal("REQUEST-PRODUCT", order.ProductNameSnapshot);
        Assert.Null(result.Data!.ManufacturingFormulaId);
        Assert.Equal(2, fixture.Store.Added.Count);
        Assert.Single(fixture.Store.Added.OfType<MfgOrderPO>());
        Assert.Single(fixture.Timeline.Logs);
        Assert.Equal("Approved", fixture.SaleOrder.Status); // Query link trước SaveChanges như legacy.
        Assert.True(fixture.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Inform_WithFormula_PreservesVaSelectionScheduleAndReservationSemantics()
    {
        var fixture = new Fixture();
        var request = fixture.RequestWithFormula();
        var result = await fixture.Inform.Handle(new(request), default);
        Assert.True(result.Success, result.Message);
        var order = Assert.Single(fixture.Store.Added.OfType<MfgProductionOrder>());
        var formula = Assert.Single(fixture.Store.Added.OfType<ManufacturingFormula>());
        Assert.Equal("Scheduling", order.Status);
        Assert.Equal("Checking", formula.Status);
        Assert.Equal("COPY", formula.Name);
        Assert.Equal(FormulaSource.FromVA, formula.SourceType);
        Assert.Null(formula.SourceManufacturingFormulaId);
        Assert.Equal(14m, formula.TotalPrice);
        var materials = formula.ManufacturingFormulaMaterials.OrderBy(x => x.LineNo).ToArray();
        Assert.Equal(new[] { 1, 2 }, materials.Select(x => x.LineNo));
        Assert.Equal(ItemType.MaterialFailure, materials[0].itemType);
        Assert.Equal("LOT-1", materials[0].LotNo);
        Assert.Equal(ItemType.Product, materials[1].itemType); // Failure + không có lot lỗi trở lại Product.
        Assert.Null(materials[1].LotNo);
        var selection = Assert.Single(fixture.Store.Added.OfType<ProductionSelectVersion>());
        Assert.Equal(Fixture.Now, selection.ValidFrom);
        Assert.Null(selection.ValidTo);
        Assert.Equal(formula.ManufacturingFormulaId, selection.ManufacturingFormulaId);
        var schedule = Assert.Single(fixture.Store.Added.OfType<SchedualMfg>());
        Assert.Equal("LAB", schedule.Note);
        Assert.Equal(request.ExpectedDate, schedule.DeliveryPlanDate);
        var reserves = fixture.Store.Added.OfType<WarehouseTempStock>().OrderBy(x => x.Code).ToArray();
        Assert.Equal(new[] { "M001", "P001" }, reserves.Select(x => x.Code));
        Assert.Equal(new[] { 60m, 40m }, reserves.Select(x => x.QtyRequest!.Value));
        Assert.All(reserves, x =>
        {
            Assert.Equal(order.ExternalId, x.VaCode);
            Assert.Equal(fixture.User.CompanyId, x.CompanyId);
            Assert.Equal("Open", x.ReserveStatus);
            Assert.Equal(0m, x.QtyUsed);
            Assert.Null(x.LotKey);
        });
        Assert.Equal(formula.ExternalId, result.Data!.ManufacturingFormulaExternalId);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.True(fixture.Store.Transaction.Committed);
        Assert.Equal("Approved", fixture.SaleOrder.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Inform_NonPositiveTotalQuantity_CreatesFormulaWithoutReservation(int? quantity)
    {
        var fixture = new Fixture();
        var request = fixture.RequestWithFormula();
        request.TotalQuantity = quantity;
        var result = await fixture.Inform.Handle(new(request), default);
        Assert.True(result.Success, result.Message);
        Assert.Single(fixture.Store.Added.OfType<ManufacturingFormula>());
        Assert.Empty(fixture.Store.Added.OfType<WarehouseTempStock>());
    }

    [Fact]
    public async Task Inform_NoReservableCodes_RollsBackWholeBundleWithoutSaving()
    {
        var fixture = new Fixture();
        var request = fixture.RequestWithFormula();
        request.FormulaItems = [new()
        {
            ItemId = fixture.Material.MaterialId, CategoryId = fixture.Category.CategoryId,
            Quantity = 1, ItemType = ItemType.Material, MaterialExternalIdSnapshot = " "
        }];
        var result = await fixture.Inform.Handle(new(request), default);
        Assert.False(result.Success);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.True(fixture.Store.Transaction.RolledBack);
        Assert.False(fixture.Store.Transaction.Committed);
        Assert.Empty(fixture.Timeline.Logs);
    }

    [Fact]
    public async Task Inform_SaveFailure_RollsBackAndDoesNotExposeException()
    {
        var fixture = new Fixture();
        fixture.Store.ThrowOnSave = true;
        var result = await fixture.Inform.Handle(new(fixture.RequestWithFormula()), default);
        Assert.False(result.Success);
        Assert.DoesNotContain("Simulated", result.Message!);
        Assert.True(fixture.Store.Transaction.RolledBack);
        Assert.False(fixture.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("product")]
    [InlineData("material")]
    [InlineData("category")]
    public async Task Inform_CrossCompanyReference_IsRejectedBeforeAdding(string reference)
    {
        var fixture = new Fixture();
        var otherCompany = Guid.NewGuid();
        if (reference == "order") fixture.SaleOrder.CompanyId = otherCompany;
        if (reference == "product") fixture.Product.CompanyId = otherCompany;
        if (reference == "material") fixture.Material.CompanyId = otherCompany;
        if (reference == "category") fixture.Category.CompanyId = otherCompany;
        var result = await fixture.Inform.Handle(new(fixture.RequestWithFormula()), default);
        Assert.False(result.Success);
        Assert.Empty(fixture.Store.Added);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.True(fixture.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task Internal_CrossCompanyProduct_IsRejected()
    {
        var fixture = new Fixture();
        fixture.Product.CompanyId = Guid.NewGuid();
        var result = await fixture.Internal.Handle(new(new()
        {
            ProductId = fixture.Product.ProductId, TotalQuantityRequest = 1, BagType = "Bag"
        }), default);
        Assert.False(result.Success);
        Assert.Empty(fixture.Store.Added);
        Assert.True(fixture.Store.Transaction.RolledBack);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("vu")]
    [InlineData("va")]
    public async Task Inform_OptionalCrossCompanyReference_IsRejected(string reference)
    {
        var fixture = new Fixture();
        var request = fixture.RequestWithFormula();
        var otherCompany = Guid.NewGuid();
        if (reference == "customer")
        {
            request.CustomerId = Guid.NewGuid();
            fixture.Store.Seed("Customers", new HRM.Domain.Entities.CustomerSchema.Customer
                { CustomerId = request.CustomerId.Value, CompanyId = otherCompany, IsActive = true });
        }
        if (reference == "vu")
        {
            request.FormulaCustomerSelect = Guid.NewGuid();
            fixture.Store.Seed("Formulas", new Formula
                { FormulaId = request.FormulaCustomerSelect, ProductId = fixture.Product.ProductId, CompanyId = otherCompany, IsActive = true });
        }
        if (reference == "va")
        {
            request.ManufacturingFormulaIdIsSelect = Guid.NewGuid();
            fixture.Store.Seed("ManufacturingFormulas", new ManufacturingFormula
                { ManufacturingFormulaId = request.ManufacturingFormulaIdIsSelect.Value, CompanyId = otherCompany, IsActive = true });
        }
        var result = await fixture.Inform.Handle(new(request), default);
        Assert.False(result.Success);
        Assert.Empty(fixture.Store.Added);
        Assert.True(fixture.Store.Transaction.RolledBack);
    }

}
