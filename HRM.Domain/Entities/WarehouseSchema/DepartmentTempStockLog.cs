using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class DepartmentTempStockLog
    {
        public long LogId { get; set; }
        public Guid CompanyId { get; set; }
        public long? TempStockId { get; set; }
        public Guid? PartId { get; set; }
        public int? RequestId { get; set; }
        public string? RequestCode { get; set; }
        public long? VoucherId { get; set; }
        public long? VoucherDetailId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? LotNumber { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public decimal DeltaKg { get; set; }
        public decimal QtyAfter { get; set; }
        public decimal? AvgUnitCost { get; set; }
        public decimal? AmountCost { get; set; }
        public string? CostStatus { get; set; }
        public string? UncostedReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? Note { get; set; }
        public string? UnitName { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
}
