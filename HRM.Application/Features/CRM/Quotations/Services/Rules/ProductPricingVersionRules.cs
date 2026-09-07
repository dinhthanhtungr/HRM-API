using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingVersionRules
{
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
                request.SortOrder,
                request.IsActive));
        }

        if (normalized.Select(x => x.SortOrder).Distinct().Count() != normalized.Count)
        {
            return OperationResult<IReadOnlyList<ProductPricingTier>>.Fail(
                "PriceTiers must have unique SortOrder values.");
        }

        var byRange = normalized
            .Where(x => x.IsActive)
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
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
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

    private sealed record NormalizedTier(
        string QuantityRangeLabel,
        decimal? MinQuantity,
        decimal? MaxQuantity,
        bool MinInclusive,
        bool MaxInclusive,
        decimal UnitPrice,
        int SortOrder,
        bool IsActive);
}
