using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationDisplayStatusResolver
{
    internal static QuotationDisplayStatusDto Resolve(
        QuotationStatus status, IEnumerable<QuotationStandardPriceReviewDto> reviews)
    {
        // Review không được che trạng thái đã gửi, đang xác nhận gửi hoặc các bước sau gửi.
        if (status is QuotationStatus.PendingApproval or QuotationStatus.Approved)
        {
            var states = reviews.Select(x => x.State).ToArray();
            if (states.Contains(ProductStandardPriceState.PendingReapproval))
                return new("PendingPriceUpdate", "warning", "StandardPriceReviewRequired");
            if (states.Length == 0 || states.Contains(ProductStandardPriceState.PendingInitialApproval) ||
                states.Contains(ProductStandardPriceState.Missing) || status == QuotationStatus.PendingApproval)
                return new("PendingPriceApproval", "warning", "StandardPriceApprovalRequired");
        }

        return status switch
        {
            QuotationStatus.Draft => new("Draft", "warning", null),
            QuotationStatus.Approved => new("Approved", "primary", null),
            QuotationStatus.Sent => new("Sent", "success", null),
            QuotationStatus.Negotiating => new("Negotiating", "warning", null),
            QuotationStatus.Accepted => new("Accepted", "success", null),
            QuotationStatus.Rejected => new("Rejected", "danger", null),
            QuotationStatus.Expired => new("Expired", "muted", null),
            QuotationStatus.Cancelled => new("Cancelled", "muted", null),
            QuotationStatus.PendingSendConfirmation => new("PendingSendConfirmation", "warning", null),
            _ => new("Unknown", "muted", null)
        };
    }
}
