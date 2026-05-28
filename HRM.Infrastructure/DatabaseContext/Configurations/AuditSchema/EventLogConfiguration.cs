using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AuditSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.AuditSchema
{
    public class EventLogConfiguration : IEntityTypeConfiguration<EventLog>
    {
        public void Configure(EntityTypeBuilder<EventLog> entity)
        {
            entity.HasKey(e => e.EventId)
                  .HasName("PK__EventLogs__227429A55C6F1195");

            entity.ToTable("EventLogs", "Audit");

            entity.Property(e => e.EventId)
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.CompanyId, "IX_EventLogs_CompanyId");
            entity.HasIndex(e => e.EmployeeID, "IX_EventLogs_CreatedBy");

            entity.HasIndex(x => x.SourceId).HasDatabaseName("IX_EventLog_SourceId");
            entity.HasIndex(x => x.SourceCode).HasDatabaseName("IX_EventLog_SourceCode");
            entity.HasIndex(x => x.EventType).HasDatabaseName("IX_EventLog_EventType");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_EventLog_Status");

            // Composite cho các case lọc kết hợp
            entity.HasIndex(x => new { x.SourceId, x.EventType })
                  .HasDatabaseName("IX_EventLog_SourceId_EventType");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            // entity.Property(e => e.IsCustomerSelect).HasDefaultValue(false);
            entity.Property(e => e.Status)
                  .HasMaxLength(32)
                  .HasDefaultValue("Draft");

            entity.HasOne(d => d.Company)
                  .WithMany(p => p.EventLogs)
                  .HasForeignKey(d => d.CompanyId)
                  .HasConstraintName("FK_EventLogs_Company");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(p => p.EventLogs)
                  .HasForeignKey(d => d.EmployeeID)
                  .HasConstraintName("FK_EventLogs_CreatedBy");

            entity.HasOne(d => d.Part)
                  .WithMany(p => p.EventLogs)
                  .HasForeignKey(d => d.DepartmentId)
                  .HasConstraintName("FK_EventLogs_DepartmentId");

            // Nếu dự án của bạn dùng xmin:
            // entity.UseXminAsConcurrencyToken();
        }
    }
}
