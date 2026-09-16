using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal static class ProductPricingReviewMaterialComparisonRules
{
    public const int DefaultPreviewLimit = 8;
    public const int MaximumPreviewLimit = 20;

    public static PricingReviewSourceMaterialPricingDto BuildSourcePricing(
        ProductPricingSourceOptionDto source,
        decimal? currentStandardMaterialCost,
        DateTime now)
    {
        var materials = source.Materials;
        var calculatedMaterialCost = source.IsCurrentMaterialCostComplete
            ? source.CurrentMaterialCost
            : null;
        var hasSourceSnapshot = materials.Count > 0 &&
            materials.All(x => x.HasSourcePriceSnapshot && x.SourceTotalPrice.HasValue);
        decimal? sourceSnapshotMaterialCost = hasSourceSnapshot
            ? PricingRoundingRules.RoundCalculatedPrice(materials.Sum(x => x.SourceTotalPrice!.Value))
            : null;
        decimal? differenceAmount = calculatedMaterialCost.HasValue && currentStandardMaterialCost.HasValue
            ? PricingRoundingRules.RoundCalculatedPrice(
                calculatedMaterialCost.Value - currentStandardMaterialCost.Value)
            : null;

        return new PricingReviewSourceMaterialPricingDto
        {
            MaterialCount = materials.Count,
            CalculatedMaterialCost = calculatedMaterialCost,
            BaselineMaterialCost = currentStandardMaterialCost,
            DifferenceAmount = differenceAmount,
            DifferencePercent = CalculatePercent(differenceAmount, currentStandardMaterialCost),
            ComparisonStatus = ToCostComparisonStatus(differenceAmount),
            MissingPriceCount = materials.Count(x => !x.HasLatestPrice || !x.LatestUnitPrice.HasValue),
            StalePriceCount = materials.Count(x =>
                x.HasLatestPrice &&
                x.LatestPriceDate.HasValue &&
                x.LatestPriceDate.Value < now.AddDays(-ProductPricingReviewRules.StalePriceDays)),
            HasSourcePriceSnapshot = hasSourceSnapshot,
            SourceSnapshotMaterialCost = sourceSnapshotMaterialCost,
            CanCompare = calculatedMaterialCost.HasValue && currentStandardMaterialCost.HasValue
        };
    }

    public static IReadOnlyList<PricingReviewFormulaMaterialComparisonDto> BuildFormulaComparisonItems(
        ProductPricingSourceOptionDto? standardSource,
        ProductPricingSourceOptionDto viewedSource,
        IReadOnlyDictionary<Guid, PricingReviewFormulaItemCategory>? categories = null)
    {
        categories ??= new Dictionary<Guid, PricingReviewFormulaItemCategory>();
        var standardByItem = AggregateMaterials(standardSource?.Materials ?? []);
        var viewedByItem = AggregateMaterials(viewedSource.Materials);
        var keys = standardByItem.Keys.Concat(viewedByItem.Keys).Distinct().ToArray();

        return keys.Select(key => MapFormulaComparisonItem(
                standardByItem.GetValueOrDefault(key),
                viewedByItem.GetValueOrDefault(key),
                standardSource is not null,
                categories))
            .ToArray();
    }

    public static IReadOnlyList<PricingReviewFormulaMaterialComparisonDto> SortFormulaComparisonItems(
        IEnumerable<PricingReviewFormulaMaterialComparisonDto> items,
        int limit,
        bool descending)
    {
        IOrderedEnumerable<PricingReviewFormulaMaterialComparisonDto> ordered;
        if (descending)
        {
            ordered = items
                .OrderBy(x => x.Status == PricingReviewFormulaMaterialComparisonStatus.MissingCurrentPrice ? 0 : 1)
                .ThenByDescending(x => Abs(x.AmountDifference))
                .ThenBy(x => x.MaterialCode, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            ordered = items
                .OrderBy(x => x.Status == PricingReviewFormulaMaterialComparisonStatus.MissingCurrentPrice ? 0 : 1)
                .ThenBy(x => Abs(x.AmountDifference))
                .ThenBy(x => x.MaterialCode, StringComparer.OrdinalIgnoreCase);
        }

        return ordered.Take(NormalizeLimit(limit)).ToArray();
    }

    public static PricingReviewFormulaComparisonSummaryDto BuildFormulaComparisonSummary(
        ProductPricingSourceOptionDto? standardSource,
        ProductPricingSourceOptionDto viewedSource,
        IReadOnlyCollection<PricingReviewFormulaMaterialComparisonDto> allItems,
        DateTime calculatedAt,
        PricingReviewFormulaComparisonUnavailableReason? sourceUnavailableReason)
    {
        var standardCost = standardSource?.IsCurrentMaterialCostComplete == true
            ? standardSource.CurrentMaterialCost
            : null;
        var viewedCost = viewedSource.IsCurrentMaterialCostComplete
            ? viewedSource.CurrentMaterialCost
            : null;
        var hasUnitMismatch = allItems.Any(x =>
            x.Status == PricingReviewFormulaMaterialComparisonStatus.UnitMismatch);
        var unavailableReason = sourceUnavailableReason ??
            (standardSource is not null && (!standardCost.HasValue || !viewedCost.HasValue)
                ? PricingReviewFormulaComparisonUnavailableReason.MissingCurrentPrice
                : hasUnitMismatch
                    ? PricingReviewFormulaComparisonUnavailableReason.UnitMismatch
                    : null);
        var canCompare = unavailableReason is null &&
            standardCost.HasValue &&
            viewedCost.HasValue;
        decimal? differenceAmount = canCompare
            ? PricingRoundingRules.RoundCalculatedPrice(viewedCost!.Value - standardCost!.Value)
            : null;

        return new PricingReviewFormulaComparisonSummaryDto
        {
            CalculatedAt = calculatedAt,
            StandardFormulaMaterialCost = standardCost,
            ViewedFormulaMaterialCost = viewedCost,
            DifferenceAmount = differenceAmount,
            DifferencePercent = CalculatePercent(differenceAmount, standardCost),
            ComparisonStatus = ToCostComparisonStatus(differenceAmount),
            StandardMaterialCount = allItems.Count(x => x.StandardFormula.IsPresent),
            ViewedMaterialCount = allItems.Count(x => x.ViewedFormula.IsPresent),
            MatchedMaterialCount = allItems.Count(x =>
                x.StandardFormula.IsPresent && x.ViewedFormula.IsPresent),
            AddedMaterialCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.AddedToViewedFormula),
            RemovedMaterialCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.RemovedFromViewedFormula),
            QuantityChangedCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.QuantityChanged),
            UnchangedCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.Unchanged),
            MissingPriceCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.MissingCurrentPrice),
            UnitMismatchCount = allItems.Count(x =>
                x.Status == PricingReviewFormulaMaterialComparisonStatus.UnitMismatch),
            CanCompare = canCompare,
            UnavailableReason = unavailableReason
        };
    }

    public static int NormalizeLimit(int limit)
        => limit < 1 ? DefaultPreviewLimit : Math.Min(limit, MaximumPreviewLimit);

    private static IReadOnlyDictionary<FormulaComparisonItemKey, FormulaMaterialAggregate> AggregateMaterials(
        IReadOnlyList<QuotationProductPricingMaterialDto> materials)
        => materials
            .GroupBy(ToComparisonKey)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var rows = group.ToArray();
                    var pricedRow = rows.FirstOrDefault(x =>
                        x.HasLatestPrice &&
                        x.LatestUnitPrice.HasValue &&
                        x.LatestTotalPrice.HasValue);
                    var units = rows.Select(x => x.Unit?.Trim() ?? string.Empty)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return new FormulaMaterialAggregate
                    {
                        ItemType = group.Key.ItemType,
                        ItemId = group.Key.ItemId,
                        CategoryId = rows.Select(x => x.CategoryId)
                            .FirstOrDefault(x => x.HasValue),
                        MaterialCode = rows.Select(x => x.ItemCode)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                        MaterialName = rows.Select(x => x.ItemName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                        Unit = units.FirstOrDefault() ?? string.Empty,
                        HasInternalUnitMismatch = units.Length > 1,
                        FormulaMaterialIds = rows.Select(x => x.FormulaMaterialId).Distinct().ToArray(),
                        Quantity = rows.Sum(x => x.Quantity),
                        HasCurrentPrice = rows.All(x =>
                            x.HasLatestPrice &&
                            x.LatestUnitPrice.HasValue &&
                            x.LatestTotalPrice.HasValue),
                        CurrentUnitPrice = pricedRow?.LatestUnitPrice,
                        CurrentPriceDate = pricedRow?.LatestPriceDate,
                        CurrentPriceSource = pricedRow?.LatestPriceSource ?? LatestPriceSourceType.Unknown,
                        CurrentPriceCalculation = pricedRow?.PriceCalculation
                    };
                });

    private static PricingReviewFormulaMaterialComparisonDto MapFormulaComparisonItem(
        FormulaMaterialAggregate? standard,
        FormulaMaterialAggregate? viewed,
        bool hasStandardSource,
        IReadOnlyDictionary<Guid, PricingReviewFormulaItemCategory> categories)
    {
        var representative = viewed ?? standard!;
        var categoryId = viewed?.CategoryId ?? standard?.CategoryId;
        var category = categoryId.HasValue
            ? categories.GetValueOrDefault(categoryId.Value)
            : null;
        var categoryGroup = ProductPricingReviewMaterialCategoryRules.Resolve(
            category?.CategoryCode,
            category?.CategoryName);
        var currentPrice = viewed?.HasCurrentPrice == true
            ? viewed
            : standard?.HasCurrentPrice == true
                ? standard
                : viewed ?? standard;
        var hasCurrentPrice = currentPrice?.HasCurrentPrice == true &&
            currentPrice.CurrentUnitPrice.HasValue &&
            (standard is null || standard.HasCurrentPrice) &&
            (viewed is null || viewed.HasCurrentPrice);
        var unitMismatch = standard?.HasInternalUnitMismatch == true ||
            viewed?.HasInternalUnitMismatch == true ||
            standard is not null && viewed is not null &&
            !string.Equals(standard.Unit, viewed.Unit, StringComparison.OrdinalIgnoreCase);
        decimal? standardAmount = standard is null
            ? 0m
            : hasCurrentPrice && !unitMismatch
                ? PricingRoundingRules.RoundCalculatedPrice(
                    standard.Quantity * currentPrice!.CurrentUnitPrice!.Value)
                : null;
        decimal? viewedAmount = viewed is null
            ? 0m
            : hasCurrentPrice && !unitMismatch
                ? PricingRoundingRules.RoundCalculatedPrice(
                    viewed.Quantity * currentPrice!.CurrentUnitPrice!.Value)
                : null;
        decimal? amountDifference = standardAmount.HasValue && viewedAmount.HasValue
            ? PricingRoundingRules.RoundCalculatedPrice(viewedAmount.Value - standardAmount.Value)
            : null;
        var quantityDifference = PricingRoundingRules.RoundStoredInput(
            (viewed?.Quantity ?? 0m) - (standard?.Quantity ?? 0m));
        var status = !hasStandardSource
            ? PricingReviewFormulaMaterialComparisonStatus.Unavailable
            : !hasCurrentPrice
                ? PricingReviewFormulaMaterialComparisonStatus.MissingCurrentPrice
                : unitMismatch
                    ? PricingReviewFormulaMaterialComparisonStatus.UnitMismatch
                    : standard is null
                        ? PricingReviewFormulaMaterialComparisonStatus.AddedToViewedFormula
                        : viewed is null
                            ? PricingReviewFormulaMaterialComparisonStatus.RemovedFromViewedFormula
                            : quantityDifference != 0m
                                ? PricingReviewFormulaMaterialComparisonStatus.QuantityChanged
                                : PricingReviewFormulaMaterialComparisonStatus.Unchanged;

        return new PricingReviewFormulaMaterialComparisonDto
        {
            ItemType = representative.ItemType,
            MaterialId = representative.ItemId,
            MaterialCode = representative.MaterialCode,
            MaterialName = representative.MaterialName,
            CategoryId = categoryId,
            CategoryCode = category?.CategoryCode,
            CategoryName = category?.CategoryName,
            CategoryGroup = categoryGroup,
            CategoryGroupName = ProductPricingReviewMaterialCategoryRules.GetDisplayName(categoryGroup),
            Unit = viewed?.Unit ?? standard?.Unit ?? string.Empty,
            CurrentPrice = new PricingReviewFormulaCurrentPriceDto
            {
                UnitPrice = hasCurrentPrice ? currentPrice!.CurrentUnitPrice : null,
                PriceSource = hasCurrentPrice
                    ? currentPrice!.CurrentPriceSource
                    : LatestPriceSourceType.Unknown,
                PriceDate = hasCurrentPrice ? currentPrice!.CurrentPriceDate : null,
                Calculation = hasCurrentPrice ? currentPrice!.CurrentPriceCalculation : null
            },
            StandardFormula = ToSide(standard, standardAmount),
            ViewedFormula = ToSide(viewed, viewedAmount),
            QuantityDifference = quantityDifference,
            AmountDifference = amountDifference,
            DifferencePercent = CalculatePercent(amountDifference, standardAmount),
            Status = status
        };
    }

    private static PricingReviewFormulaMaterialSideDto ToSide(
        FormulaMaterialAggregate? material,
        decimal? amount)
        => material is null
            ? new PricingReviewFormulaMaterialSideDto
            {
                IsPresent = false,
                Quantity = 0m,
                Amount = 0m
            }
            : new PricingReviewFormulaMaterialSideDto
            {
                FormulaMaterialIds = material.FormulaMaterialIds,
                IsPresent = true,
                Quantity = material.Quantity,
                Amount = amount
            };

    private static FormulaComparisonItemKey ToComparisonKey(QuotationProductPricingMaterialDto material)
    {
        var normalizedType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(material.ItemType);
        var itemId = material.ItemId is { } id && id != Guid.Empty ? id : (Guid?)null;
        return new FormulaComparisonItemKey(
            normalizedType,
            itemId,
            itemId.HasValue ? string.Empty : material.ItemCode.Trim().ToUpperInvariant());
    }

    private static PricingReviewCostComparisonStatus ToCostComparisonStatus(decimal? difference)
        => difference switch
        {
            > 0m => PricingReviewCostComparisonStatus.Increased,
            < 0m => PricingReviewCostComparisonStatus.Decreased,
            0m => PricingReviewCostComparisonStatus.Unchanged,
            _ => PricingReviewCostComparisonStatus.Unavailable
        };

    private static decimal? CalculatePercent(decimal? difference, decimal? baseline)
        => !difference.HasValue || !baseline.HasValue || baseline.Value == 0m
            ? null
            : decimal.Round(
                difference.Value / baseline.Value * 100m,
                4,
                MidpointRounding.AwayFromZero);

    private static decimal Abs(decimal? value) => value.HasValue ? Math.Abs(value.Value) : -1m;

    private readonly record struct FormulaComparisonItemKey(
        ItemType ItemType,
        Guid? ItemId,
        string FallbackCode);

    private sealed class FormulaMaterialAggregate
    {
        public ItemType ItemType { get; init; }
        public Guid? ItemId { get; init; }
        public Guid? CategoryId { get; init; }
        public string MaterialCode { get; init; } = string.Empty;
        public string MaterialName { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public bool HasInternalUnitMismatch { get; init; }
        public IReadOnlyList<Guid> FormulaMaterialIds { get; init; } = [];
        public decimal Quantity { get; init; }
        public bool HasCurrentPrice { get; init; }
        public decimal? CurrentUnitPrice { get; init; }
        public DateTime? CurrentPriceDate { get; init; }
        public LatestPriceSourceType CurrentPriceSource { get; init; }
        public PriceCalculationDetailDto? CurrentPriceCalculation { get; init; }
    }
}
