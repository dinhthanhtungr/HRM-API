using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.Executive;

public sealed class ProductPricingReviewMaterialComparisonRulesTests
{
    [Fact]
    public void FormulaStructureDifference_IgnoresPriceButDetectsQuantityChanges()
    {
        var itemId = Guid.NewGuid();
        var standard = Source(10m, true, ComparisonMaterial(itemId, "SAME", 1m, 10m));
        var sameStructureWithNewPrice = Source(
            20m, true, ComparisonMaterial(itemId, "SAME", 1m, 20m));
        var changedQuantity = Source(
            20m, true, ComparisonMaterial(itemId, "SAME", 2m, 10m));

        Assert.False(ProductPricingReviewMaterialComparisonRules.HasFormulaStructureDifference(
            standard,
            sameStructureWithNewPrice));
        Assert.True(ProductPricingReviewMaterialComparisonRules.HasFormulaStructureDifference(
            standard,
            changedQuantity));
    }

    [Theory]
    [InlineData(120, 100, PricingReviewCostComparisonStatus.Increased)]
    [InlineData(80, 100, PricingReviewCostComparisonStatus.Decreased)]
    [InlineData(100, 100, PricingReviewCostComparisonStatus.Unchanged)]
    public void SourcePricing_ComparesCompleteRealtimeCostAgainstCurrentStandardCost(
        decimal calculated,
        decimal baseline,
        PricingReviewCostComparisonStatus expectedStatus)
    {
        var source = Source(
            calculated,
            isComplete: true,
            Material("A", 1m, 90m, calculated));

        var result = ProductPricingReviewMaterialComparisonRules.BuildSourcePricing(
            source,
            baseline,
            new DateTime(2026, 9, 11));

        Assert.True(result.CanCompare);
        Assert.Equal(calculated - baseline, result.DifferenceAmount);
        Assert.Equal(expectedStatus, result.ComparisonStatus);
        Assert.True(result.HasSourcePriceSnapshot);
        Assert.Equal(calculated, result.CalculatedMaterialCost);
    }

    [Fact]
    public void SourcePricing_ReturnsUnavailableWhenAnyCurrentPriceIsMissing()
    {
        var missing = Material("MISSING", 1m, 10m, null);
        var source = Source(10m, isComplete: false, missing);

        var result = ProductPricingReviewMaterialComparisonRules.BuildSourcePricing(
            source,
            8m,
            new DateTime(2026, 9, 11));

        Assert.False(result.CanCompare);
        Assert.Null(result.CalculatedMaterialCost);
        Assert.Null(result.DifferenceAmount);
        Assert.Equal(PricingReviewCostComparisonStatus.Unavailable, result.ComparisonStatus);
        Assert.Equal(1, result.MissingPriceCount);
    }

    [Fact]
    public void SourcePricing_DistinguishesZeroBaselineFromMissingBaseline()
    {
        var source = Source(0m, isComplete: true, Material("FREE", 1m, 0m, 0m));

        var zeroBaseline = ProductPricingReviewMaterialComparisonRules.BuildSourcePricing(
            source, 0m, new DateTime(2026, 9, 11));
        var missingBaseline = ProductPricingReviewMaterialComparisonRules.BuildSourcePricing(
            source, null, new DateTime(2026, 9, 11));

        Assert.True(zeroBaseline.CanCompare);
        Assert.Equal(PricingReviewCostComparisonStatus.Unchanged, zeroBaseline.ComparisonStatus);
        Assert.Null(zeroBaseline.DifferencePercent);
        Assert.False(missingBaseline.CanCompare);
        Assert.Equal(PricingReviewCostComparisonStatus.Unavailable, missingBaseline.ComparisonStatus);
    }

    [Fact]
    public void FormulaComparison_UsesOneCurrentPriceAndComparesFormulaQuantities()
    {
        var itemId = Guid.NewGuid();
        var standard = Source(10m, true, ComparisonMaterial(itemId, "SAME", 1m, 10m));
        var viewed = Source(20m, true, ComparisonMaterial(itemId, "SAME", 2m, 10m));

        var item = ProductPricingReviewMaterialComparisonRules
            .BuildFormulaComparisonItems(standard, viewed)
            .Single();

        Assert.Equal(10m, item.CurrentPrice.UnitPrice);
        Assert.Equal(1m, item.StandardFormula.Quantity);
        Assert.Equal(10m, item.StandardFormula.Amount);
        Assert.Equal(2m, item.ViewedFormula.Quantity);
        Assert.Equal(20m, item.ViewedFormula.Amount);
        Assert.Equal(10m, item.AmountDifference);
        Assert.Equal(PricingReviewFormulaMaterialComparisonStatus.QuantityChanged, item.Status);
    }

