using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Resolves the business state of a product's standard price without exposing
/// Formula/cost internals to quotation consumers.
/// </summary>
internal static class ProductStandardPriceStateResolver
{
    public static ProductStandardPriceStateResult Resolve(
        ProductPricingVersion? approvedPricing,
        DateTime? latestFormulaConfirmedAt,
        DateTime now,
        QuotationFeatureOptions options)
    {
        if (approvedPricing is null)
        {
            return new ProductStandardPriceStateResult(
                ProductStandardPriceState.PendingInitialApproval,
                false,
                false,
                null);
        }

        var approvedAt = approvedPricing.ApprovedAt ??
            approvedPricing.UpdatedDate ??
            approvedPricing.CreatedDate;
        var dueDate = options.ApprovedPricingReviewAfterDays > 0
            ? approvedAt.AddDays(options.ApprovedPricingReviewAfterDays)
            : (DateTime?)null;
        var formulaPending = latestFormulaConfirmedAt.HasValue &&
            latestFormulaConfirmedAt.Value > approvedAt;
        var reviewExpired = dueDate.HasValue && now >= dueDate.Value;
        return new ProductStandardPriceStateResult(
            formulaPending || reviewExpired
                ? ProductStandardPriceState.PendingReapproval
                : ProductStandardPriceState.Active,
            formulaPending,
            reviewExpired,
            dueDate);
    }
}

internal sealed record ProductStandardPriceStateResult(
    ProductStandardPriceState State,
    bool HasFormulaConfirmationPending,
    bool IsPricingReviewExpired,
    DateTime? PricingReviewDueDate);
