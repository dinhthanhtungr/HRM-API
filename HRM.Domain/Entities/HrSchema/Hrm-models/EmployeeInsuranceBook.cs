using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeInsuranceBook
    {
        public Guid EmployeeInsuranceBookId { get; set; } = Guid.CreateVersion7();

        public Guid EmployeeId { get; set; }

        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? JobTitleSnapshot { get; set; }
        public string? SocialInsuranceNumberSnapshot { get; set; }

        public bool HasBookCover { get; set; }
        public bool HasDetachedLeaf { get; set; }

        public int? DetachedLeafCount { get; set; }
        public DateOnly? DetachedLeafFromDate { get; set; }
        public DateOnly? DetachedLeafToDate { get; set; }

        public string? Note { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
