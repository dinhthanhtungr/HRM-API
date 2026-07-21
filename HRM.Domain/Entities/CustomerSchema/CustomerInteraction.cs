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
    public class CustomerInteraction
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? ContactId { get; set; }

        public CustomerInteractionType InteractionType { get; set; } = CustomerInteractionType.Call;

        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Outcome { get; set; }
        public string? NextAction { get; set; }

        public DateTime InteractionAt { get; set; } = DateTime.Now;
        public DateTime? NextFollowUpDate { get; set; }

        public Guid? AssignedSaleEmployeeId { get; set; }

        public Guid CompanyId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual Customer Customer { get; set; } = null!;
        public virtual Contact? Contact { get; set; }
        public virtual Company Company { get; set; } = null!;
        public virtual Employee? AssignedSaleEmployee { get; set; }
        public virtual Employee CreatedByNavigation { get; set; } = null!;
        public virtual Employee? UpdatedByNavigation { get; set; }
    }
}
