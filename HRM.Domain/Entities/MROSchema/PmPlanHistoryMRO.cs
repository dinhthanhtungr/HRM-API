using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class PmPlanHistoryMRO
    {
        public long HistId { get; set; }                  // bigint identity (PK)
        public long PlanId { get; set; }                  // FK -> pmplan.plan_id
        public string Action { get; set; } = "";            // text
        public string? Details { get; set; }                 // jsonb (lưu dạng string JSON)
        public Guid? PerformedBy { get; set; }             // uuid
        public DateTime? PerformedAt { get; set; }            // datetime
        public string? Note { get; set; }                    // text
        public string? WoRef { get; set; }                   // text
        public string PlanExternalId { get; set; } = "";    // text (snapshot)
        public decimal? MinutesSpent { get; set; }            // numeric
        // Navigations (tuỳ chọn)
        public virtual PmPlanMRO? Plan { get; set; }
        //public virtual Employee? PerformedByNav { get; set; }
    }
}
