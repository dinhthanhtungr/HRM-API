using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class PicklistDraft
    {
        public long DraftId { get; set; }
        public Guid CompanyId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public short Kind { get; set; }
        public int? ReqType { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? FrozenAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public Guid? ReceivedBy { get; set; }
        public int? RoundNo { get; set; }
        public DateTime? ExportedAt { get; set; }
        public Guid? ExportedBy { get; set; }

        public virtual ICollection<PicklistDraftLine> PicklistDraftLines { get; set; } = new List<PicklistDraftLine>();
    }
}
