using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class ProductionOutputReceiptSource
    {
        public long ProductionOutputId { get; set; }
        public Guid CompanyId { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public long? ShiftReportForAllId { get; set; }
        public long? ShiftReportDetailForAllId { get; set; }
        public string? ProductionReportCode { get; set; }
        public Guid? MfgProductionOrderId { get; set; }
        public string? MfgExternalId { get; set; }
        public string? VaExternalId { get; set; }
        public int? OperationScheduleIdpk { get; set; }
        public int LineNo { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? LotNumber { get; set; }
        public int ItemStockType { get; set; }
        public bool IsOpenEnded { get; set; }
        public decimal? PlannedQtyKg { get; set; }
        public decimal ProducedQtyKg { get; set; }
        public decimal ImportedQtyKg { get; set; }
        public decimal? RemainingQtyKg { get; set; }
        public int ReceiptCount { get; set; }
        public decimal? GoodQtyKg { get; set; }
        public decimal? DefectQtyKg { get; set; }
        public decimal? ScrapQtyKg { get; set; }
        public string LinkStatus { get; set; } = string.Empty;
        public string? MaterialIssueRequestCode { get; set; }
        public DateTime? MaterialIssueFoundAt { get; set; }
        public string CostStatus { get; set; } = string.Empty;
        public string? UnlinkedReason { get; set; }
        public Guid? OverrideBy { get; set; }
        public DateTime? OverrideAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public string? Note { get; set; }
        public string? UnitName { get; set; }
        public DateOnly? ExpiryDate { get; set; }

        public virtual ICollection<ProductionOutputReceiptLedger> ProductionOutputReceiptLedgers { get; set; } = new List<ProductionOutputReceiptLedger>();
    }
}
