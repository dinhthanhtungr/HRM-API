using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class TempStockIssueLedger
    {
        public long LedgerId { get; set; }
        public Guid CompanyId { get; set; }
        public string SourceKind { get; set; } = string.Empty;
        public string SourceKey { get; set; } = string.Empty;
        public string? RequestCode { get; set; }
        public int? RequestDetailId { get; set; }
        public string VaCode { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string? LotNo { get; set; }
        public decimal QtyKg { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? UnitName { get; set; }
    }
}
