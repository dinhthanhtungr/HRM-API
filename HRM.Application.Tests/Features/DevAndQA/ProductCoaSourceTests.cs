using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductCoaSources;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.PrintectSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class ProductCoaSourceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyJoinRetainsClosedVersionsAndInactiveOrdersAndPrefersExactLookup(bool exact)
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var user = new IqcWorkflowTests.User();
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company,
            Name = "Current product", ColourCode = "RED", ExpiryType = "12 months" };
        var formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(),
            CompanyId = company, IsActive = true, ExternalId = "VA123" };
        var order = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(),
            CompanyId = company, ProductId = product.ProductId, IsActive = false,
            ProductNameSnapshot = "Old snapshot", ProductExternalIdSnapshot = "ORDER", BagType = "Bag", TotalQuantityRequest = 25 };
        store.Seed("Products", product);
        store.Seed("ManufacturingFormulas", formula);
        store.Seed("MfgProductionOrders", order);
        store.Seed("ProductionSelectVersions",
            new ProductionSelectVersion { CompanyId = company, ManufacturingFormulaId = formula.ManufacturingFormulaId,
                MfgProductionOrderId = order.MfgProductionOrderId, ValidTo = DateTime.Now },
            new ProductionSelectVersion { CompanyId = company, ManufacturingFormulaId = formula.ManufacturingFormulaId,
                MfgProductionOrderId = order.MfgProductionOrderId });
        store.Seed("ProductTests",
            new ProductTest { ExternalId = "123", ProductId = product.ProductId, ProductName = "Normalized", ProductExternalId = "NORMAL" },
            new ProductTest { ExternalId = exact ? "VA123" : "OTHER", ProductId = product.ProductId,
                ProductName = "Exact", ProductExternalId = "EXACT" });
        var originalDate = new DateTime(2026, 1, 1);
        store.Seed("HistoryPrintLabelForAlls",
            new HistoryPrintLabelForAll { ExternalId = exact ? "VA123" : "OTHER", CreatedAt = originalDate },
            new HistoryPrintLabelForAll { ExternalId = "123", CreatedAt = originalDate.AddDays(1) });

        var result = await new GetProductCoaSourcesQueryHandler(db, user).Handle(new(), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, row =>
        {
            Assert.Equal(exact ? "VA123" : "123", row.BatchId);
            Assert.Equal(exact ? "Exact" : "Normalized", row.ProductName);
            Assert.Equal(exact ? "EXACT" : "NORMAL", row.ProductExternalId);
            Assert.Equal(exact ? originalDate : originalDate.AddDays(1), Assert.Single(row.LabelPrintDates));
            Assert.Equal(25, row.TotalQuantityRequest);
        });
    }

    [Fact]
    public async Task CompanyScopeExcludesForeignSourcesAndDefaultsInvalidPagination()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var user = new IqcWorkflowTests.User();
        var other = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var formulaId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        store.Seed("Products", new Product { ProductId = productId, CompanyId = other });
        store.Seed("ManufacturingFormulas", new ManufacturingFormula { ManufacturingFormulaId = formulaId,
            CompanyId = other, IsActive = true });
        store.Seed("ProductionSelectVersions", new ProductionSelectVersion { CompanyId = other,
            ManufacturingFormulaId = formulaId, MfgProductionOrderId = orderId });
        store.Seed("MfgProductionOrders", new MfgProductionOrder { MfgProductionOrderId = orderId,
            CompanyId = other, ProductId = productId });

        var result = await new GetProductCoaSourcesQueryHandler(db, user)
            .Handle(new() { PageNumber = 0, PageSize = 0 }, default);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(20, result.Data.PageSize);
    }
}
