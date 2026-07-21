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
    public class CustomerFollowUpTask
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? CustomerInteractionId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? NextAction { get; set; }

        public CustomerFollowUpTaskStatus Status { get; set; } = CustomerFollowUpTaskStatus.Pending;
        public CustomerFollowUpPriority Priority { get; set; } = CustomerFollowUpPriority.Normal;

        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public Guid? CompletedBy { get; set; }
        public string? CompletionNote { get; set; }

        public Guid? AssignedSaleEmployeeId { get; set; }

        public Guid CompanyId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual Customer Customer { get; set; } = null!;
        public virtual CustomerInteraction? CustomerInteraction { get; set; }
        public virtual Company Company { get; set; } = null!;
        public virtual Employee? CompletedByNavigation { get; set; }
        public virtual Employee? AssignedSaleEmployee { get; set; }
        public virtual Employee CreatedByNavigation { get; set; } = null!;
        public virtual Employee? UpdatedByNavigation { get; set; }
    }
}
