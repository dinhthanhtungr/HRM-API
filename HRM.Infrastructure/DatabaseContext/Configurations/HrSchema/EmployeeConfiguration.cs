using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> entity)
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__Employee__7AD04FF1C1895B9F");
            entity.ToTable("Employees", "hr");

            entity.HasIndex(e => e.PartId).HasDatabaseName("IX_Employees_PartID");
            entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_Employees_CompanyId");

            // MfgProductionOrder has no CheckedBy FK. Without this, EF creates a shadow EmployeeId column.
            entity.Ignore(e => e.MfgProductionOrderCheckedByNavigations);

            entity.Property(e => e.EmployeeId)
                  .HasColumnName("EmployeeID")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Address).HasColumnType("citext");
            entity.Property(e => e.Email).HasColumnType("citext");
            entity.Property(e => e.ExternalId).HasColumnType("citext");
            entity.Property(e => e.FullName).HasColumnType("citext");
            entity.Property(e => e.Gender).HasColumnType("citext");
            entity.Property(e => e.Identifier).HasColumnType("citext");
            entity.Property(e => e.PhoneNumber).HasColumnType("citext");
            entity.Property(e => e.Status).HasColumnType("citext");

            entity.Property(e => e.LevelId).HasColumnName("LevelID");
            entity.Property(e => e.PartId).HasColumnName("PartID");     // NÊN để nullable nếu dùng SET NULL
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId");

            entity.Property(e => e.CreatedDate).HasColumnName("created_date");

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(e => e.CreatedByNavigation)
                .WithMany(e => e.CreatedEmployees)
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UpdatedByNavigation)
                .WithMany(e => e.UpdatedEmployees)
                .HasForeignKey(e => e.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Part bị xoá -> SET NULL cho Employee.PartId (khuyến nghị)
            entity.HasOne(d => d.Part).WithMany(p => p.Employees)
                  .HasForeignKey(d => d.PartId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Employees_Parts");

            // Không xoá dây chuyền theo Company
            entity.HasOne(d => d.Company).WithMany(p => p.Employees)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Employees_Companies");
        }
    }
}
