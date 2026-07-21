using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class CustomerTransferLogConfiguration : IEntityTypeConfiguration<CustomerTransferLog>
    {
        public void Configure(EntityTypeBuilder<CustomerTransferLog> entity)
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC276977D605");
            entity.ToTable("CustomerTransferLog", "Customer");

            entity.Property(e => e.Id)
                  .HasColumnName("Id")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.FromEmployeeId).HasColumnName("FromEmployeeId");
            entity.Property(e => e.ToEmployeeId).HasColumnName("ToEmployeeId");
            entity.Property(e => e.FromGroupId).HasColumnName("FromGroupId");
            entity.Property(e => e.ToGroupId).HasColumnName("ToGroupId");
            entity.Property(e => e.Note).HasColumnName("Note").HasColumnType("text");
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId");

            entity.HasIndex(e => new { e.CompanyId, e.CreatedDate, e.Id })
                  .IsDescending(false, true, true)
                  .HasDatabaseName("IX_CustomerTransferLog_Company_CreatedDateDesc");

            entity.HasIndex(e => new { e.CompanyId, e.FromEmployeeId, e.CreatedDate, e.Id })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("IX_CustomerTransferLog_Company_FromEmp_CreatedDateDesc");

            entity.HasIndex(e => new { e.CompanyId, e.ToEmployeeId, e.CreatedDate, e.Id })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("IX_CustomerTransferLog_Company_ToEmp_CreatedDateDesc");

            entity.HasOne(d => d.Company).WithMany(p => p.CustomerTransferLogs)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_Company");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerTransferLogCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_CreatedBy");

            entity.HasOne(d => d.FromEmployee).WithMany(p => p.CustomerTransferLogFromEmployees)
                  .HasForeignKey(d => d.FromEmployeeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_FromEmployee");

            entity.HasOne(d => d.ToEmployee).WithMany(p => p.CustomerTransferLogToEmployees)
                  .HasForeignKey(d => d.ToEmployeeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_ToEmployee");

            entity.HasOne(d => d.FromGroup).WithMany(p => p.CustomerTransferLogFromGroups)
                  .HasForeignKey(d => d.FromGroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_FromGroup");

            entity.HasOne(d => d.ToGroup).WithMany(p => p.CustomerTransferLogToGroups)
                  .HasForeignKey(d => d.ToGroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerTransferLog_ToGroup");
        }
    }
}
