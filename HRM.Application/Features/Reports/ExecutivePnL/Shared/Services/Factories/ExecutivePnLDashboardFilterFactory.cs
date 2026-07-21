using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByProductType;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Factories
{
    /// <summary>
    /// Chuẩn hóa query params thành filter nội bộ dùng cho các reader của Executive PnL dashboard.
    /// </summary>
    internal static class ExecutivePnLDashboardFilterFactory
    {
        /// <summary>
        /// Tạo filter chung cho dashboard/report, đồng thời chuẩn hóa khoảng tháng bằng ExecutivePnLPeriod.
        /// </summary>
        public static ExecutivePnLFilter Create(
            Guid? companyId,
            string? businessUnit,
            string? currency,
            DateTime? fromMonth,
            DateTime? toMonth)
        {
            var period = ExecutivePnLPeriod.Create(fromMonth, toMonth);

            return new ExecutivePnLFilter
            {
                CompanyId = companyId,
                BusinessUnit = businessUnit,
                Currency = currency,
                FromMonth = period.FromMonth,
                ToMonth = period.ToMonth
            };
        }

        /// <summary>
        /// Tạo filter riêng cho tab theo sale, có thêm điều kiện lọc sale group và sale person.
        /// </summary>
        public static ExecutivePnLSaleFilter CreateSaleFilter(
            Guid? companyId,
            string? businessUnit,
            string? currency,
            DateTime? fromMonth,
            DateTime? toMonth,
            Guid? saleGroup,
            Guid? salePerson)
        {
            var period = ExecutivePnLPeriod.Create(fromMonth, toMonth);

            return new ExecutivePnLSaleFilter
            {
                CompanyId = companyId,
                BusinessUnit = businessUnit,
                Currency = currency,
                FromMonth = period.FromMonth,
                ToMonth = period.ToMonth,
                SaleGroup = saleGroup,
                SalePerson = salePerson
            };
        }

        /// <summary>
        /// Tạo filter riêng cho tab theo producType, có thêm điều kiện lọc sale group và sale person.
        /// </summary>
        public static ExecutivePnLProductTypeFilter CreateProductTypeFilter(
            Guid? companyId,
            string? businessUnit,
            string? currency,
            DateTime? fromMonth,
            DateTime? toMonth)
        {
            var period = ExecutivePnLPeriod.Create(fromMonth, toMonth);

            return new ExecutivePnLProductTypeFilter
            {
                CompanyId = companyId,
                BusinessUnit = businessUnit,
                Currency = currency,
                FromMonth = period.FromMonth,
                ToMonth = period.ToMonth
            };
        }
    }
}
