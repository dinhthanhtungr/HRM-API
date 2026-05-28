using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.AuditSchema
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> entity)
        {
            entity.ToTable("AuditLogs", "Audit");

            entity.HasKey(e => e.AuditLogId)
                  .HasName("PK_AuditLogs");

            entity.Property(e => e.AuditLogId)
                  .ValueGeneratedNever();


            entity.Property(e => e.SchemaName)
                  .HasMaxLength(128)
                  .IsRequired();

            entity.Property(e => e.TableName)
                  .HasMaxLength(128)
                  .IsRequired();

            entity.Property(e => e.ActionType)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(e => e.ChangedAt)
                  .IsRequired();

            entity.Property(e => e.OldValues)
                  .HasColumnType("jsonb");

            entity.Property(e => e.NewValues)
                  .HasColumnType("jsonb");

            entity.Property(e => e.ChangedValues)
                  .HasColumnType("jsonb");

            entity.Property(e => e.IpAddress)
                  .HasMaxLength(64);

            entity.Property(e => e.UserAgent)
                  .HasMaxLength(512);

            entity.Property(e => e.Reason)
                  .HasMaxLength(500);

            entity.Property(e => e.CorrelationId);

            entity.HasIndex(e => e.CompanyId)
                  .HasDatabaseName("IX_AuditLogs_CompanyId");

            entity.HasIndex(e => e.ChangedBy)
                  .HasDatabaseName("IX_AuditLogs_ChangedBy");

            entity.HasIndex(e => e.ActionType)
                  .HasDatabaseName("IX_AuditLogs_ActionType");

            entity.HasIndex(e => new { e.SchemaName, e.TableName, e.RecordId, e.ChangedAt })
                  .HasDatabaseName("IX_AuditLogs_Entity_Record_ChangedAt");

            entity.HasIndex(e => new { e.CompanyId, e.ChangedAt })
                  .HasDatabaseName("IX_AuditLogs_Company_ChangedAt");

            entity.HasIndex(e => e.CorrelationId)
                  .HasDatabaseName("IX_AuditLogs_CorrelationId");

            entity.HasOne<Company>()
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_AuditLogs_Company");

            entity.HasOne<Employee>()
                  .WithMany(e => e.AuditLogs)
                  .HasForeignKey(e => e.ChangedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_AuditLogs_ChangedBy");
        }
    }
}
