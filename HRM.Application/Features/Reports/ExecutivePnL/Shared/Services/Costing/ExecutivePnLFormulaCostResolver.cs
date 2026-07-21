namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing
{
    /// <summary>
    /// Hỗ trợ đọc danh sách lot và lấy đơn giá vốn công thức sản xuất dùng cho Executive PnL.
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
        /// Tách chuỗi LotNoList thành danh sách mã lot riêng biệt, bỏ khoảng trắng và loại trùng không phân biệt hoa thường.
        /// </summary>
        public static IEnumerable<string> SplitLotCodes(string? lotNoList)
        {
            if (string.IsNullOrWhiteSpace(lotNoList))
            {
                return Array.Empty<string>();
            }

            return lotNoList
                .Split(new[] { ',', ';', '|', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
