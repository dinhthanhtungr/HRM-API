using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingVersionRules
{
    private const int PercentScale = 4;

    public static string? ValidatePricingValues(
        decimal? materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice,
        decimal? profitMarginRate)
    {
        if (materialCost is < 0m || manufacturingCost is < 0m || standardSellingPrice is < 0m)
        {
            return "Pricing cost and selling price values cannot be negative.";
        }

        return profitMarginRate is < 0m or > 100m
            ? "ProfitMarginRate must be between 0 and 100."
            : null;
    }

    public static OperationResult<NormalizedProductPricingValues> NormalizePricingValues(
        decimal? materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice,
        decimal? profitMarginRate,
        ProductPricingChangedField? changedField)
    {
        if (changedField.HasValue && !Enum.IsDefined(changedField.Value))
        {
            return OperationResult<NormalizedProductPricingValues>.Fail(
                "ChangedField is invalid.");
        }

        var pricingError = ValidatePricingValues(
            materialCost,
            manufacturingCost,
            standardSellingPrice,
            profitMarginRate);
        if (pricingError is not null)
        {
            return OperationResult<NormalizedProductPricingValues>.Fail(pricingError);
        }

        decimal? normalizedMaterialCost = materialCost.HasValue
            ? PricingRoundingRules.RoundCalculatedPrice(materialCost.Value)
            : null;
        var normalizedManufacturingCost = RoundStoredInputIfProvided(manufacturingCost);
        var normalizedStandardSellingPrice = RoundStoredInputIfProvided(standardSellingPrice);
        var normalizedProfitMarginRate = RoundPercentIfProvided(profitMarginRate);

        if (!normalizedMaterialCost.HasValue || !normalizedManufacturingCost.HasValue)
        {
            if (changedField.HasValue)
            {
                return OperationResult<NormalizedProductPricingValues>.Fail(
                    "Realtime material cost and manufacturing cost are required to recalculate product pricing.");
            }

            return OperationResult<NormalizedProductPricingValues>.Ok(
                new NormalizedProductPricingValues(
                    normalizedMaterialCost,
                    normalizedManufacturingCost,
                    normalizedStandardSellingPrice,
                    normalizedProfitMarginRate));
        }

        var costBase = PricingRoundingRules.RoundCalculatedPrice(
            normalizedMaterialCost.GetValueOrDefault() +
            normalizedManufacturingCost.GetValueOrDefault());
        if (costBase <= 0m &&
            (changedField.HasValue ||
             normalizedStandardSellingPrice is > 0m ||
             normalizedProfitMarginRate is > 0m))
        {
            return OperationResult<NormalizedProductPricingValues>.Fail(
                "A positive material and manufacturing cost base is required to recalculate product pricing.");
        }

        switch (changedField)
        {
            case ProductPricingChangedField.ManufacturingCost:
                normalizedProfitMarginRate ??= 0m;
                normalizedStandardSellingPrice = CalculateStandardSellingPrice(
                    costBase,
                    normalizedProfitMarginRate.Value);
                break;

            case ProductPricingChangedField.StandardSellingPrice:
                if (!normalizedStandardSellingPrice.HasValue)
                {
                    return OperationResult<NormalizedProductPricingValues>.Fail(
                        "StandardSellingPrice is required when ChangedField is StandardSellingPrice.");
                }

                normalizedProfitMarginRate = CalculateProfitMarginRate(
                    normalizedStandardSellingPrice.Value,
                    costBase);
                break;

            case ProductPricingChangedField.ProfitMarginRate:
                if (!normalizedProfitMarginRate.HasValue)
                {
                    return OperationResult<NormalizedProductPricingValues>.Fail(
                        "ProfitMarginRate is required when ChangedField is ProfitMarginRate.");
                }

                normalizedStandardSellingPrice = CalculateStandardSellingPrice(
                    costBase,
                    normalizedProfitMarginRate.Value);
                break;

            default:
                if (normalizedStandardSellingPrice.HasValue)
                {
                    normalizedProfitMarginRate = CalculateProfitMarginRate(
                        normalizedStandardSellingPrice.Value,
                        costBase);
                }
                else if (normalizedProfitMarginRate.HasValue)
                {
                    normalizedStandardSellingPrice = CalculateStandardSellingPrice(
                        costBase,
                        normalizedProfitMarginRate.Value);
                }
                else
                {
                    normalizedStandardSellingPrice =
                        PricingRoundingRules.RoundCalculatedPrice(costBase);
                    normalizedProfitMarginRate = 0m;
                }

                break;
        }

        pricingError = ValidatePricingValues(
            normalizedMaterialCost,
            normalizedManufacturingCost,
            normalizedStandardSellingPrice,
            normalizedProfitMarginRate);
        if (pricingError is not null)
        {
            return OperationResult<NormalizedProductPricingValues>.Fail(pricingError);
        }

        return OperationResult<NormalizedProductPricingValues>.Ok(
            new NormalizedProductPricingValues(
                normalizedMaterialCost,
                normalizedManufacturingCost,
                normalizedStandardSellingPrice,
                normalizedProfitMarginRate));
    }

    public static OperationResult<IReadOnlyList<ProductPricingTier>> BuildTiers(
        Guid productPricingVersionId,
        IReadOnlyList<ProductPricingTierRequest>? requests)
    {
        requests ??= [];
        if (requests.Count > QuotationRules.MaximumPriceTierCountPerLine)
        {
            return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                $"PriceTiers cannot contain more than {QuotationRules.MaximumPriceTierCountPerLine} tiers.");
        }

        var normalized = new List<NormalizedTier>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var label = QuotationRules.TrimToNull(request.QuantityRangeLabel);
            if (label is null || label.Length > QuotationRules.MaximumQuantityRangeLabelLength)
            {
                return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                    $"PriceTiers[{index}].QuantityRangeLabel is required and cannot exceed " +
                    $"{QuotationRules.MaximumQuantityRangeLabelLength} characters.");
            }

            if (request.UnitPrice < 0m || request.SortOrder < 0 ||
                request.MinQuantity is < 0m || request.MaxQuantity is < 0m)
            {
                return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                    $"PriceTiers[{index}] must have a non-negative UnitPrice and no negative values.");
            }

            if (request.MinQuantity.HasValue && request.MaxQuantity.HasValue &&
                (request.MinQuantity > request.MaxQuantity ||
                 (request.MinQuantity == request.MaxQuantity &&
                  (!request.MinInclusive || !request.MaxInclusive))))
            {
                return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                    $"PriceTiers[{index}] has an invalid quantity range.");
            }

            normalized.Add(new NormalizedTier(
                label,
                request.MinQuantity,
                request.MaxQuantity,
                request.MinInclusive,
                request.MaxInclusive,
                request.UnitPrice,
                request.SortOrder));
        }

        if (normalized.Select(x => x.SortOrder).Distinct().Count() != normalized.Count)
        {
            return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                "PriceTiers must have unique SortOrder values.");
        }

        var byRange = normalized
            .OrderBy(x => x.MinQuantity.HasValue ? 1 : 0)
            .ThenBy(x => x.MinQuantity)
            .ToArray();
        for (var index = 1; index < byRange.Length; index++)
        {
            if (RangesOverlap(byRange[index - 1], byRange[index]))
            {
                return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                    "PriceTiers contains overlapping quantity ranges.");
            }
        }

        var entities = normalized
            .OrderBy(x => x.SortOrder)
            .Select(x => new ProductPricingTier
            {
                ProductPricingTierId = Guid.CreateVersion7(),
                ProductPricingVersionId = productPricingVersionId,
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder
            })
            .ToArray();

        return OperationResult<IReadOnlyList<ProductPricingTier>>.Ok(entities);
    }

    public static ProductPricingChangedField? ResolveChangedField(
        ProductPricingChangedField? requestedChangedField,
        decimal? currentManufacturingCost,
        decimal? currentStandardSellingPrice,
        decimal? currentProfitMarginRate,
        decimal? requestedManufacturingCost,
        decimal? requestedStandardSellingPrice,
        decimal? requestedProfitMarginRate)
    {
        if (requestedChangedField.HasValue)
        {
            return requestedChangedField;
        }

        if (requestedManufacturingCost != currentManufacturingCost)
        {
            return ProductPricingChangedField.ManufacturingCost;
        }

        if (requestedStandardSellingPrice != currentStandardSellingPrice)
        {
            return ProductPricingChangedField.StandardSellingPrice;
        }

        return requestedProfitMarginRate != currentProfitMarginRate
            ? ProductPricingChangedField.ProfitMarginRate
            : null;
    }

    public static bool Contains(ProductPricingTier tier, decimal quantity)
    {
        var aboveMinimum = !tier.MinQuantity.HasValue ||
            quantity > tier.MinQuantity.Value ||
            (tier.MinInclusive && quantity == tier.MinQuantity.Value);
        var belowMaximum = !tier.MaxQuantity.HasValue ||
            quantity < tier.MaxQuantity.Value ||
            (tier.MaxInclusive && quantity == tier.MaxQuantity.Value);
        return aboveMinimum && belowMaximum;
    }

    private static bool RangesOverlap(NormalizedTier left, NormalizedTier right)
    {
        if (!left.MaxQuantity.HasValue || !right.MinQuantity.HasValue)
        {
            return true;
        }

        return right.MinQuantity < left.MaxQuantity ||
            (right.MinQuantity == left.MaxQuantity && left.MaxInclusive && right.MinInclusive);
    }

    private static decimal CalculateStandardSellingPrice(
        decimal costBase,
        decimal profitMarginRate)
        => PricingRoundingRules.RoundCalculatedPrice(
            costBase * (1m + profitMarginRate / 100m));

    private static decimal CalculateProfitMarginRate(
        decimal standardSellingPrice,
        decimal costBase)
    {
        if (costBase <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            (standardSellingPrice - costBase) / costBase * 100m,
            PercentScale,
            MidpointRounding.AwayFromZero);
    }

    private static decimal? RoundStoredInputIfProvided(decimal? value)
        => value.HasValue
            ? PricingRoundingRules.RoundStoredInput(value.Value)
            : null;

    private static decimal? RoundPercentIfProvided(decimal? value)
        => value.HasValue
            ? decimal.Round(
                value.Value,
                PercentScale,
                MidpointRounding.AwayFromZero)
            : null;

    private sealed record NormalizedTier(
        string QuantityRangeLabel,
        decimal? MinQuantity,
        decimal? MaxQuantity,
        bool MinInclusive,
        bool MaxInclusive,
        decimal UnitPrice,
        int SortOrder);
}

internal sealed record NormalizedProductPricingValues(
    decimal? MaterialCostSnapshot,
    decimal? ManufacturingCost,
    decimal? StandardSellingPrice,
    decimal? ProfitMarginRate);
