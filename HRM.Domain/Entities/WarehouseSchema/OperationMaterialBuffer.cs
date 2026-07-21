using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class OperationMaterialBuffer
    {
        public long BufferId { get; set; }
        public Guid CompanyId { get; set; }
        public string? RequestCode { get; set; }
        public long? VoucherId { get; set; }
        public long? VoucherDetailId { get; set; }
        public int? OperationScheduleIdpk { get; set; }
        public Guid? MfgProductionOrderId { get; set; }
        public string? MfgExternalId { get; set; }
        public string? VaExternalId { get; set; }
        public int? OperationNo { get; set; }
        public string? OperationName { get; set; }
        public string? MachineId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? LotNumber { get; set; }
        public int? ItemStockType { get; set; }
        public decimal IssuedQtyKg { get; set; }
        public decimal UsedQtyKg { get; set; }
        public decimal ReturnedQtyKg { get; set; }
        public decimal? RemainingQtyKg { get; set; }
        public decimal? AvgUnitCost { get; set; }
        public decimal? AmountCost { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? Note { get; set; }
        public string CostStatus { get; set; } = string.Empty;
        public string? UncostedReason { get; set; }
        public DateTime? CostedAt { get; set; }
        public Guid? CostedBy { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }
    }
}
