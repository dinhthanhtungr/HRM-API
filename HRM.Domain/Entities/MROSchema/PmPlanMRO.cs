using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class PmPlanMRO
    {
        public long PlanId { get; set; }                    // bigint identity (PK)
        public string PlanExternalId { get; set; } = "";      // text
        public string? EquipmentExternalId { get; set; }      // text
        public string? DepartmentExternalId { get; set; }     // text
        public string? FactoryExternalId { get; set; }        // text

        public string? Notes { get; set; }                    // text
        public int IntervalVal { get; set; }              // int
        public string IntervalUnit { get; set; } = "day";    // text: day/week/month/year/hour/shift...
        public DateTime AnchorDate { get; set; }              // datetime (mốc bắt đầu)
        public string Status { get; set; } = "Draft";        // text
        public DateTime CreatedAt { get; set; }               // datetime (UTC)
        public Guid CreatedBy { get; set; }                // uuid
    }
}
