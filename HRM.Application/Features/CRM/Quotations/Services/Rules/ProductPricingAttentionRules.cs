using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingAttentionRules
{
    public static IReadOnlyList<ProductPricingAttentionSource> ForNeedsPricingQueue(
        IReadOnlyList<ProductPricingAttentionSource> sources)
        => sources
            .Where(source => source != ProductPricingAttentionSource.LabFormulaConfirmed)
            .ToArray();

    public static IReadOnlyList<ProductPricingRequestRow> GetPendingQuotationRequests(
        PricingVersionRow? approved,
        IReadOnlyList<ProductPricingRequestRow> requests)
        => GetPendingQuotationRequests(GetApprovedAt(approved), requests);

    public static IReadOnlyList<ProductPricingRequestRow> GetPendingQuotationRequests(
        DateTime? approvedAt,
        IReadOnlyList<ProductPricingRequestRow> requests)
    {
        return requests
            .Where(x => !approvedAt.HasValue || x.RequestedAt > approvedAt.Value)
            .ToArray();
    }

    public static IReadOnlyList<ProductPricingAttentionSource> Resolve(
        PricingVersionRow? approved,
        IReadOnlyList<ProductPricingRequestRow> requests,
        DateTime? latestFormulaConfirmedAt,
        DateTime now,
        QuotationFeatureOptions options,
        bool materialCostChanged = false)
        => Resolve(
            GetApprovedAt(approved),
            requests,
            latestFormulaConfirmedAt,
            now,
            options,
            materialCostChanged);

    public static IReadOnlyList<ProductPricingAttentionSource> Resolve(
        DateTime? approvedAt,
        IReadOnlyList<ProductPricingRequestRow> requests,
        DateTime? latestFormulaConfirmedAt,
        DateTime now,
        QuotationFeatureOptions options,
        bool materialCostChanged = false)
    {
        var sources = new List<ProductPricingAttentionSource>(4);

        if (GetPendingQuotationRequests(approvedAt, requests).Count > 0)
        {
            sources.Add(ProductPricingAttentionSource.SaleQuotationRequested);
        }

        if (approvedAt.HasValue &&
            latestFormulaConfirmedAt.HasValue &&
            latestFormulaConfirmedAt.Value > approvedAt.Value)
        {
            sources.Add(ProductPricingAttentionSource.LabFormulaConfirmed);
        }

        if (approvedAt.HasValue &&
            options.ApprovedPricingReviewAfterDays > 0 &&
            now >= approvedAt.Value.AddDays(options.ApprovedPricingReviewAfterDays))
        {
            sources.Add(ProductPricingAttentionSource.ReviewExpired);
        }

        if (materialCostChanged)
        {
            sources.Add(ProductPricingAttentionSource.MaterialCostIncreased);
        }

        return sources;
    }

    private static DateTime? GetApprovedAt(PricingVersionRow? approved)
        => approved?.ApprovedAt ?? approved?.UpdatedDate ?? approved?.CreatedDate;
}
