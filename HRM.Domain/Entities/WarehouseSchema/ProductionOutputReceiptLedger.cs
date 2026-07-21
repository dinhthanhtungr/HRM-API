using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class ProductionOutputReceiptLedger
    {
        public long LedgerId { get; set; }
        public Guid CompanyId { get; set; }
        public long ProductionOutputId { get; set; }
        public string? RequestCode { get; set; }
        public long? RequestDetailId { get; set; }
        public long? VoucherId { get; set; }
        public long? VoucherDetailId { get; set; }
        public string? ProductCode { get; set; }
        public string? LotNumber { get; set; }
        public int? ItemStockType { get; set; }
        public decimal QtyKg { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? UnitName { get; set; }
        public DateOnly? ExpiryDate { get; set; }

        public virtual ProductionOutputReceiptSource ProductionOutput { get; set; } = null!;
    }
}
