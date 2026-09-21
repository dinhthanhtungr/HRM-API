using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Xác định các đơn bán thông thường cần đối chiếu giá chốt với giá chuẩn đã duyệt.
/// Đơn nội bộ, khiếu nại và gửi mẫu không áp dụng cảnh báo này.
/// </summary>
internal static class SaleOrderApprovalPriceWarningRules
{
    public static bool ShouldCheck(OrderType orderType, bool isInternalCustomer)
        => orderType == OrderType.Merchandise && !isInternalCustomer;

    public static bool IsBelowApprovedStandardPrice(decimal unitPriceAgreed, decimal approvedStandardSellingPrice)
        => unitPriceAgreed < approvedStandardSellingPrice;
}
