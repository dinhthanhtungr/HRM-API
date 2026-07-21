using HRM.Application.Commons.Reporting;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Rules
{
    /// <summary>
    /// Tập hợp các quy tắc lọc dữ liệu dùng chung cho báo cáo Executive PnL.
    /// </summary>
    internal static class ExecutivePnLReportRules
    {
        /// <summary>
        /// Mã khách hàng nội bộ cần loại khỏi doanh thu và các báo cáo phân tích.
        /// </summary>
        public const string InternalCustomerExternalId = DeliveryRevenueQuery.InternalCustomerExternalId;

        /// <summary>
        /// Kiểm tra trạng thái chứng từ có phải đã hủy không, hỗ trợ cả hai cách ghi Cancelled và Canceled.
        /// </summary>
        public static bool IsCancelledStatus(string? status)
        {
            return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
        }
    }
}
