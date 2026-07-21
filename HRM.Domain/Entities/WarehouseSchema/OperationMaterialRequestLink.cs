using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class OperationMaterialRequestLink
    {
        public long LinkId { get; set; }
        public Guid CompanyId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public long? RequestDetailId { get; set; }
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
        public decimal RequestedQtyKg { get; set; }
        public int? ItemStockType { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public int? RequestId { get; set; }
        public string? UnitName { get; set; }
    }
}
