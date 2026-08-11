namespace HRM.Domain.Enums.CustomerEnum;

/// <summary>
/// Trạng thái lịch sử mua hàng dùng để lọc báo cáo chăm sóc khách hàng.
/// </summary>
public enum CustomerPurchaseStatus
{
    All = 0,
    HasPurchased = 1,
    NeverPurchased = 2,
    Dormant = 3
}
