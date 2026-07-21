namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Currency
{
    /// <summary>
    /// Chuyển đổi số tiền của báo cáo Executive PnL về đơn vị VND theo tỷ giá trên chứng từ.
    /// </summary>
    internal static class ExecutivePnLCurrencyConverter
    {
        /// <summary>
        /// Trả về số tiền VND; nếu tiền tệ đã là VND hoặc thiếu tỷ giá hợp lệ thì giữ nguyên số tiền gốc.
        /// </summary>
        public static decimal ToVnd(decimal amount, string? currency, decimal? exchangeRate)
        {
            if (amount == 0)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(currency) ||
                currency.Equals("VND", StringComparison.OrdinalIgnoreCase))
            {
                return amount;
            }

            return exchangeRate.HasValue && exchangeRate.Value > 0
                ? amount * exchangeRate.Value
                : amount;
        }
    }
}

