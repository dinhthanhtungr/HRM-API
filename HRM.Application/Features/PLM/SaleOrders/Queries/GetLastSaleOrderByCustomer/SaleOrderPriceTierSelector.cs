using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;

internal static class SaleOrderPriceTierSelector
{
    public static SaleOrderPriceTierSelection Select(ResolvedProductTierPricingReferences references)
    {
        if (references.LatestQuotedPricing is { PriceTiers.Count: > 0 } latest)
        {
            return new SaleOrderPriceTierSelection(
                SaleOrderPriceTierSource.LatestCustomerQuotation,
                latest.SentDate,
                latest.QuotationId,
                latest.QuotationExternalId,
                latest.PriceTiers);
        }

        if (references.ApprovedPricing is { PriceTiers.Count: > 0 } approved)
        {
            return new SaleOrderPriceTierSelection(
                SaleOrderPriceTierSource.ApprovedProductPricing,
                approved.ApprovedAt ?? approved.PriceTiers.FirstOrDefault()?.PriceDate,
                null,
                null,
                approved.PriceTiers);
        }

        if (references.SystemCalculatedPricing is { PriceTiers.Count: > 0 } system)
        {
            return new SaleOrderPriceTierSelection(
                SaleOrderPriceTierSource.SystemCalculated,
                system.CalculatedAt,
                null,
                null,
                system.PriceTiers);
        }

        return SaleOrderPriceTierSelection.Unavailable;
    }
}

internal sealed record SaleOrderPriceTierSelection(
    SaleOrderPriceTierSource Source,
    DateTime? SourceDate,
    Guid? QuotationId,
    string? QuotationExternalId,
    IReadOnlyList<QuotationTierPriceReference> PriceTiers)
{
    public static SaleOrderPriceTierSelection Unavailable { get; } = new(
        SaleOrderPriceTierSource.Unavailable,
        null,
        null,
        null,
        []);
}
