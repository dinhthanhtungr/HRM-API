using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Comparison;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Queries.CompareFormulas;

internal static class FormulaComparisonCalculator
{
    public static FormulaComparisonDto Compare(
        FormulaComparisonSource baseFormula,
        FormulaComparisonSource comparedFormula,
        string currency,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem,
        DateTime calculatedAt)
    {
        var baseByItem = Aggregate(baseFormula.Items);
        var comparedByItem = Aggregate(comparedFormula.Items);
        var keys = baseByItem.Keys.Concat(comparedByItem.Keys).Distinct().ToArray();
        var items = keys
            .Select(key => MapItem(
                baseByItem.GetValueOrDefault(key),
                comparedByItem.GetValueOrDefault(key),
                latestPriceByItem))
            .OrderBy(x => x.Status == FormulaComparisonItemStatus.MissingCurrentPrice ? 0 : 1)
            .ThenByDescending(x => x.AmountDifference.HasValue
                ? Math.Abs(x.AmountDifference.Value)
                : -1m)
            .ThenBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingPriceCount = items.Count(x =>
            x.Status == FormulaComparisonItemStatus.MissingCurrentPrice);
        var unitMismatchCount = items.Count(x =>
            x.Status == FormulaComparisonItemStatus.UnitMismatch);
        var unavailableReason = missingPriceCount > 0
            ? FormulaComparisonUnavailableReason.MissingCurrentPrice
            : unitMismatchCount > 0
                ? FormulaComparisonUnavailableReason.UnitMismatch
                : (FormulaComparisonUnavailableReason?)null;
        var canCompare = unavailableReason is null;
        var baseCost = canCompare
            ? PricingRoundingRules.RoundCalculatedPrice(items.Sum(x => x.BaseFormula.Amount ?? 0m))
            : (decimal?)null;
        var comparedCost = canCompare
            ? PricingRoundingRules.RoundCalculatedPrice(items.Sum(x => x.ComparedFormula.Amount ?? 0m))
            : (decimal?)null;
        var differenceAmount = baseCost.HasValue && comparedCost.HasValue
            ? PricingRoundingRules.RoundCalculatedPrice(comparedCost.Value - baseCost.Value)
            : (decimal?)null;

        return new FormulaComparisonDto
        {
            Currency = currency,
            BaseFormula = MapFormula(baseFormula),
            ComparedFormula = MapFormula(comparedFormula),
            Summary = new FormulaComparisonSummaryDto
            {
                CalculatedAt = calculatedAt,
                BaseFormulaMaterialCost = baseCost,
                ComparedFormulaMaterialCost = comparedCost,
                DifferenceAmount = differenceAmount,
                DifferencePercent = CalculatePercent(differenceAmount, baseCost),
                ComparisonStatus = ToComparisonStatus(differenceAmount),
                BaseItemCount = items.Count(x => x.BaseFormula.IsPresent),
                ComparedItemCount = items.Count(x => x.ComparedFormula.IsPresent),
                MatchedItemCount = items.Count(x =>
                    x.BaseFormula.IsPresent && x.ComparedFormula.IsPresent),
                AddedToComparedCount = items.Count(x =>
                    x.Status == FormulaComparisonItemStatus.AddedToComparedFormula),
                RemovedFromComparedCount = items.Count(x =>
                    x.Status == FormulaComparisonItemStatus.RemovedFromComparedFormula),
                QuantityChangedCount = items.Count(x =>
                    x.Status == FormulaComparisonItemStatus.QuantityChanged),
                UnchangedCount = items.Count(x =>
                    x.Status == FormulaComparisonItemStatus.Unchanged),
                MissingPriceCount = missingPriceCount,
                UnitMismatchCount = unitMismatchCount,
                CanCompare = canCompare,
                UnavailableReason = unavailableReason
            },
            Items = items
        };
    }

    private static IReadOnlyDictionary<FormulaComparisonItemKey, FormulaComparisonAggregate> Aggregate(
        IReadOnlyCollection<FormulaComparisonMaterial> materials)
        => materials
            .GroupBy(ToKey)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var rows = group.ToArray();
                    var units = rows
                        .Select(x => NormalizeUnit(x.Unit))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var representative = rows[0];

