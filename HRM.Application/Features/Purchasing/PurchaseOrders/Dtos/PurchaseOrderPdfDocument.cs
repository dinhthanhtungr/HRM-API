namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos
{
    /// <summary>Read-model riêng cho PDF, giữ null để tính fallback đúng mẫu gốc; không trả ra API JSON.</summary>
    public sealed class PurchaseOrderPdfDocument
    {
        public Guid PurchaseOrderSnapshotId { get; set; }

        public string PurchaseOrderExternalIdSnapshot { get; set; } = null!;

        public string? EmployeeExternalIdSnapshot { get; set; }

        public string? EmployeeFullNameSnapshot { get; set; }

        public string? PhoneNumberSnapshot { get; set; }

        public string? EmailSnapshot { get; set; }

        public string? SupplierExternalIdSnapshot { get; set; }
        public string? SupplierNameSnapshot { get; set; }
        public string? SupplierContactSnapshot { get; set; }
        public string? SupplierPhoneNumberSnapshot { get; set; }
        public string? SupplierAddressSnapshot { get; set; }

        public string? Note { get; set; }

        public decimal? TotalPrice { get; set; }

        public string? DeliveryAddress { get; set; }

        public string? PaymentTypes { get; set; }

        public int? Vat { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? RequestDeliveryDate { get; set; }

        public List<PurchaseOrderPdfLine> Details { get; set; } = new List<PurchaseOrderPdfLine>();
    }
}
