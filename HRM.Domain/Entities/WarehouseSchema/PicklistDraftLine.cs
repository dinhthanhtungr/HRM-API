using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class PicklistDraftLine
    {
        public long LineId { get; set; }
        public long DraftId { get; set; }
        public int LineNo { get; set; }
        public int ShelfStockId { get; set; }
        public string? SlotCode { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? LotNumber { get; set; }
        public decimal QtyKg { get; set; }
        public int? Bags { get; set; }
        public decimal? StdBagKg { get; set; }
        public string? LotKey { get; set; }
        public int? RawLineNo { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }

        public virtual PicklistDraft Draft { get; set; } = null!;
    }
}
