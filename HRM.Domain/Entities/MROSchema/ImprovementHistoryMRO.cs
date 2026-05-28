using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class ImprovementHistoryMRO
    {
        public long HistoryId { get; set; }                 // PK (bigint identity)
        public long ProposalId { get; set; }                 // FK -> mro.improvement_hdr.proposal_id
        public string ProposalExternal { get; set; } = string.Empty; // text (mã ngoài của proposal)

        public string? Action { get; set; }                 // text
        public string? Summary { get; set; }                 // text
        public decimal? Minutes { get; set; }                 // numeric
        public string? Note { get; set; }                 // text
        public string? WoRef { get; set; }                 // text (work order ref)

        public DateTime? PerformedAt { get; set; }                 // datetime (UTC)
        public Guid? PerformedBy { get; set; }                 // uuid (FK -> Employees)

        // Navigations (tuỳ bạn có muốn 2 chiều không)
        //public virtual ImprovementHdrMRO? Proposal { get; set; }
        public virtual Employee? PerformedByNav { get; set; }
    }
}
