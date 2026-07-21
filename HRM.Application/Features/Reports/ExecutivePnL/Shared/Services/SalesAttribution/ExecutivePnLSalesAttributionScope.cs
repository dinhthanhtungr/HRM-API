namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.SalesAttribution
{
    /// <summary>
    /// Chứa kết quả phân bổ khách hàng về sale và group sale hợp lệ cho các báo cáo Executive PnL.
    /// </summary>
    internal sealed class ExecutivePnLSalesAttributionScope
    {
        private readonly IReadOnlyDictionary<Guid, ExecutivePnLSalesAttribution> _attributionsByCustomer;

        public ExecutivePnLSalesAttributionScope(
            IReadOnlyDictionary<Guid, ExecutivePnLSalesAttribution> attributionsByCustomer)
        {
            _attributionsByCustomer = attributionsByCustomer;
        }

        /// <summary>
        /// Lấy thông tin sale/group chịu trách nhiệm cho customer; trả về false nếu customer không thuộc scope báo cáo.
        /// </summary>
        public bool TryGetAttribution(Guid customerId, out ExecutivePnLSalesAttribution attribution)
        {
            return _attributionsByCustomer.TryGetValue(customerId, out attribution!);
        }
    }

    /// <summary>
    /// Mô tả một dòng phân bổ customer về sale và group sale dùng khi tổng hợp doanh thu, giá vốn và lợi nhuận.
    /// </summary>
    internal sealed class ExecutivePnLSalesAttribution
    {
        public Guid CustomerId { get; init; }
        public Guid SaleId { get; init; }
        public string SaleLabel { get; init; } = string.Empty;
        public string GroupKey { get; init; } = string.Empty;
        public string GroupLabel { get; init; } = string.Empty;
    }
}
