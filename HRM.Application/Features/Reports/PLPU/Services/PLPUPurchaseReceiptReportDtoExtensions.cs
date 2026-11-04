namespace HRM.Application.Features.Reports.PLPU;
    internal static class PLPUPurchaseReceiptReportDtoExtensions
    {
        public static string MaterialCode(this PLPUPurchaseReceiptReportDto row)
        {
            return row.ProductCode;
        }
    }
