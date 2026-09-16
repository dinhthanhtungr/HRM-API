namespace HRM.Application.Features.Warehouse.Services;

/// <summary>Preserves legacy free-text voucher statuses while normalizing a safe filter value.</summary>
public static class WarehouseVoucherStatusRules
{
    public static bool TryNormalizeFilter(string? status, out string? normalizedStatus)
    {
        normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        return normalizedStatus is null || normalizedStatus.Length <= 100;
    }
}
