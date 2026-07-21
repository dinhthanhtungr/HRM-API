using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class CustomerWorkPlanConfiguration : IEntityTypeConfiguration<CustomerWorkPlan>
    {
        public void Configure(EntityTypeBuilder<CustomerWorkPlan> entity)
        {
            entity.HasKey(e => e.Id).HasName("PK_CustomerWorkPlans_Id");
            entity.ToTable("CustomerWorkPlans", "Customer");

            entity.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(e => e.PlanName).HasColumnName("PlanName").HasColumnType("citext").IsRequired();
            entity.Property(e => e.Objective).HasColumnName("Objective").HasColumnType("text");
            entity.Property(e => e.Strategy).HasColumnName("Strategy").HasColumnType("text");
            entity.Property(e => e.DiscussionSummary).HasColumnName("DiscussionSummary").HasColumnType("text");
            entity.Property(e => e.NextAction).HasColumnName("NextAction").HasColumnType("text");
            entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>().HasDefaultValue(CustomerWorkPlanStatus.Active);
            entity.Property(e => e.Priority).HasColumnName("Priority").HasConversion<int>().HasDefaultValue(CustomerFollowUpPriority.Normal);
            entity.Property(e => e.StartDate).HasColumnName("StartDate");
            entity.Property(e => e.EndDate).HasColumnName("EndDate");
            entity.Property(e => e.NextFollowUpDate).HasColumnName("NextFollowUpDate");
            entity.Property(e => e.AssignedSaleEmployeeId).HasColumnName("AssignedSaleEmployeeId");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId").IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.Status, e.NextFollowUpDate })
                  .HasDatabaseName("IX_CustomerWorkPlans_Customer_Status_NextFollowUp");

            entity.HasIndex(e => new { e.CompanyId, e.AssignedSaleEmployeeId, e.Status, e.NextFollowUpDate })
                  .HasDatabaseName("IX_CustomerWorkPlans_Assigned_Status_NextFollowUp");

            entity.HasOne(e => e.Customer).WithMany(c => c.CustomerWorkPlans)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerWorkPlans_Customer");

            entity.HasOne(e => e.Company).WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerWorkPlans_Company");

            entity.HasOne(e => e.AssignedSaleEmployee).WithMany()
                  .HasForeignKey(e => e.AssignedSaleEmployeeId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerWorkPlans_AssignedSaleEmployee");

            entity.HasOne(e => e.CreatedByNavigation).WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerWorkPlans_CreatedBy");

            entity.HasOne(e => e.UpdatedByNavigation).WithMany()
                  .HasForeignKey(e => e.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerWorkPlans_UpdatedBy");
        }
    }
}
