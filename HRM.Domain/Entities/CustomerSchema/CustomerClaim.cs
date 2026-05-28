using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema
{
    public class CustomerClaim
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid GroupId { get; set; }
        public ClaimType Type { get; set; } = ClaimType.Work;
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid CompanyId { get; set; }

        public virtual Customer Customer { get; set; } = null!;
        public virtual Employee Employee { get; set; } = null!;
        public virtual Group Group { get; set; } = null!;
        public virtual Company Company { get; set; } = null!;
    }
}
