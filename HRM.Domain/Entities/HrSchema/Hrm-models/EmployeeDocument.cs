using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Employees;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeDocument
    {
        public Guid EmployeeDocumentId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public EmployeeDocumentType DocumentType { get; set; } = default!;
        public string? DocumentName { get; set; }
        public EmployeeDocumentStatus Status { get; set; } = default!;
        public string? Note { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
