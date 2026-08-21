using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationPriceTierBuilder
{
    public static OperationResult<QuotationLinePricing> Build(
        Guid quotationLineId,
        QuotationLinePriceMode priceMode,
        decimal quantity,
        decimal fixedUnitPrice,
        IReadOnlyList<QuotationLinePriceTierRequest>? requests,
        string fieldPath,
        bool allowMissingPrice = false)
    {
        if (!Enum.IsDefined(priceMode))
        {
            return OperationResult<QuotationLinePricing>.Fail($"{fieldPath}.priceMode is invalid.");
        }

        requests ??= [];
        if (priceMode != QuotationLinePriceMode.Tiered)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceMode must be Tiered.");
        }

        if (requests.Count == 0)
        {
            return allowMissingPrice
                ? OperationResult<QuotationLinePricing>.Ok(
                    new QuotationLinePricing(0m, []))
                : OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers must contain at least one tier for Tiered pricing.");
        }

        if (requests.Count > QuotationRules.MaximumPriceTierCountPerLine)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers cannot contain more than " +
                $"{QuotationRules.MaximumPriceTierCountPerLine} tiers.");
        }

        var normalized = new List<NormalizedTier>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var label = QuotationRules.TrimToNull(request.QuantityRangeLabel);
            if (label is null || label.Length > QuotationRules.MaximumQuantityRangeLabelLength)
            {
                return OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers[{index}].quantityRangeLabel is required and cannot exceed " +
                    $"{QuotationRules.MaximumQuantityRangeLabelLength} characters.");
            }

            if (request.UnitPrice < 0m || request.SortOrder < 0 ||
                request.MinQuantity is < 0m || request.MaxQuantity is < 0m)
            {
                return OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers[{index}] must have a non-negative unitPrice and no negative values.");
            }

            if (request.MinQuantity.HasValue && request.MaxQuantity.HasValue &&
                (request.MinQuantity.Value > request.MaxQuantity.Value ||
                 (request.MinQuantity.Value == request.MaxQuantity.Value &&
                  (!request.MinInclusive || !request.MaxInclusive))))
            {
                return OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers[{index}] has an invalid quantity range.");
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
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers must have unique sortOrder values.");
        }

        var byRange = normalized
            .OrderBy(x => x.MinQuantity.HasValue ? 1 : 0)
            .ThenBy(x => x.MinQuantity)
            .ToArray();

        for (var index = 1; index < byRange.Length; index++)
        {
            if (RangesOverlap(byRange[index - 1], byRange[index]))
            {
                return OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers contains overlapping quantity ranges.");
            }
        }

        var matchedTiers = normalized.Where(x => Contains(x, quantity)).ToArray();
        if (matchedTiers.Length != 1)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers must contain exactly one tier matching quantity {quantity}.");
        }

        var entities = normalized
            .OrderBy(x => x.SortOrder)
            .Select(x => new QuotationLinePriceTier
            {
                QuotationLinePriceTierId = Guid.CreateVersion7(),
                QuotationLineId = quotationLineId,
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder
            })
            .ToList();

        return OperationResult<QuotationLinePricing>.Ok(
            new QuotationLinePricing(matchedTiers[0].UnitPrice, entities));
    }

    private static bool Contains(NormalizedTier tier, decimal quantity)
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

        if (right.MinQuantity.Value < left.MaxQuantity.Value)
        {
            return true;
        }

        return right.MinQuantity.Value == left.MaxQuantity.Value &&
            left.MaxInclusive &&
            right.MinInclusive;
    }

    private sealed record NormalizedTier(
        string QuantityRangeLabel,
        decimal? MinQuantity,
        decimal? MaxQuantity,
        bool MinInclusive,
        bool MaxInclusive,
        decimal UnitPrice,
        int SortOrder);
}

internal sealed record QuotationLinePricing(
    decimal EffectiveUnitPrice,
    IReadOnlyList<QuotationLinePriceTier> PriceTiers);