                    return new FormulaComparisonAggregate
                    {
                        ItemType = group.Key.ItemType,
                        ItemId = group.Key.ItemId,
                        ItemCode = rows.Select(x => x.ItemCode)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                        ItemName = rows.Select(x => x.ItemName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                        CategoryId = rows.Select(x => x.CategoryId)
                            .FirstOrDefault(x => x.HasValue),
                        Unit = units.FirstOrDefault() ?? string.Empty,
                        HasInternalUnitMismatch = units.Length > 1,
                        FormulaMaterialIds = rows.Select(x => x.FormulaMaterialId)
                            .Distinct()
                            .ToArray(),
                        Quantity = rows.Sum(x => x.Quantity),
                        FallbackPriceKey = representative.PriceKey
                    };
                });

    private static FormulaComparisonItemDto MapItem(
        FormulaComparisonAggregate? baseItem,
        FormulaComparisonAggregate? comparedItem,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem)
    {
        var representative = comparedItem ?? baseItem!;
        var priceKey = representative.FallbackPriceKey;
        LatestItemPriceDto? currentPrice = null;
        var hasCurrentPrice = priceKey.HasValue &&
            latestPriceByItem.TryGetValue(priceKey.Value, out currentPrice) &&
            currentPrice.PriceSource != LatestPriceSourceType.Unknown;
        var unitMismatch = baseItem?.HasInternalUnitMismatch == true ||
            comparedItem?.HasInternalUnitMismatch == true ||
            baseItem is not null && comparedItem is not null &&
            !string.Equals(baseItem.Unit, comparedItem.Unit, StringComparison.OrdinalIgnoreCase);
        var baseAmount = CalculateAmount(baseItem, hasCurrentPrice, unitMismatch, currentPrice);
        var comparedAmount = CalculateAmount(comparedItem, hasCurrentPrice, unitMismatch, currentPrice);
        var amountDifference = baseAmount.HasValue && comparedAmount.HasValue
            ? PricingRoundingRules.RoundCalculatedPrice(comparedAmount.Value - baseAmount.Value)
            : (decimal?)null;
        var quantityDifference = PricingRoundingRules.RoundStoredInput(
            (comparedItem?.Quantity ?? 0m) - (baseItem?.Quantity ?? 0m));
        var status = !hasCurrentPrice
            ? FormulaComparisonItemStatus.MissingCurrentPrice
            : unitMismatch
                ? FormulaComparisonItemStatus.UnitMismatch
                : baseItem is null
                    ? FormulaComparisonItemStatus.AddedToComparedFormula
                    : comparedItem is null
                        ? FormulaComparisonItemStatus.RemovedFromComparedFormula
                        : quantityDifference != 0m
                            ? FormulaComparisonItemStatus.QuantityChanged
                            : FormulaComparisonItemStatus.Unchanged;
        var normalizedType = representative.ItemType;

        return new FormulaComparisonItemDto
        {
            ItemType = normalizedType,
            MaterialId = normalizedType == ItemType.Material ? representative.ItemId : null,
            ProductId = normalizedType == ItemType.Product ? representative.ItemId : null,
            ItemCode = representative.ItemCode,
            ItemName = representative.ItemName,
            CategoryId = comparedItem?.CategoryId ?? baseItem?.CategoryId,
            Unit = comparedItem?.Unit ?? baseItem?.Unit ?? string.Empty,
            CurrentPrice = new FormulaComparisonCurrentPriceDto
            {
                UnitPrice = hasCurrentPrice ? currentPrice!.CurrentPrice : null,
                PriceSource = hasCurrentPrice
                    ? currentPrice!.PriceSource
                    : LatestPriceSourceType.Unknown,
                PriceDate = hasCurrentPrice ? currentPrice!.PriceDate : null,
                Calculation = hasCurrentPrice ? currentPrice!.Calculation : null
            },
            BaseFormula = ToSide(baseItem, baseAmount),
            ComparedFormula = ToSide(comparedItem, comparedAmount),
            QuantityDifference = quantityDifference,
            AmountDifference = amountDifference,
            DifferencePercent = CalculatePercent(amountDifference, baseAmount),
            Status = status
        };
    }

    private static decimal? CalculateAmount(
        FormulaComparisonAggregate? item,
        bool hasCurrentPrice,
        bool unitMismatch,
        LatestItemPriceDto? currentPrice)
        => item is null
            ? 0m
            : hasCurrentPrice && !unitMismatch
                ? PricingRoundingRules.RoundCalculatedPrice(
                    item.Quantity * currentPrice!.CurrentPrice)
                : null;

    private static FormulaComparisonSideDto ToSide(
        FormulaComparisonAggregate? item,
        decimal? amount)
        => item is null
            ? new FormulaComparisonSideDto
            {
                IsPresent = false,
                Quantity = 0m,
                Amount = 0m
            }
            : new FormulaComparisonSideDto
            {
                FormulaMaterialIds = item.FormulaMaterialIds,
                IsPresent = true,
                Quantity = item.Quantity,
                Amount = amount
            };

    private static FormulaComparisonFormulaDto MapFormula(FormulaComparisonSource formula)
        => new()
        {
            FormulaId = formula.FormulaId,
            ProductId = formula.ProductId,
            ExternalId = formula.ExternalId,
            Name = formula.Name,
            Status = formula.Status,
            StepOfProduct = formula.StepOfProduct
        };

    private static FormulaComparisonItemKey ToKey(FormulaComparisonMaterial item)
    {
        var normalizedType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(item.ItemType);
        var itemId = item.ItemId is { } id && id != Guid.Empty ? id : (Guid?)null;
        return new FormulaComparisonItemKey(
            normalizedType,
            itemId,
            itemId.HasValue ? string.Empty : item.ItemCode.Trim().ToUpperInvariant());
    }

    private static string NormalizeUnit(string? unit) => unit?.Trim() ?? string.Empty;

    private static FormulaComparisonStatus ToComparisonStatus(decimal? difference)
        => difference switch
        {
            > 0m => FormulaComparisonStatus.Increased,
            < 0m => FormulaComparisonStatus.Decreased,
            0m => FormulaComparisonStatus.Unchanged,
            _ => FormulaComparisonStatus.Unavailable
        };

    private static decimal? CalculatePercent(decimal? difference, decimal? baseline)
        => !difference.HasValue || !baseline.HasValue || baseline.Value == 0m
            ? null
            : decimal.Round(
                difference.Value / baseline.Value * 100m,
                4,
                MidpointRounding.AwayFromZero);

    private readonly record struct FormulaComparisonItemKey(
        ItemType ItemType,
        Guid? ItemId,
        string FallbackCode);

    private sealed class FormulaComparisonAggregate
    {
        public ItemType ItemType { get; init; }
        public Guid? ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public Guid? CategoryId { get; init; }
        public string Unit { get; init; } = string.Empty;
        public bool HasInternalUnitMismatch { get; init; }
        public IReadOnlyList<Guid> FormulaMaterialIds { get; init; } = [];
        public decimal Quantity { get; init; }
        public PriceItemKey? FallbackPriceKey { get; init; }
    }
}

internal sealed class FormulaComparisonSource
{
    public Guid FormulaId { get; init; }
    public Guid ProductId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public HRM.Domain.Enums.Manufacturings.StepOfProduct? StepOfProduct { get; init; }
    public IReadOnlyCollection<FormulaComparisonMaterial> Items { get; set; } = [];
}

internal sealed class FormulaComparisonMaterial
{
    public Guid FormulaMaterialId { get; init; }
    public Guid FormulaId { get; set; }
    public Guid? ItemId { get; init; }
    public ItemType ItemType { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;

    public PriceItemKey? PriceKey => ItemId is { } id && id != Guid.Empty
        ? new PriceItemKey(FormulaRealtimeMaterialCostCalculator.NormalizeItemType(ItemType), id)
        : null;
}
