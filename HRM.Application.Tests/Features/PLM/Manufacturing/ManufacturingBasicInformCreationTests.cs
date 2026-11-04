using System.Reflection;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInformProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingBasicInformCreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_ResolvesOrderPriceSnapshotsAndOptionalFormulaOnServer(bool withFormula)
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new(f.Request(withFormula ? null : [])), default);
        Assert.True(result.Success, result.Message);
        var write = Assert.IsType<CreateProductionOrderInformCommand>(f.Sender.Request).Request;
        Assert.Equal(f.Base.Detail.ProductId, write.ProductId);
        Assert.Equal(f.Base.Sale.CustomerId, write.CustomerId);
        Assert.Equal(20m, write.UnitPriceAgreed);
        Assert.Equal("DATABASE-PRODUCT", write.ProductExternalIdSnapshot);
        Assert.Equal("DATABASE-SALE", write.MerchandiseOrderExternalId);
        Assert.Equal(withFormula ? 1 : 0, write.FormulaItems.Count);
        if (withFormula) Assert.Equal(12.5m, write.FormulaItems[0].UnitPrice);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("detail")]
    [InlineData("customer")]
    [InlineData("product")]
    [InlineData("source")]
    [InlineData("role")]
    public async Task Create_RejectsInvalidReferencesWithoutInvokingWriter(string reason)
    {
        var f = new Fixture();
        if (reason == "company") f.Base.Sale.CompanyId = Guid.NewGuid();
        if (reason == "detail") f.Base.Detail.IsActive = false;
        if (reason == "customer") f.Base.Db.Customers.Single().CompanyId = Guid.NewGuid();
        if (reason == "product") f.Base.Db.Products.Single().IsActive = false;
        if (reason == "source") f.Base.Db.Formulas.Single().ProductId = Guid.NewGuid();
        if (reason == "role") f.Base.User.Roles = [ApplicationRoles.Sales.SaleUser];
        var result = await f.Handler.Handle(new(f.Request([])), default);
        Assert.False(result.Success);
        Assert.Null(f.Sender.Request);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Create_RequiresProcessStepBeforeLoadingReferences()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new(new CreateBasicInformProductionOrderRequest
        {
            MerchandiseOrderId = f.Base.Sale.MerchandiseOrderId,
            MerchandiseOrderDetailId = f.Base.Detail.MerchandiseOrderDetailId,
            RequiredDate = ManufacturingFormulaCreationTests.Fixture.Now
        }), default);
        Assert.False(result.Success);
        Assert.Null(f.Sender.Request);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Create_RejectsForgedSourceItemWithoutInvokingWriter()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new(f.Request([new ManufacturingFormulaInputItem
        {
            SourceItemId = Guid.NewGuid(), ItemId = f.Base.Material.MaterialId,
            ItemType = HRM.Domain.Enums.Formulas.ItemType.Material, Quantity = 1
        }])), default);
        Assert.False(result.Success);
        Assert.Null(f.Sender.Request);
    }

    private sealed class Fixture
    {
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.FromVu);
        public CaptureSender Sender { get; }
        public CreateBasicInformProductionOrderCommandHandler Handler { get; }

        public Fixture()
        {
            Base.Sale.CustomerId = Guid.NewGuid();
            Base.Sale.ExternalId = "DATABASE-SALE";
            Base.Detail.ProductExternalIdSnapshot = "DATABASE-PRODUCT";
            Base.Store.Seed(nameof(Base.Db.MerchandiseOrderDetails), Base.Detail);
            Base.Store.Seed(nameof(Base.Db.Customers), new Customer
            {
                CustomerId = Base.Sale.CustomerId, CompanyId = Base.Order.CompanyId, IsActive = true
            });
            var sender = DispatchProxy.Create<ISender, CaptureSender>();
            Sender = (CaptureSender)(object)sender;
            var prices = DispatchProxy.Create<HRM.Application.Abstractions.Commons.Pricing.IMaterialPriceQueryService,
                ManufacturingBasicInternalCreationTests.Prices>();
            Handler = new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User),
                new ManufacturingFormulaInputResolver(Base.Db, prices, new ManufacturingFormulaCreationTests.Clock(), new CurrentUserPermissionService(Base.User)), sender);
        }

        public CreateBasicInformProductionOrderRequest Request(List<ManufacturingFormulaInputItem>? items) => new()
        {
            MerchandiseOrderId = Base.Sale.MerchandiseOrderId,
            MerchandiseOrderDetailId = Base.Detail.MerchandiseOrderDetailId,
            FormulaId = Base.SourceId, RequiredDate = ManufacturingFormulaCreationTests.Fixture.Now,
            StepOfProduct = (StepOfProduct)0,
            Items = items
        };
    }

    public class CaptureSender : DispatchProxy
    {
        public object? Request { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Request = args![0];
            return Task.FromResult(OperationResult<CreateProductionOrderInformResult>.Ok(
                new CreateProductionOrderInformResult(Guid.NewGuid(), "MFG", null, null)));
        }
    }
}
