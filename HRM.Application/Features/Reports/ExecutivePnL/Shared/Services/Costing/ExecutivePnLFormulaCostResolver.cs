namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing
{
    using HRM.Application.Commons.Deliveries;

    /// <summary>
    /// Fallback cho dữ liệu Delivery Order lịch sử chưa có lot consumption/cost snapshot.
    /// </summary>
    internal static class ExecutivePnLFormulaCostResolver
    {
        /// <summary>
        /// Tìm cost của lot đầu tiên có trong bản đồ formula cost; trả về 0 nếu không tìm thấy lot hợp lệ.
        /// </summary>
        public static decimal ResolveUnitCost(
            string? lotNoList,
            IReadOnlyDictionary<string, decimal> formulaCostMap)
        {
            foreach (var lotCode in SplitLotCodes(lotNoList))
            {
                if (formulaCostMap.TryGetValue(lotCode, out var formulaCost))
                {
                    return formulaCost;
                }
            }

            return 0m;
        }

        /// <summary>
        /// Tách LotNoList legacy thành danh sách mã lot, bỏ khoảng trắng và loại trùng không phân biệt hoa thường.
        /// </summary>
        public static IEnumerable<string> SplitLotCodes(string? lotNoList)
        {
            return DeliveryOrderLotReadRules.SplitLegacy(lotNoList);
        }
    }
}
