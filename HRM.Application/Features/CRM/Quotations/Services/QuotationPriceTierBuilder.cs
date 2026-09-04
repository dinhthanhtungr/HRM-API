using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationPriceTierBuilder
{
    public static OperationResult<QuotationLinePricing> Build(
        Guid quotationLineId,
        decimal quantity,
        IReadOnlyList<QuotationLinePriceTierRequest>? requests,
        string fieldPath)
    {
        requests ??= [];
        if (requests.Count == 0)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers must contain at least one active tier.");
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

            if (request.UnitPrice < 0m || request.CommissionAmount < 0m || request.SortOrder < 0 ||
                request.MinQuantity is < 0m || request.MaxQuantity is < 0m)
            {
                return OperationResult<QuotationLinePricing>.Fail(
                    $"{fieldPath}.priceTiers[{index}] must have non-negative unitPrice, commissionAmount and quantity values.");
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
                request.CommissionAmount,
                request.SortOrder,
                request.IsActive));
        }

        if (normalized.Select(x => x.SortOrder).Distinct().Count() != normalized.Count)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers must have unique sortOrder values.");
        }

        var activeTiers = normalized.Where(x => x.IsActive).ToArray();
        if (activeTiers.Length == 0)
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.priceTiers must contain at least one active tier.");
        }
        var byRange = activeTiers
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

        var effectiveTier = activeTiers.FirstOrDefault(x => Contains(x, quantity)) ??
            activeTiers.MinBy(x => x.SortOrder)!;

        var entities = normalized
            .OrderBy(x => x.SortOrder)
            .Select(x =>
            {
                var tier = new QuotationLinePriceTier
                {
                    QuotationLinePriceTierId = Guid.CreateVersion7(),
                    QuotationLineId = quotationLineId,
                    QuantityRangeLabel = x.QuantityRangeLabel,
                    MinQuantity = x.MinQuantity,
                    MaxQuantity = x.MaxQuantity,
                    MinInclusive = x.MinInclusive,
                    MaxInclusive = x.MaxInclusive,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                };
                tier.SetPrices(x.UnitPrice, x.CommissionAmount);
                return tier;
            })
            .ToList();

        return OperationResult<QuotationLinePricing>.Ok(
            new QuotationLinePricing(effectiveTier.UnitPrice, entities));
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
        decimal CommissionAmount,
        int SortOrder,
        bool IsActive);
}

internal sealed record QuotationLinePricing(
    decimal EffectiveUnitPrice,
    IReadOnlyList<QuotationLinePriceTier> PriceTiers);
