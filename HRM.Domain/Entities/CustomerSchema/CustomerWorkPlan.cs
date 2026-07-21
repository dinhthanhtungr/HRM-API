using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.CustomerSchema
{
    public class CustomerWorkPlan
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }

        public string PlanName { get; set; } = string.Empty;
        public string? Objective { get; set; }
        public string? Strategy { get; set; }
        public string? DiscussionSummary { get; set; }
        public string? NextAction { get; set; }

        public CustomerWorkPlanStatus Status { get; set; } = CustomerWorkPlanStatus.Active;
        public CustomerFollowUpPriority Priority { get; set; } = CustomerFollowUpPriority.Normal;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? NextFollowUpDate { get; set; }

        public Guid? AssignedSaleEmployeeId { get; set; }

        public Guid CompanyId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual Customer Customer { get; set; } = null!;
        public virtual Company Company { get; set; } = null!;
        public virtual Employee? AssignedSaleEmployee { get; set; }
        public virtual Employee CreatedByNavigation { get; set; } = null!;
        public virtual Employee? UpdatedByNavigation { get; set; }
    }
}
