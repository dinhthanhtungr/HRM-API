using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseStockFifoLayer
    {
        public long LayerId { get; set; }
        public Guid CompanyId { get; set; }
        public int? ShelfStockId { get; set; }
        public int SlotId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }
        public int? StockType { get; set; }
        public long InLedgerId { get; set; }
        public DateTime InAt { get; set; }
        public decimal QtyInKg { get; set; }
        public decimal QtyUsedKg { get; set; }
        public decimal? RemainingKg { get; set; }
        public string UnitName { get; set; } = string.Empty;
    }
}
