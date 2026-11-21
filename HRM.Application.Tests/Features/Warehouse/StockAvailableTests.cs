using System.Globalization;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockAvailableTests
{
    [Fact]
    public async Task InternalExactCodes_FilterBeforeTotalsAndDoNotBindFromHttp()
    {
        var f = new Fixture();
        var other = f.Stock(2, StockType.RawMaterial, "B", 99);
        other.Code = "OTHER";
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, StockType.RawMaterial, "A", 10), other);
        var page = (await f.Handler.Handle(new() { ExactCodes = ["ITEM"] }, default)).Data!;
        Assert.Equal("ITEM", Assert.Single(page.Items).Code);
        Assert.Equal(10m, page.Items[0].TotalOnHandKg);
        Assert.Equal(2, (await f.Handler.Handle(new(), default)).Data!.TotalCount);
        Assert.Empty((await f.Handler.Handle(new() { ExactCodes = [] }, default)).Data!.Items);
        var (bound, valid) = await Bind("?exactCodes=ITEM");
        Assert.True(valid);
        Assert.Null(bound.ExactCodes);
    }

    [Theory]
    [InlineData(ApplicationRoles.Production.PLPUUser, true)]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.President, true)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    [InlineData(ApplicationRoles.Warehouse.KHOUser, false)]
    public async Task MixingShelf_VisibilityFollowsCapabilityBeforeTotalsAndPaging(string role, bool allowed)
    {
        var f = new Fixture([role]);
        var bySlot = f.Stock(3, StockType.RawMaterial, "LEGACY", 80);
        bySlot.WarehouseShelves!.SlotCode = "CT.0.1";
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks),
            f.Stock(1, StockType.RawMaterial, "A", 10),
            f.Stock(2, StockType.RawMaterial, "CT.0.1", 90), bySlot,
            f.Stock(4, StockType.FinishedGood, "CT.0.1", 70),
            f.Stock(5, StockType.RawMaterial, "CT.0.1", 20, active: false),
            f.Stock(6, StockType.RawMaterial, "CT.0.1", 1000, company: Guid.NewGuid()));
        var page = (await f.Handler.Handle(new() { PageSize = 20 }, default)).Data!;
        Assert.Equal(allowed ? 2 : 1, page.TotalCount);
        var raw = Assert.Single(page.Items, x => x.StockType == StockType.RawMaterial);
        Assert.Equal(allowed ? 180m : 10m, raw.TotalOnHandKg);
        Assert.Equal(allowed ? 200m : 10m, raw.PhysicalOnHandKg);
        Assert.Equal(allowed ? 20m : 0m, raw.InactiveShelfOnHandKg);
        Assert.Equal(raw.TotalOnHandKg, raw.AvailableKg);
        Assert.Equal(allowed ? 4 : 1, raw.StockDetailAvailables.Count);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task MixingShelf_ExplicitClaimsAndAuthenticationAreRespected(bool authenticated, bool explicitSet, bool grant)
    {
        var user = new MixingUser { IsAuthenticated = authenticated, HasExplicitPermissionSet = explicitSet,
            Roles = [ApplicationRoles.Production.PLPUUser],
            Permissions = grant ? [ApplicationPermissions.Warehouse.ViewMixingStock] : [] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.False(permissions.HasPermission(ApplicationPermissions.Warehouse.ViewMixingStock));
        var f = new Fixture([ApplicationRoles.Production.PLPUUser], explicitSet: true,
            permissions: grant ? [ApplicationPermissions.Warehouse.ViewMixingStock] : []);
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, StockType.RawMaterial, "CT.0.1", 7));
        Assert.Equal(grant ? 1 : 0, (await f.Handler.Handle(new(), default)).Data!.TotalCount);
    }

    [Fact]
    public async Task MixingShelf_IsExcludedFromDetailsTotalsAndPaging()
    {
        var f = new Fixture();
        var mixingByCode = f.Stock(2, StockType.RawMaterial, "CT.0.1", 90);
        var mixingBySlot = f.Stock(3, StockType.RawMaterial, "LEGACY", 80);
        mixingBySlot.WarehouseShelves!.SlotCode = "CT.0.1";
        var mixingOnly = f.Stock(4, StockType.FinishedGood, "CT.0.1", 70);
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks),
            f.Stock(1, StockType.RawMaterial, "A", 10), mixingByCode, mixingBySlot, mixingOnly);
        var page = (await f.Handler.Handle(new() { PageSize = 20 }, default)).Data!;
        Assert.Equal(1, page.TotalCount);
        var row = Assert.Single(page.Items);
        Assert.Equal(10m, row.TotalOnHandKg);
        Assert.Equal(10m, row.PhysicalOnHandKg);
        Assert.Equal(10m, row.AvailableKg);
        Assert.Equal("A", Assert.Single(row.StockDetailAvailables).ShelfStockCode);
    }

    [Theory]
    [InlineData("?stockTypes=3", new[] { 3 })]
    [InlineData("?stockTypes=3&stockTypes=4", new[] { 3, 4 })]
    [InlineData("?stockTypes=1&stockTypes=2", new[] { 1, 2 })]
    [InlineData("?stockTypes=5", new[] { 5 })]
    [InlineData("", new int[0])]
    public async Task QueryBinding_PreservesSingleAndRepeatedValues(string query, int[] expected)
    {
        var (model, valid) = await Bind(query);
        Assert.True(valid);
        Assert.Equal(expected, model.StockTypes.Select(x => (int)x));
    }

    [Theory]
    [InlineData("?stockTypes=99")]
    [InlineData("?stockTypes=invalid")]
    public async Task QueryBinding_RejectsInvalidEnum(string query)
        => Assert.False((await Bind(query)).Valid);

    [Theory]
    [InlineData(3, 4)]
    [InlineData(1, 2)]
    public async Task CombinedPages_KeepTypesAndLocationsSeparate(int pass, int fail)
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks),
            f.Stock(1, (StockType)fail, "B", 7), f.Stock(2, (StockType)pass, "B", 3),
            f.Stock(3, (StockType)pass, "A", 5), f.Stock(4, (StockType)pass, "A", 2),
            f.Stock(5, StockType.Other, "A", 100),
            f.Stock(6, (StockType)pass, "A", 100, active: false),
            f.Stock(7, (StockType)pass, "A", 100, company: Guid.NewGuid()));
        var first = await f.Handler.Handle(new() { StockTypes = [(StockType)fail, (StockType)pass, (StockType)pass], PageSize = 1 }, default);
        var second = await f.Handler.Handle(new() { StockTypes = [(StockType)pass, (StockType)fail], PageSize = 1, PageNumber = 2 }, default);
        Assert.True(first.Success);
        Assert.Equal(2, first.Data!.TotalCount);
        Assert.True(first.Data.HasNextPage);
        Assert.Equal(2, second.Data!.TotalCount);
        Assert.False(second.Data.HasNextPage);
        var good = Assert.Single(first.Data.Items);
        var bad = Assert.Single(second.Data.Items);
        Assert.Equal((StockType)pass, good.StockType);
        Assert.Equal((StockType)fail, bad.StockType);
        Assert.Equal(10m, good.TotalOnHandKg);
        Assert.Equal(110m, good.PhysicalOnHandKg);
        Assert.Equal(100m, good.InactiveShelfOnHandKg);
        Assert.Equal(10m, good.AvailableKg);
        Assert.Equal(new[] { "A", "B" }, good.StockDetailAvailables.Where(x => x.ShelfIsActive).Select(x => x.ShelfStockCode));
        Assert.Equal(new[] { 7m, 3m }, good.StockDetailAvailables.Where(x => x.ShelfIsActive).Select(x => x.OnHandKg));
        var disabled = Assert.Single(good.StockDetailAvailables, x => !x.ShelfIsActive);
        Assert.Equal(100m, disabled.OnHandKg);
        Assert.False(disabled.CanUse);
        Assert.Equal("ShelfInactive", disabled.UnavailableReason);
        Assert.All(good.StockDetailAvailables, x => Assert.Equal("SAME-LOT", x.LotNo));
        Assert.Equal(7m, Assert.Single(bad.StockDetailAvailables).OnHandKg);
        Assert.Equal(0m, bad.AvailableKg);
        Assert.Empty((await f.Handler.Handle(new() { StockTypes = [(StockType)pass, (StockType)fail], PageSize = 1, PageNumber = 3 }, default)).Data!.Items);
    }

    [Fact]
    public async Task Reservations_SubtractOnlyFromActiveTotal_WithoutChangingPhysicalStock()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks),
            f.Stock(1, StockType.RawMaterial, "ON", 70),
            f.Stock(2, StockType.RawMaterial, "OFF", 30, active: false));
        f.Store.Seed(nameof(f.Db.WarehouseTempStocks),
            new WarehouseTempStock { CompanyId = f.Company, Code = "ITEM", VaCode = "MFG261000162", ReserveStatus = "Open", QtyRequest = 25, QtyUsed = 5 },
            new WarehouseTempStock { CompanyId = Guid.NewGuid(), Code = "ITEM", ReserveStatus = "Open", QtyRequest = 100 });
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion
        {
            CompanyId = f.Company,
            MfgProductionOrder = new() { CompanyId = f.Company, IsActive = true, ExternalId = "MFG261000162" },
            ManufacturingFormula = new() { CompanyId = f.Company, IsActive = true, ExternalId = "VA261000001" }
        });

        var item = Assert.Single((await f.Handler.Handle(new(), default)).Data!.Items);
        Assert.Equal(100m, item.PhysicalOnHandKg);
        Assert.Equal(30m, item.InactiveShelfOnHandKg);
        Assert.Equal(70m, item.TotalOnHandKg);
        Assert.Equal(20m, item.ReservedOpenAllKg);
        Assert.Equal(50m, item.AvailableKg);
        var reservation = Assert.Single(item.ReservedVaCodes);
        Assert.Equal("VA261000001", reservation.VaCode);
        Assert.Equal(20m, reservation.ReservedKg);
        var active = Assert.Single(item.StockDetailAvailables, x => x.ShelfIsActive);
        Assert.True(active.CanUse);
        Assert.Null(active.UnavailableReason);
    }

    [Theory]
    [InlineData("selection-company")]
    [InlineData("mfg-company")]
    [InlineData("formula-company")]
    [InlineData("mfg-inactive")]
    [InlineData("formula-inactive")]
    [InlineData("closed")]
    [InlineData("no-formula")]
    [InlineData("unrequested")]
    public async Task ReservationFormula_RejectsInvalidOrForeignLinks(string scenario)
    {
        var f = new Fixture();
        var selection = Selection(f.Company, "MFG-1", "VA-1", DateTime.Today);
        switch (scenario)
        {
            case "selection-company": selection.CompanyId = Guid.NewGuid(); break;
            case "mfg-company": selection.MfgProductionOrder.CompanyId = Guid.NewGuid(); break;
            case "formula-company": selection.ManufacturingFormula!.CompanyId = Guid.NewGuid(); break;
            case "mfg-inactive": selection.MfgProductionOrder.IsActive = false; break;
            case "formula-inactive": selection.ManufacturingFormula!.IsActive = false; break;
            case "closed": selection.ValidTo = DateTime.Today; break;
            case "no-formula": selection.ManufacturingFormula = null; break;
            case "unrequested": selection.MfgProductionOrder.ExternalId = "MFG-OTHER"; break;
        }
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), selection);
        Assert.Empty(await ReservedManufacturingFormulaReader.ReadAsync(f.Db, f.Company, ["MFG-1"], default));
    }

    [Fact]
    public async Task ReservationWithoutFormula_ReturnsEmptyVaCodeAndKeepsQuantity()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, StockType.RawMaterial, "ON", 70));
        f.Store.Seed(nameof(f.Db.WarehouseTempStocks), new WarehouseTempStock
        {
            CompanyId = f.Company, Code = "ITEM", VaCode = "MFG-MISSING",
            ReserveStatus = "Open", QtyRequest = 20, CreatedDate = new DateTime(2026, 10, 17)
        });
        var item = Assert.Single((await f.Handler.Handle(new(), default)).Data!.Items);
        var reservation = Assert.Single(item.ReservedVaCodes);
        Assert.Equal(string.Empty, reservation.VaCode);
        Assert.Equal(20m, reservation.ReservedKg);
        Assert.Equal(new DateTime(2026, 10, 17), reservation.CreatedDate);
        Assert.Equal(50m, item.AvailableKg);
    }

    [Fact]
    public async Task ReservationFormula_UsesLatestOpenSelection_AndRetainsMissingReservation()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions),
            Selection(f.Company, "MFG-1", "VA-OLD", null),
            Selection(f.Company, "MFG-1", "VA-CURRENT", DateTime.Today));
        var result = await ReservedManufacturingFormulaReader.ReadAsync(f.Db, f.Company, ["MFG-1", "MFG-MISSING"], default);
        Assert.Equal("VA-CURRENT", result["MFG-1"]);
        Assert.False(result.ContainsKey("MFG-MISSING"));
    }

    private static ProductionSelectVersion Selection(Guid company, string mfg, string formula, DateTime? validFrom)
        => new()
        {
            CompanyId = company, ValidFrom = validFrom, ProductionSelectVersionId = Guid.NewGuid(),
            MfgProductionOrder = new() { CompanyId = company, IsActive = true, ExternalId = mfg },
            ManufacturingFormula = new() { CompanyId = company, IsActive = true, ExternalId = formula }
        };

    [Fact]
    public async Task DisabledOnlyStock_RemainsVisible_ButCannotSupplyOperationalReaders()
    {
        var f = new Fixture();
        var disabled = f.Stock(1, StockType.RawMaterial, "OFF", 30, active: false);
        var foreignShelf = f.Stock(2, StockType.RawMaterial, "FOREIGN", 100);
        foreignShelf.WarehouseShelves!.CompanyId = Guid.NewGuid();
        var orphan = f.Stock(3, StockType.RawMaterial, "ORPHAN", 100);
        orphan.WarehouseShelves = null!;
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), disabled, foreignShelf, orphan);

        var result = await f.Handler.Handle(new() { OnlyAvailableLeZero = true }, default);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(30m, item.PhysicalOnHandKg);
        Assert.Equal(30m, item.InactiveShelfOnHandKg);
        Assert.Equal(0m, item.TotalOnHandKg);
        Assert.Equal(0m, item.AvailableKg);
        Assert.False(Assert.Single(item.StockDetailAvailables).CanUse);
        Assert.Empty(WarehouseStockQueryHelper.ActiveShelfStocks(new[] { disabled }.AsQueryable(), f.Company));
        Assert.Empty(WarehouseStockQueryHelper.ForItemStock(new[] { disabled }.AsQueryable(), f.Company, "ITEM", StockType.RawMaterial));
    }

    [Fact]
    public async Task FiltersAndCount_ApplyBeforePaging_AndOmissionStillReturnsAllTypes()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, StockType.RawMaterial, "A", 10),
            f.Stock(2, StockType.DefectiveRawMaterial, "A", 8), f.Stock(3, StockType.Other, "A", 4));
        Assert.Equal(3, (await f.Handler.Handle(new(), default)).Data!.TotalCount);
        Assert.Equal(StockType.Other, Assert.Single((await f.Handler.Handle(new() { StockTypes = [StockType.Other] }, default)).Data!.Items).StockType);
        var filtered = (await f.Handler.Handle(new() { StockTypes = [StockType.RawMaterial, StockType.DefectiveRawMaterial], AvailableMax = 0, PageSize = 1 }, default)).Data!;
        Assert.Equal(1, filtered.TotalCount);
        Assert.False(filtered.HasNextPage);
        Assert.Equal(StockType.DefectiveRawMaterial, Assert.Single(filtered.Items).StockType);
        Assert.False((await f.Handler.Handle(new() { StockTypes = [(StockType)99] }, default)).Success);
    }

    private static async Task<(GetStockAvailableQuery Model, bool Valid)> Bind(string query)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.QueryString = new QueryString(query);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = provider.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(GetStockAvailableQuery));
        var binding = new BindingInfo { BindingSource = BindingSource.Query };
        var binder = provider.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext { Metadata = metadata, BindingInfo = binding });
        var context = DefaultModelBindingContext.CreateBindingContext(action,
            new QueryStringValueProvider(BindingSource.Query, http.Request.Query, CultureInfo.InvariantCulture), metadata, binding, "");
        await binder.BindModelAsync(context);
        return ((GetStockAvailableQuery)context.Result.Model!, action.ModelState.ErrorCount == 0);
    }

    private sealed class Fixture
    {
        public Guid Company { get; } = Guid.NewGuid();
        public IWarehouseReadDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public GetStockAvailableQueryHandler Handler { get; }
        public Fixture(string[]? roles = null, bool explicitSet = false, string[]? permissions = null)
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
            var user = new MixingUser { CompanyId = Company, Roles = roles ?? [],
                HasExplicitPermissionSet = explicitSet, Permissions = permissions ?? [] };
            Handler = new(Db, user, new Visibility(), new CurrentUserPermissionService(user));
        }
        public WarehouseShelfStock Stock(int id, StockType type, string shelf, decimal kg, bool active = true, Guid? company = null)
            => new() { ShelfStockId = id, Code = "ITEM", LotNo = "SAME-LOT", StockType = type, ShelfStockCode = shelf,
                QtyKg = kg, CompanyId = company ?? Company, WarehouseShelves = new() { IsActive = active, CompanyId = company ?? Company } };
    }

    private sealed class MixingUser : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; }
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Visibility : IWarehouseStockVisibilityService
    {
        public Task<IQueryable<WarehouseShelfStock>> ApplyAsync(IQueryable<WarehouseShelfStock> query, Guid companyId, string? keyword, CancellationToken cancellationToken = default) => Task.FromResult(query);
        public Task<IQueryable<WarehouseVoucherDetail>> ApplyVoucherDetailsAsync(IQueryable<WarehouseVoucherDetail> query, Guid companyId, string? keyword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
