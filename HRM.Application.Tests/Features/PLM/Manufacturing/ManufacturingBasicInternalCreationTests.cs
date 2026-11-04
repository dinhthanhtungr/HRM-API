using System.Reflection;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingBasicInternalCreationTests
{
    [Theory]
    [InlineData(true, 20)]
    [InlineData(false, 12.5)]
    public async Task BasicCreate_ResolvesCostOnServerAndUsesExistingTransactionalWriter(bool current, decimal expected)
    {
        var f = new Fixture();
        if (current) f.Prices.Data[new(ItemType.Material, f.Base.Material.MaterialId)] = new() { CurrentPrice = 20 };
        var result = await f.Handler.Handle(new(f.Request()), default);
        Assert.True(result.Success, result.Message);
        var write = Assert.IsType<CreateInternalProductionOrderCommand>(f.Sender.Request);
        Assert.Null(write.Request.InitialStatus);
        Assert.Equal(expected, write.Request.UnitPriceAgreed);
        Assert.Equal(f.Base.SourceId, write.Request.FormulaId);
        Assert.Equal(ManufacturingFormulaCreationTests.Fixture.Now.Date, write.Request.RequiredDate);
        Assert.Empty(f.Base.Store.Added);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("company")]
    [InlineData("product")]
    [InlineData("empty-id")]
    [InlineData("bag")]
    public async Task BasicCreate_RejectsInvalidScopeBeforeInvokingWriter(string reason)
    {
        var f = new Fixture();
        if (reason == "role") f.Base.User.Roles = [ApplicationRoles.Sales.SaleUser];
        if (reason == "company") f.Base.Db.Formulas.Single().CompanyId = Guid.NewGuid();
        if (reason == "product") f.Base.Db.Formulas.Single().ProductId = Guid.NewGuid();
        var request = reason == "empty-id" ? f.Request(Guid.Empty) : f.Request(bag: reason == "bag" ? "" : "bag");
        Assert.False((await f.Handler.Handle(new(request), default)).Success);
        Assert.Null(f.Sender.Request);
        Assert.Empty(f.Base.Store.Added);
    }

    private sealed class Fixture
    {
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.FromVu);
        public Prices Prices { get; }
        public Sender Sender { get; }
        public CreateBasicInternalProductionOrderCommandHandler Handler { get; }
        public Fixture()
        {
            var prices = DispatchProxy.Create<IMaterialPriceQueryService, Prices>();
            Prices = (Prices)(object)prices;
            var sender = DispatchProxy.Create<ISender, Sender>();
            Sender = (Sender)(object)sender;
            Handler = new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), prices, new Clock(), sender);
        }
        public CreateBasicInternalProductionOrderRequest Request(Guid? formulaId = null, string bag = "bag") => new() {
            ProductId = Base.Order.ProductId, FormulaId = formulaId ?? Base.SourceId, BagType = bag, TotalQuantityRequest = 1 };
    }
    public class Prices : DispatchProxy
    {
        public Dictionary<PriceItemKey, LatestItemPriceDto> Data { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IMaterialPriceQueryService.LoadLatestPricingItemPriceInfoDictAsync), method!.Name);
            return Task.FromResult(Data);
        }
    }
    public class Sender : DispatchProxy
    {
        public object? Request;
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            Request = args![0];
            return Task.FromResult(OperationResult<Guid>.Ok(Guid.NewGuid()));
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => ManufacturingFormulaCreationTests.Fixture.Now; }
}
