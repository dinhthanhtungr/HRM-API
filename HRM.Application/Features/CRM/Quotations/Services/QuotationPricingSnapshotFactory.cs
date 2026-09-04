using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationPricingSnapshotFactory
{
    public static OperationResult<QuotationLinePricing> Create(
        Guid quotationLineId,
        Guid companyId,
        Guid productId,
        string currency,
        decimal quantity,
        ProductPricingVersion approvedVersion,
        string fieldPath)
    {
        if (approvedVersion.Status != ProductPricingStatus.Approved ||
            !approvedVersion.IsActive ||
            approvedVersion.CompanyId != companyId ||
            approvedVersion.ProductId != productId ||
            approvedVersion.StandardSellingPrice is null or < 0m ||
            !string.Equals(approvedVersion.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<QuotationLinePricing>.Fail(
                $"{fieldPath}.productPricingVersionId must reference an active approved version " +
                "for the same company, product, and currency.");
        }

        var tiers = approvedVersion.PriceTiers
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new QuotationLinePriceTierRequest
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                CommissionAmount = 0m,
                SortOrder = x.SortOrder
            })
            .ToArray();

        var tierResult = QuotationPriceTierBuilder.Build(
            quotationLineId,
            quantity,
            tiers,
            fieldPath);
        if (!tierResult.Success || tierResult.Data is null)
        {
            return tierResult;
        }

        return OperationResult<QuotationLinePricing>.Ok(
            new QuotationLinePricing(
                approvedVersion.StandardSellingPrice.Value,
                tierResult.Data.PriceTiers));
    }
}
