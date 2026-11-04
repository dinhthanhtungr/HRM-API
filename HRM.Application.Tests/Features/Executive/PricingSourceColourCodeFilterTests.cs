using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Executive;

public sealed class PricingSourceColourCodeFilterTests
{
    [Theory]
    [InlineData("TP81050", "TP81050", true, ItemType.Product, true)]
    [InlineData(" TP81050 ", "tp81050", true, ItemType.ProductFailure, true)]
    [InlineData("TP81050", "TP81050", false, ItemType.Product, false)]
    [InlineData("TP81050", "OTHER", true, ItemType.Product, false)]
    [InlineData("TP81050", "TP81050", true, ItemType.Material, false)]
    [InlineData(null, null, true, ItemType.Product, false)]
    [InlineData(" ", " ", true, ItemType.Product, false)]
    [InlineData("TP81050", null, true, ItemType.Product, false)]
    public void OnlyActiveProductComponentsWithMatchingCurrentCodeAreExcluded(
        string? targetCode, string? ingredientCode, bool active, ItemType type, bool excluded)
    {
        var companyId = Guid.NewGuid();
        var target = new Product { ProductId = Guid.NewGuid(), CompanyId = companyId, ColourCode = targetCode! };
        var ingredient = new Product { ProductId = Guid.NewGuid(), CompanyId = companyId, ColourCode = ingredientCode! };
        var material = new ManufacturingFormulaMaterial
        {
            ManufacturingFormulaId = Guid.NewGuid(), IsActive = active, itemType = type,
            ProductId = ingredient.ProductId, Product = ingredient, Quantity = 0,
            MaterialExternalIdSnapshot = "UNRELATED",
            ManufacturingFormula = new ManufacturingFormula { CompanyId = companyId }
        };
        var result = PricingSourceColourCodeFilter.SelectExcludedSources(
            new[] { material }.AsQueryable(), new[] { target }.AsQueryable(), companyId).ToArray();
        Assert.Equal(excluded ? 1 : 0, result.Length);
        ingredient.CompanyId = Guid.NewGuid();
        Assert.Empty(PricingSourceColourCodeFilter.SelectExcludedSources(
            new[] { material }.AsQueryable(), new[] { target }.AsQueryable(), companyId));
    }

    [Fact]
    public void BothCorrelatedQueriesTranslateToPostgresBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only").Options);
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var exclusions = PricingSourceColourCodeFilter.SelectExcludedSources(
            db.ManufacturingFormulaMaterials, db.Products.Where(x => x.ProductId == productId), companyId);
        var sql = db.ManufacturingFormulas.Where(x => x.CompanyId == companyId &&
            !exclusions.Any(e => e.ManufacturingFormulaId == x.ManufacturingFormulaId))
            .OrderBy(x => x.CreatedDate).Skip(5).Take(5).ToQueryString();
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("upper(", sql);
        Assert.Contains("btrim(", sql);
        Assert.Contains("is_active", sql);
        Assert.Contains("LIMIT", sql);
        var suggestionSql = db.ProductionSelectVersions.Where(x => !exclusions.Any(e =>
            e.ProductId == x.MfgProductionOrder.ProductId &&
            e.ManufacturingFormulaId == x.ManufacturingFormulaId)).ToQueryString();
        Assert.Contains("NOT EXISTS", suggestionSql);
    }
}
