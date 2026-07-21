using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services
{
    internal static class ExecutivePnLAmountResolvers
    {
        public static decimal ResolveSalesRevenueUnitPrice(decimal unitPriceAgreed)
        {
            return unitPriceAgreed;
        }

        public static decimal ResolveSalesDeductionAmount()
        {
            // Chưa có nguồn dữ liệu giảm trừ doanh thu.
            // Khi bổ sung credit note/return/rebate, gắn vào đây.
            return 0m;
        }

        public static decimal ResolveManufacturingUnitCost(Formula formula)
        {
            // Legacy helper: returns the manufacturing formula total price snapshot.
            // Current Executive PnL report uses MerchandiseOrderDetail.BaseCostSnapshot directly,
            // so this overload stays only for older call sites or future formula-based cost logic.
            return formula.TotalPrice;
        }

        public static decimal ResolveManufacturingUnitCost(decimal totalPrice)
        {
            // Formula-based P&L readers pass the resolved manufacturing unit cost through this hook.
            return totalPrice;
        }
    }
}

