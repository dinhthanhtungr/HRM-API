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
    public class CustomerAssignmentConfiguration : IEntityTypeConfiguration<CustomerAssignment>
    {
        public void Configure(EntityTypeBuilder<CustomerAssignment> entity)
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC279397A842");
            entity.ToTable("CustomerAssignment", "Customer");

            entity.Property(e => e.Id)
                  .HasColumnName("Id")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeId");
            entity.Property(e => e.GroupId).HasColumnName("GroupId");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId");
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.EmployeeId, e.UpdatedDate, e.Id })
                  .IsDescending(false, false, false, true, true)
                  .HasDatabaseName("IX_CustAssign_Company_Active_Emp_UpdatedDesc");

            entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.GroupId, e.UpdatedDate, e.Id })
                  .IsDescending(false, false, false, true, true)
                  .HasDatabaseName("IX_CustAssign_Company_Active_Group_UpdatedDesc");

            entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.IsActive })
                  .HasDatabaseName("IX_CustAssign_Company_Customer_Active");

            entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.GroupId })
                  .IsUnique()
                  .HasFilter("\"IsActive\" = TRUE")
                  .HasDatabaseName("UX_CustAssign_Company_Customer_Group_Active");

            entity.HasOne(d => d.Group).WithMany(p => p.CustomerAssignments)
                  .HasForeignKey(d => d.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerAssignment_Group");

            entity.HasOne(d => d.Company).WithMany(p => p.CustomerAssignments)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerAssignment_Company");

            // XÓA CUSTOMER -> XÓA CustomerAssignment
            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerAssignments)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerAssignment_Customer");

            entity.HasOne(d => d.Employee).WithMany(p => p.CustomerAssignmentEmployees)
                  .HasForeignKey(d => d.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerAssignment_Employee");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerAssignmentCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerAssignment_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerAssignmentUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerAssignment_UpdatedBy");
        }
    }
}
