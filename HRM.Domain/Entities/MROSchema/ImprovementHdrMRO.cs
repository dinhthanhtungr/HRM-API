using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class ImprovementHdrMRO
    {
        public long ProposalId { get; set; }                 // bigint (PK)
        public string ProposalExternal { get; set; } = string.Empty; // text (mã ngoài)
        public string Title { get; set; } = string.Empty; // text
        public string? Description { get; set; }                 // text

        public string? AreaExternalId { get; set; }                 // text
        public string? EquipmentExternalId { get; set; }               // text
        public string? FactoryExternalId { get; set; }                 // text

        public string Status { get; set; } = "Draft";      // text: Draft/Approved/InProgress/Done/Closed/Cancelled

        public DateTime CreatedAt { get; set; }                 // timestamptz (UTC)
        public Guid CreatedBy { get; set; }                 // uuid

        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }

        public DateTime? StartedAt { get; set; }
        public Guid? StartedBy { get; set; }

        public DateTime? DoneAt { get; set; }
        public Guid? DoneBy { get; set; }

        public DateTime? ClosedAt { get; set; }
        public Guid? ClosedBy { get; set; }

        // Navigations (tuỳ bạn có muốn FK sang Employees không)
        public virtual Employee? CreatedByNav { get; set; }
        public virtual Employee? ApprovedByNav { get; set; }
        public virtual Employee? StartedByNav { get; set; }
        public virtual Employee? DoneByNav { get; set; }
        public virtual Employee? ClosedByNav { get; set; }

        //public virtual ICollection<ImprovementHistoryMRO> ImprovementHistoryMROs { get; set; } = new List<ImprovementHistoryMRO>();

    }
}