    [Fact]
    public void FormulaComparison_ReturnsFormulaCategoryMetadataForFrontendGrouping()
    {
        var itemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var standard = Source(10m, true, ComparisonMaterial(itemId, "PIGMENT", 1m, 10m, categoryId: categoryId));
        var viewed = Source(20m, true, ComparisonMaterial(itemId, "PIGMENT", 2m, 10m, categoryId: categoryId));
        IReadOnlyDictionary<Guid, PricingReviewFormulaItemCategory> categories =
            new Dictionary<Guid, PricingReviewFormulaItemCategory>
            {
                [categoryId] = new()
                {
                    CategoryId = categoryId,
                    CategoryCode = "BM",
                    CategoryName = "Bột màu"
                }
            };

        var item = ProductPricingReviewMaterialComparisonRules
            .BuildFormulaComparisonItems(standard, viewed, categories)
            .Single();

        Assert.Equal(categoryId, item.CategoryId);
        Assert.Equal("BM", item.CategoryCode);
        Assert.Equal("Bột màu", item.CategoryName);
        Assert.Equal(PricingReviewMaterialCategoryGroup.Pigment, item.CategoryGroup);
        Assert.Equal("Bột màu", item.CategoryGroupName);
    }

    [Theory]
    [InlineData(ItemType.MaterialFailure, ItemType.Material, true)]
    [InlineData(ItemType.ProductFailure, ItemType.Product, false)]
    public void ItemIdentity_NormalizesFailureTypesAndSeparatesMaterialFromProduct(
        ItemType sourceType,
        ItemType expectedType,
        bool isMaterial)
    {
        var itemId = Guid.NewGuid();

        var identity = ProductPricingReviewItemIdentity.Resolve(sourceType, itemId);

        Assert.Equal(expectedType, identity.ItemType);
        Assert.Equal(isMaterial ? itemId : null, identity.MaterialId);
        Assert.Equal(isMaterial ? null : itemId, identity.ProductId);
    }

    [Fact]
    public void FormulaComparison_ReturnsProductFailureAsProductIdentity()
    {
        var productId = Guid.NewGuid();
        var product = ComparisonMaterial(
            productId,
            "TP_13912",
            0.173m,
            89_875m,
            itemType: ItemType.ProductFailure);
        var viewed = Source(15_548m, true, product);

        var item = ProductPricingReviewMaterialComparisonRules
            .BuildFormulaComparisonItems(null, viewed)
            .Single();

        Assert.Equal(ItemType.Product, item.ItemType);
        Assert.Null(item.MaterialId);
        Assert.Equal(productId, item.ProductId);
    }

    [Fact]
    public void FormulaComparison_ReportsAddedAndRemovedMaterials()
    {
        var removedId = Guid.NewGuid();
        var addedId = Guid.NewGuid();
        var standard = Source(10m, true, ComparisonMaterial(removedId, "REMOVED", 1m, 10m));
        var viewed = Source(20m, true, ComparisonMaterial(addedId, "ADDED", 2m, 10m));

        var items = ProductPricingReviewMaterialComparisonRules
            .BuildFormulaComparisonItems(standard, viewed);

        Assert.Contains(items, x =>
            x.MaterialCode == "ADDED" &&
            x.Status == PricingReviewFormulaMaterialComparisonStatus.AddedToViewedFormula &&
            x.AmountDifference == 20m);
        Assert.Contains(items, x =>
            x.MaterialCode == "REMOVED" &&
            x.Status == PricingReviewFormulaMaterialComparisonStatus.RemovedFromViewedFormula &&
            x.AmountDifference == -10m);
    }

