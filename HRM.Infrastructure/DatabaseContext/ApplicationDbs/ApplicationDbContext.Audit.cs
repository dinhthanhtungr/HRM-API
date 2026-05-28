using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<EventLog> EventLogs { get; set; } = default!;
        public virtual DbSet<CodeCounter> CodeCounters { get; set; } = default!;
        public virtual DbSet<AuditLog> AuditLogs { get; set; } = default!;
    }
}
