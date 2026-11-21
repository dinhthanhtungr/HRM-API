using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Xác định các đơn bán thông thường cần đối chiếu giá chốt với giá chuẩn đã duyệt.
/// Đơn nội bộ, khiếu nại và gửi mẫu không áp dụng cảnh báo này.
/// </summary>
internal static class SaleOrderApprovalPriceWarningRules
{
    public const string MessagePrefix = "Đã duyệt và tạo lệnh sản xuất. Cảnh báo giá thấp hơn giá chuẩn đã duyệt:";

    public static bool ShouldCheck(OrderType orderType, bool isInternalCustomer)
        => orderType == OrderType.Merchandise && !isInternalCustomer;

    public static bool IsBelowApprovedStandardPrice(decimal unitPriceAgreed, decimal approvedStandardSellingPrice)
        => unitPriceAgreed < approvedStandardSellingPrice;

    public static decimal ConvertApprovedPrice(decimal vndPrice, string currency, decimal exchangeRate)
        => QuotationPricingCurrencyConverter.ConvertFromStandardPricing(vndPrice, currency, exchangeRate);

    // Match the SaleOrder form: first matching range by SortOrder, honoring open bounds.
    // If quantity falls in a gap or no active tier exists, retain the approved base price.
    public static decimal ResolveApprovedPrice(decimal quantity, decimal standardSellingPrice,
        IEnumerable<ProductPricingTier> tiers)
        => tiers.Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .FirstOrDefault(t =>
                (!t.MinQuantity.HasValue || quantity > t.MinQuantity.Value ||
                    (t.MinInclusive && quantity == t.MinQuantity.Value)) &&
                (!t.MaxQuantity.HasValue || quantity < t.MaxQuantity.Value ||
                    (t.MaxInclusive && quantity == t.MaxQuantity.Value)))?.UnitPrice
            ?? standardSellingPrice;

    public static bool IsWarningMessage(string? message)
        => message?.StartsWith(MessagePrefix, StringComparison.Ordinal) == true;
}
