using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Audits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.AuditSchema
{
    public class AuditLog
    {
        public Guid AuditLogId { get; set; } = Guid.CreateVersion7();

        public Guid? CompanyId { get; set; }

        public string SchemaName { get; set; } = default!;

        public string TableName { get; set; } = default!;

        public Guid RecordId { get; set; }

        public AuditActionType ActionType { get; set; }
        // Create, Update, Delete, Restore

        public Guid? ChangedBy { get; set; }

        public DateTime ChangedAt { get; set; }

        public string? OldValues { get; set; }
        // jsonb

        public string? NewValues { get; set; }
        // jsonb

        public string? ChangedValues { get; set; }
        // jsonb: chỉ field bị thay đổi

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? Reason { get; set; }

        public Guid? CorrelationId { get; set; }

        public virtual Company? Company { get; set; }
        public virtual Employee? ChangedByNavigation { get; set; }

    }
}
