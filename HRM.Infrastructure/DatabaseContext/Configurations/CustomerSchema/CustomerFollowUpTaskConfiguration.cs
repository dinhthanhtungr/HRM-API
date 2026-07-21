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
    public class CustomerFollowUpTaskConfiguration : IEntityTypeConfiguration<CustomerFollowUpTask>
    {
        public void Configure(EntityTypeBuilder<CustomerFollowUpTask> entity)
        {
            entity.HasKey(e => e.Id).HasName("PK_CustomerFollowUpTasks_Id");
            entity.ToTable("CustomerFollowUpTasks", "Customer");

            entity.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(e => e.CustomerInteractionId).HasColumnName("CustomerInteractionId");
            entity.Property(e => e.Title).HasColumnName("Title").HasColumnType("citext").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasColumnType("text");
            entity.Property(e => e.NextAction).HasColumnName("NextAction").HasColumnType("text");
            entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>().HasDefaultValue(CustomerFollowUpTaskStatus.Pending);
            entity.Property(e => e.Priority).HasColumnName("Priority").HasConversion<int>().HasDefaultValue(CustomerFollowUpPriority.Normal);
            entity.Property(e => e.DueDate).HasColumnName("DueDate");
            entity.Property(e => e.CompletedDate).HasColumnName("CompletedDate");
            entity.Property(e => e.AssignedSaleEmployeeId).HasColumnName("AssignedSaleEmployeeId");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId").IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CompletedBy)
                  .HasColumnName("CompletedBy");

            entity.Property(e => e.CompletionNote)
                  .HasColumnName("CompletionNote")
                  .HasColumnType("text");
            entity.HasIndex(e => new { e.CompanyId, e.AssignedSaleEmployeeId, e.Status, e.DueDate })
                  .HasDatabaseName("IX_CustomerFollowUpTasks_Assigned_Status_DueDate");

            entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.DueDate })
                  .HasDatabaseName("IX_CustomerFollowUpTasks_Customer_DueDate");

            entity.HasOne(e => e.Customer).WithMany(c => c.CustomerFollowUpTasks)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerFollowUpTasks_Customer");

            entity.HasOne(e => e.CustomerInteraction).WithMany()
                  .HasForeignKey(e => e.CustomerInteractionId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerFollowUpTasks_Interaction");

            entity.HasOne(e => e.CompletedByNavigation)
                  .WithMany()
                  .HasForeignKey(e => e.CompletedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerFollowUpTasks_CompletedBy");

            entity.HasOne(e => e.Company).WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerFollowUpTasks_Company");

            entity.HasOne(e => e.AssignedSaleEmployee).WithMany()
                  .HasForeignKey(e => e.AssignedSaleEmployeeId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerFollowUpTasks_AssignedSaleEmployee");

            entity.HasOne(e => e.CreatedByNavigation).WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerFollowUpTasks_CreatedBy");

            entity.HasOne(e => e.UpdatedByNavigation).WithMany()
                  .HasForeignKey(e => e.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerFollowUpTasks_UpdatedBy");
        }
    }
}
