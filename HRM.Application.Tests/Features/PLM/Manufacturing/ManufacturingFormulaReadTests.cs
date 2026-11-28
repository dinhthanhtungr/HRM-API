using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaSource;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using System.Reflection;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaEditor;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;

using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaReadTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MaterialSource_UsesSavedCostsWithoutPriceProviderAndMasksUnauthorizedCosts(bool allowed)
    {
        var f = new Fixture(allowed);
        var visibility = new PLMFieldVisibilityService(new CurrentUserPermissionService(f.Base.User));
        var reader = new ManufacturingFormulaReadService(f.Db, f.Db, visibility, (IMaterialPriceQueryService)(object)f.Prices);
        var handler = new HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaSource.GetManufacturingFormulaSourceQueryHandler(
            f.Db, f.Base.User, new CurrentUserPermissionService(f.Base.User), visibility, reader);
        var result = await handler.Handle(new(f.Va.ManufacturingFormulaId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(allowed ? f.Base.Db.ManufacturingFormulaMaterials.Single().UnitPrice : (decimal?)null,
            Assert.Single(result.Data!.Items).UnitPrice);
        Assert.Equal(0, f.Prices.Calls);
        f.Va.CompanyId = Guid.NewGuid();
        Assert.False((await handler.Handle(new(f.Va.ManufacturingFormulaId), default)).Success);
    }

    [Fact]
    public async Task Choices_ReturnsOneScopedFormulaAndCurrentStandardFromLatestSelection()
    {
        var f = new Fixture(false);
        f.Selection.ValidFrom = Fixture.Now.AddDays(-1);
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula {
            CompanyId = f.Base.Order.CompanyId, ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Va.ManufacturingFormulaId,
            ValidFrom = Fixture.Now.AddDays(-2), ValidTo = Fixture.Now.AddDays(1) });
        var handler = new HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices.GetManufacturingFormulaChoicesQueryHandler(
            f.Db, f.Base.User, new CurrentUserPermissionService(f.Base.User), new Clock());
        var result = await handler.Handle(new(new() { OrderId = f.Base.Order.MfgProductionOrderId }), default);
        Assert.True(result.Success, result.Message);
        var choice = Assert.Single(result.Data!.Items);
        Assert.True(choice.IsSelected);
        Assert.True(choice.IsStandard);
        Assert.Equal(f.Base.Order.ExternalId, choice.OrderExternalId);
        f.Base.Order.CompanyId = Guid.NewGuid();
        Assert.Empty((await handler.Handle(new(new() { OrderId = f.Base.Order.MfgProductionOrderId }), default)).Data!.Items);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Editor_MasksPricesBeforeCallingPriceProvider(bool allowed)
    {
        var f = new Fixture(allowed);
        var result = await f.Editor.Handle(new(f.Base.Order.MfgProductionOrderId, f.Va.ManufacturingFormulaId), default);
        Assert.True(result.Success, result.Message);
        var row = Assert.Single(result.Data!.Items);
        Assert.Equal("current name", row.Name);
        Assert.Equal(allowed ? 20m : null, row.UnitPrice);
        Assert.Equal(allowed ? 20m : null, result.Data.TotalPrice);
        Assert.Equal(allowed ? Fixture.Now : (DateTime?)null, row.PriceDate);
        Assert.Equal(allowed ? 1 : 0, f.Prices.Calls);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("mismatched-vu")]
    public async Task Prefill_UsesProductStandardOnlyWhenVuProvenanceMatches(string scenario)
    {
        var f = new Fixture(false);
        f.Va.SourceVUFormulaId = scenario == "standard" ? f.Base.Order.FormulaId : Guid.NewGuid();
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula { CompanyId = f.Base.Order.CompanyId,
            ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Va.ManufacturingFormulaId, ValidFrom = Fixture.Now.AddDays(-1) });
        var result = await f.Editor.Handle(new(f.Base.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Null(result.Data!.FormulaId);
        Assert.Equal(scenario == "standard" ? FormulaSource.FromVA : FormulaSource.FromVU, result.Data.SourceType);
        Assert.Equal(scenario == "standard" ? f.Va.ManufacturingFormulaId : f.Base.Order.FormulaId, result.Data.SourceFormulaId);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("unrelated")]
    [InlineData("inactive")]
    public async Task View_RejectsForeignOrUnassociatedFormulaBeforePriceQuery(string reason)
    {
        var f = new Fixture(true);
        if (reason == "foreign") f.Va.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Va.IsActive = false;
        Assert.False((await f.Editor.Handle(new(f.Base.Order.MfgProductionOrderId,
            reason == "unrelated" ? Guid.NewGuid() : f.Va.ManufacturingFormulaId), default)).Success);
        Assert.Equal(0, f.Prices.Calls);
    }

    [Fact]
    public async Task Compare_KeepsClosedLatestStandardAndNeverQueriesPrices()
    {
        var f = new Fixture(true);
        var previous = new ProductStandardFormula { ProductStandardFormulaId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId,
            ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Va.ManufacturingFormulaId, ValidFrom = Fixture.Now.AddDays(-2), ValidTo = Fixture.Now.AddDays(-1) };
        var latest = new ProductStandardFormula { ProductStandardFormulaId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId,
            ProductId = f.Base.Order.ProductId, ManufacturingFormulaId = f.Va.ManufacturingFormulaId, ValidFrom = Fixture.Now.AddDays(-1), ValidTo = Fixture.Now };
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), previous, latest);
        var result = await f.List.Handle(new(new() { Source = FormulaSource.FromVA }, Compare: true), default);
        Assert.True(result.Success, result.Message);
        var va = Assert.Single(result.Data!.Items);
        Assert.Null(va.TotalPrice);
        Assert.Null(Assert.Single(va.Items).UnitPrice);
        Assert.Equal(0, f.Prices.Calls);
    }

    [Fact]
    public async Task List_PreservesPsvHistoryDuplicatesAndPricesOnceForPage()
    {
        var f = new Fixture(true);
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), f.Selection, new ProductionSelectVersion {
            CompanyId = f.Base.Order.CompanyId, MfgProductionOrderId = f.Base.Order.MfgProductionOrderId,
            ManufacturingFormulaId = f.Va.ManufacturingFormulaId, ValidTo = Fixture.Now });
        var result = await f.List.Handle(new(new() { Source = FormulaSource.FromVA }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, x => Assert.Equal(20m, x.TotalPrice));
        Assert.Equal(1, f.Prices.Calls);
    }

    [Fact]
    public async Task RequestedSource_UsesSavedVaInsteadOfProductStandard()
    {
        var f = new Fixture(false);
        f.Base.Order.Status = "FormulaRequested";
        f.Va.Note = "Chosen source instruction";
        var other = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Base.Order.CompanyId,
            IsActive = true, ExternalId = "VA-OTHER", SourceVUFormulaId = f.Base.Order.FormulaId };
        f.Store.Seed(nameof(f.Db.ManufacturingFormulas), f.Va, other);
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula {
            CompanyId = f.Base.Order.CompanyId, ProductId = f.Base.Order.ProductId,
            ManufacturingFormulaId = other.ManufacturingFormulaId, ManufacturingFormula = other, ValidFrom = Fixture.Now.AddDays(-1) });
        f.Selection.ManufacturingFormula = f.Va;
        f.Selection.ValidFrom = null;
        var result = await f.Editor.Handle(new(f.Base.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(f.Va.ManufacturingFormulaId, result.Data!.SourceFormulaId);
        Assert.Equal("Chosen source instruction", result.Data.Note);
    }

    [Fact]
    public async Task RequestedVu_UsesSampleRequestProductRequirement_EvenWhenStandardExists()
    {
        var f = new Fixture(false);
        f.Base.Order.Status = "FormulaRequested";
        f.Base.Order.Product.Requirement = "QA/QC/SX from sample request";
        f.Va.SourceVUFormulaId = f.Base.Order.FormulaId;
        f.Store.Seed<ProductionSelectVersion>(nameof(f.Db.ProductionSelectVersions));
        f.Store.Seed(nameof(f.Db.ProductStandardFormulas), new ProductStandardFormula {
            CompanyId = f.Base.Order.CompanyId, ProductId = f.Base.Order.ProductId,
            ManufacturingFormulaId = f.Va.ManufacturingFormulaId, ManufacturingFormula = f.Va, ValidFrom = Fixture.Now.AddDays(-1) });
        var result = await f.Editor.Handle(new(f.Base.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(f.Base.Order.FormulaId, result.Data!.SourceFormulaId);
        Assert.Equal("QA/QC/SX from sample request", result.Data.Note);
    }
    private sealed class Fixture
    {
        public static readonly DateTime Now = ManufacturingFormulaCreationTests.Fixture.Now;
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.FromVu);
        public ManufacturingQuantityExportTests.TestContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public ManufacturingFormula Va { get; }
        public ProductionSelectVersion Selection { get; }
        public PriceProvider Prices { get; }
        public GetManufacturingFormulaEditorQueryHandler Editor { get; }
        public GetManufacturingFormulasQueryHandler List { get; }
        public Fixture(bool priceAllowed)
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<ManufacturingQuantityExportTests.TestContext>();
            Base.User.HasExplicitPermissionSet = true;
            Base.User.Permissions = priceAllowed ? [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials,
                ApplicationPermissions.PLM.ViewFormulaPrices] : [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
            Va = Base.Db.ManufacturingFormulas.Single();
            Selection = new() { CompanyId = Base.Order.CompanyId, MfgProductionOrderId = Base.Order.MfgProductionOrderId,
                ManufacturingFormulaId = Va.ManufacturingFormulaId, ValidFrom = Now };
            Store.Seed(nameof(Db.MfgProductionOrders), Base.Order);
            Store.Seed(nameof(Db.MfgOrderPOs), Base.Db.MfgOrderPOs.Single());
            Store.Seed(nameof(Db.Products), Base.Order.Product);
            Store.Seed(nameof(Db.Materials), Base.Material);
            Store.Seed(nameof(Db.Formulas), Base.Db.Formulas.Single());
            Store.Seed(nameof(Db.FormulaMaterials), Base.Db.FormulaMaterials.Single());
            Store.Seed(nameof(Db.ManufacturingFormulas), Va);
            Store.Seed(nameof(Db.ManufacturingFormulaMaterials), Base.Db.ManufacturingFormulaMaterials.Single());
            Store.Seed(nameof(Db.ProductionSelectVersions), Selection);
            var prices = DispatchProxy.Create<IMaterialPriceQueryService, PriceProvider>();
            Prices = (PriceProvider)(object)prices;
            Prices.Data[new(ItemType.Material, Base.Material.MaterialId)] = new() { CurrentPrice = 20, PriceDate = Now };
            var permissions = new CurrentUserPermissionService(Base.User);
            var visibility = new PLMFieldVisibilityService(permissions);
            var reader = new ManufacturingFormulaReadService(Db, Db, visibility, prices);
            var sender = DispatchProxy.Create<ISender, Sender>();
            Editor = new(Db, Base.User, permissions, visibility, new Clock(), reader, sender);
            List = new(Db, Base.User, permissions, visibility, reader);
        }
    }
    public class PriceProvider : DispatchProxy
    {
        public Dictionary<PriceItemKey, LatestItemPriceDto> Data { get; } = [];
        public int Calls;
        protected override object? Invoke(MethodInfo? method, object?[]? args) { Calls++; return Task.FromResult(Data); }
    }
    public class Sender : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(OperationResult<GetMfgProductionOrder>.Ok(new GetMfgProductionOrder()));
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => Fixture.Now; }
}
