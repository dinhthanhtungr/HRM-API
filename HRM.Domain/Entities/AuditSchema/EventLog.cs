using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Logs;

namespace HRM.Domain.Entities.AuditSchema
{
    public class EventLog
    {
        // PK
        public Guid EventId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid DepartmentId { get; set; }

        // Nguồn gốc log
        public Guid SourceId { get; set; } // ID gốc được ghi log
        public string SourceCode { get; set; } = string.Empty; // ExternalId / Code gốc


        // Nội dung log
        public EventType EventType { get; set; } // Loại log (int)
        public string Status { get; set; } = string.Empty; // Trạng thái sự kiện


        // Soft delete
        public bool IsActive { get; set; } = true; // Lệnh xoá mềm

        public string? Note { get; set; } // Ghi chú thêm


        // Audit fields
        public Guid EmployeeID { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Part? Part { get; set; }
    }
}
