using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class ReservedManufacturingLinkTests
{
    [Fact]
    public async Task SameFormulaInDifferentOrdersKeepsExactMfgAndCompanyScope()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var company = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        store.Seed(nameof(db.ProductionSelectVersions),
            Selection(company, first, "MFG-1", "VA-OLD", DateTime.Today.AddDays(-1)),
            Selection(company, first, "MFG-1", "VA-SHARED", DateTime.Today),
            Selection(company, second, "MFG-2", "VA-SHARED", DateTime.Today),
            Selection(Guid.NewGuid(), Guid.NewGuid(), "MFG-1", "FOREIGN", DateTime.Today.AddDays(1)));
        var result = await ReservedManufacturingFormulaReader.ReadLinksAsync(db, company, ["MFG-1", "MFG-2", "MFG-MISSING"], default);
        Assert.Equal(first, result["MFG-1"].MfgProductionOrderId);
        Assert.Equal(second, result["MFG-2"].MfgProductionOrderId);
        Assert.Equal("VA-SHARED", result["MFG-1"].FormulaCode);
        Assert.Equal("VA-SHARED", result["MFG-2"].FormulaCode);
        Assert.False(result.ContainsKey("MFG-MISSING"));
        Assert.Equal(0, store.Saves);
    }

    private static ProductionSelectVersion Selection(Guid company, Guid mfgId, string mfg, string va, DateTime date) => new()
    {
        CompanyId = company, MfgProductionOrderId = mfgId, ProductionSelectVersionId = Guid.NewGuid(), ValidFrom = date,
        MfgProductionOrder = new() { CompanyId = company, IsActive = true, ExternalId = mfg },
        ManufacturingFormula = new() { CompanyId = company, IsActive = true, ExternalId = va }
    };
}