    [Fact]
    public void FormulaComparison_IsUnavailableWithoutStandardFormula()
    {
        var viewed = Source(
            15m,
            true,
            ComparisonMaterial(Guid.NewGuid(), "VIEWED", 1m, 15m));
        var items = ProductPricingReviewMaterialComparisonRules
            .BuildFormulaComparisonItems(null, viewed);

        var summary = ProductPricingReviewMaterialComparisonRules.BuildFormulaComparisonSummary(
            null,
            viewed,
            items,
            new DateTime(2026, 9, 12),
            PricingReviewFormulaComparisonUnavailableReason.NoStandardFormula);

        Assert.False(summary.CanCompare);
        Assert.Equal(
            PricingReviewFormulaComparisonUnavailableReason.NoStandardFormula,
            summary.UnavailableReason);
        Assert.Null(summary.StandardFormulaMaterialCost);
        Assert.Equal(15m, summary.ViewedFormulaMaterialCost);
        Assert.All(items, x => Assert.Equal(
            PricingReviewFormulaMaterialComparisonStatus.Unavailable,
            x.Status));
    }

    [Fact]
    public void FormulaComparison_PrioritizesMissingPriceThenLargestAmountDifference()
    {
        var missingId = Guid.NewGuid();
        var highId = Guid.NewGuid();
        var lowId = Guid.NewGuid();
        var standard = Source(
            20m,
            false,
            ComparisonMaterial(missingId, "MISSING", 1m, null),
            ComparisonMaterial(highId, "HIGH", 1m, 10m),
            ComparisonMaterial(lowId, "LOW", 1m, 10m));
        var viewed = Source(
            112m,
            false,
            ComparisonMaterial(missingId, "MISSING", 2m, null),
            ComparisonMaterial(highId, "HIGH", 10m, 10m),
            ComparisonMaterial(lowId, "LOW", 1.2m, 10m));

        var all = ProductPricingReviewMaterialComparisonRules.BuildFormulaComparisonItems(
            standard,
            viewed);
        var result = ProductPricingReviewMaterialComparisonRules.SortFormulaComparisonItems(
            all,
            limit: 2,
            descending: true);

        Assert.Equal(["MISSING", "HIGH"], result.Select(x => x.MaterialCode));
        Assert.Equal(
            PricingReviewFormulaMaterialComparisonStatus.MissingCurrentPrice,
            result[0].Status);
    }

    private static ProductPricingSourceOptionDto Source(
        decimal currentMaterialCost,
        bool isComplete,
        params QuotationProductPricingMaterialDto[] materials)
        => new()
        {
            CurrentMaterialCost = currentMaterialCost,
            IsCurrentMaterialCostComplete = isComplete,
            Materials = materials
        };

    private static QuotationProductPricingMaterialDto Material(
        string code,
        decimal quantity,
        decimal? sourceAmount,
        decimal? currentAmount,
        bool hasSourceSnapshot = true)
        => new()
        {
            FormulaMaterialId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ItemType = ItemType.Material,
            ItemCode = code,
            ItemName = code,
            Quantity = quantity,
            Unit = "Kg",
            HasSourcePriceSnapshot = hasSourceSnapshot,
            SourceUnitPrice = sourceAmount / quantity,
            SourceTotalPrice = sourceAmount,
            HasLatestPrice = currentAmount.HasValue,
            LatestUnitPrice = currentAmount / quantity,
            LatestTotalPrice = currentAmount,
            LatestPriceDate = new DateTime(2026, 9, 10),
            LatestPriceSource = LatestPriceSourceType.MaterialSupplier
        };

    private static QuotationProductPricingMaterialDto ComparisonMaterial(
        Guid itemId,
        string code,
        decimal quantity,
        decimal? currentUnitPrice,
        string unit = "Kg",
        Guid? categoryId = null,
        ItemType itemType = ItemType.Material)
        => new()
        {
            FormulaMaterialId = Guid.NewGuid(),
            ItemId = itemId,
            ItemType = itemType,
            ItemCode = code,
            ItemName = code,
            CategoryId = categoryId,
            Quantity = quantity,
            Unit = unit,
            HasLatestPrice = currentUnitPrice.HasValue,
            LatestUnitPrice = currentUnitPrice,
            LatestTotalPrice = currentUnitPrice.HasValue
                ? quantity * currentUnitPrice.Value
                : null,
            LatestPriceDate = currentUnitPrice.HasValue
                ? new DateTime(2026, 9, 10)
                : null,
            LatestPriceSource = currentUnitPrice.HasValue
                ? LatestPriceSourceType.MaterialSupplier
                : LatestPriceSourceType.Unknown
        };
}
