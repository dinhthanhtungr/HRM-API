using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.DevAndQA.ProductInspections;
using HRM.Application.Features.DevAndQA.ProductInspections.Commands.CreateProductInspection;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class ProductInspectionOptionalStandardTests
{
    [Theory]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public async Task CreateScopesSourceAndAllowsMissingStandard(bool withStandard, bool foreign, bool succeeds, bool wrongProduct, bool wrongBatch)
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMWriteDbContext>();
        var user = new IqcWorkflowTests.User();
        var company = foreign ? Guid.NewGuid() : user.CompanyId!.Value;
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var formulaId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        store.Seed("Products", new Product { ProductId = productId, CompanyId = company, Name = "Resin", ColourCode = "BLACK" });
        store.Seed("MfgProductionOrders", new MfgProductionOrder { MfgProductionOrderId = orderId, CompanyId = company,
            ProductId = productId, ProductExternalIdSnapshot = "BLACK" });
        store.Seed("ManufacturingFormulas", new ManufacturingFormula { ManufacturingFormulaId = formulaId,
            CompanyId = company, IsActive = true, ExternalId = "VA123" });
        store.Seed("ProductionSelectVersions", new ProductionSelectVersion { CompanyId = company,
            ManufacturingFormulaId = formulaId, MfgProductionOrderId = orderId });
        if (withStandard)
            store.Seed("ProductStandards", new ProductStandard { Id = standardId, CompanyId = company, ProductId = wrongProduct ? Guid.NewGuid() : productId,
                Product = new Product { Name = "Resin", ColourCode = "BLACK" }, IsActive = true });
        var result = await new CreateProductInspectionCommandHandler(db, user, new Codes()).Handle(
            new(new ProductInspectionWriteRequest { MfgProductionOrderId = orderId, BatchId = wrongBatch ? "OTHER" : "123",
                ProductStandardId = withStandard ? standardId : null }), default);
        Assert.Equal(succeeds, result.Success);
        if (!succeeds) { Assert.Empty(store.Added); return; }
        var saved = Assert.IsType<ProductInspection>(Assert.Single(store.Added));
        Assert.Equal(user.CompanyId, saved.CompanyId);
        Assert.Equal(productId, saved.ProductId);
        Assert.Equal(orderId, saved.MfgProductionOrderId);
        Assert.Equal(withStandard ? standardId : (Guid?)null, saved.ProductStandardId);
        Assert.Equal("Resin", saved.ProductName);
    }

    [Fact]
    public void CompanyScopeDoesNotFallbackForExplicitForeignCompany()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        store.Seed("ProductStandards", new ProductStandard { Id = standardId, CompanyId = company });
        var owned = new ProductInspection { CompanyId = company };
        var legacy = new ProductInspection { ProductStandardId = standardId };
        var foreign = new ProductInspection { CompanyId = Guid.NewGuid(), ProductStandardId = standardId };
        var unknown = new ProductInspection();
        Assert.Equal(new[] { owned, legacy }, new[] { owned, legacy, foreign, unknown }.AsQueryable().ForCompany(db, company));
    }

    private sealed class Codes : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => Task.FromResult("KSP_TEST");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => Task.FromResult("KSP_TEST");
    }
}
