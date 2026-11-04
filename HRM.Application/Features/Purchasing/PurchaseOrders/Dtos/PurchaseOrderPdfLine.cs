namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos
{
    /// <summary>Read-model riêng cho PDF, giữ null để tính fallback đúng mẫu gốc; không trả ra API JSON.</summary>
    public sealed class PurchaseOrderPdfLine
    {
        public Guid MaterialId { get; set; }
        public string? MaterialExternalIDSnapshot { get; set; }
        public string? MaterialNameSnapshot { get; set; }

        public string? Package { get; set; }

        public decimal? BaseCostSnapshot { get; set; }
        public DateTime? BaseDateSnapshot { get; set; }

        public decimal? RequestQuantity { get; set; }
        public decimal? UnitPriceAgreed { get; set; }

        public decimal? TotalUnitPriceAgreed { get; set; }
    }
}
