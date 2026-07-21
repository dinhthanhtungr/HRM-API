using System;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class ImportRequestFlowLog
    {
        public long FlowLogId { get; set; }
        public Guid CompanyId { get; set; }
        public int? RequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public long? RequestDetailId { get; set; }
        public int LineNo { get; set; }
        public int? ReqType { get; set; }
        public string? RequestName { get; set; }
        public string FlowType { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? LotNumber { get; set; }
        public decimal QtyKg { get; set; }
        public int BagNumber { get; set; }
        public int? StockType { get; set; }
        public long? SourceRowId { get; set; }
        public string? SourceTable { get; set; }
        public string? SourceRequestCode { get; set; }
        public decimal? SourceAvailableQtyKg { get; set; }
        public string? MfgExternalId { get; set; }
        public string? VaExternalId { get; set; }
        public string? RefCode { get; set; }
        public string? GroupType { get; set; }
        public string? Shift { get; set; }
        public string? RoleName { get; set; }
        public string LogStatus { get; set; } = string.Empty;
        public decimal AppliedQtyKg { get; set; }
        public int AppliedBagNumber { get; set; }
        public long? AppliedVoucherId { get; set; }
        public string? AppliedVoucherCode { get; set; }
        public DateTime? AppliedAt { get; set; }
        public Guid? AppliedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public Guid? ConfirmedBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public string? Note { get; set; }
        public string? UnitName { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
}
