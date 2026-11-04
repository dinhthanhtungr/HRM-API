using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaVersions;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaVersionReadTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Versions_ReturnHistoricalSnapshotsAndMaskPriceByExplicitPermission(bool priceAllowed)
    {
        var f = new Fixture(priceAllowed);
        var result = await f.Handler.Handle(new(f.Formula.ManufacturingFormulaId), default);
        Assert.True(result.Success, result.Message);
        var version = Assert.Single(result.Data!.Items);
        var row = Assert.Single(version.Items);
        Assert.Equal("historical name", row.Name);
        Assert.Equal("historical code", row.Code);
        Assert.Equal(priceAllowed ? 12m : null, row.UnitPrice);
        Assert.Equal(priceAllowed ? 6m : null, row.TotalPrice);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("visibility")]
    [InlineData("page")]
    public async Task Versions_RejectForeignParentMissingCapabilityOrInvalidPage(string reason)
    {
        var f = new Fixture(false);
        if (reason == "foreign") f.Formula.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Formula.IsActive = false;
        if (reason == "visibility") f.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders];
        Assert.False((await f.Handler.Handle(new(f.Formula.ManufacturingFormulaId, reason == "page" ? int.MaxValue : 1), default)).Success);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new() { HasExplicitPermissionSet = true };
        public ManufacturingFormula Formula { get; }
        public GetManufacturingFormulaVersionsQueryHandler Handler { get; }
        public Fixture(bool priceAllowed)
        {
            User.Permissions = priceAllowed ? [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials,
                ApplicationPermissions.PLM.ViewFormulaPrices] : [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
            var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            Formula = new() { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true };
            var version = new ManufacturingFormulaVersion { ManufacturingFormulaVersionId = Guid.NewGuid(), ManufacturingFormulaId = Formula.ManufacturingFormulaId,
                ManufacturingFormula = Formula, VersionNo = 1 };
            store.Seed(nameof(db.ManufacturingFormulas), Formula);
            store.Seed(nameof(db.ManufacturingFormulaVersions), version);
            store.Seed(nameof(db.ManufacturingFormulaVersionItems), new ManufacturingFormulaVersionItem {
                ManufacturingFormulaVersionItemId = Guid.NewGuid(), ManufacturingFormulaVersionId = version.ManufacturingFormulaVersionId,
                Version = version, MaterialId = Guid.NewGuid(), itemType = ItemType.Material, Quantity = .5m, UnitPrice = 12m, TotalPrice = 6m,
                MaterialNameSnapshot = "historical name", MaterialExternalIdSnapshot = "historical code" });
            var permissions = new CurrentUserPermissionService(User);
            Handler = new(db, User, permissions, new PLMFieldVisibilityService(permissions));
        }
    }
}
