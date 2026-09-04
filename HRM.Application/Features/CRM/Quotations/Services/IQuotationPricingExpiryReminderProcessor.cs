namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Quét báo giá nháp đang dùng giá chuẩn quá hạn rà soát và phát cảnh báo nội bộ một lần.
/// </summary>
public interface IQuotationPricingExpiryReminderProcessor
{
    Task<int> ProcessExpiredPricingAsync(CancellationToken cancellationToken = default);
}
