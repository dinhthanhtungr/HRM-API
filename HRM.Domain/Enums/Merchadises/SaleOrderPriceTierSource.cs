namespace HRM.Domain.Enums.Merchadises;

/// <summary>
/// Nguồn của danh sách bậc giá gợi ý khi Sale lập đơn hàng.
/// </summary>
public enum SaleOrderPriceTierSource
{
    Unavailable = 0,
    LatestCustomerQuotation = 10,
    ApprovedProductPricing = 20,
    SystemCalculated = 30
}
