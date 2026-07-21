using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkPlanConfiguration : IEntityTypeConfiguration<WorkPlan>
{
    public void Configure(EntityTypeBuilder<WorkPlan> entity)
    {
        entity.ToTable("WorkPlans", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkPlans_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.PlanName).HasColumnName("PlanName").HasColumnType("citext").IsRequired();
        entity.Property(x => x.Objective).HasColumnName("Objective").HasColumnType("text");
        entity.Property(x => x.Strategy).HasColumnName("Strategy").HasColumnType("text");
        entity.Property(x => x.DiscussionSummary).HasColumnName("DiscussionSummary").HasColumnType("text");
        entity.Property(x => x.NextAction).HasColumnName("NextAction").HasColumnType("text");
        entity.Property(x => x.Status).HasColumnName("Status").HasConversion<int>().HasDefaultValue(WorkPlanStatus.Active);
        entity.Property(x => x.Priority).HasColumnName("Priority").HasConversion<int>().HasDefaultValue(WorkTaskPriority.Normal);
        entity.Property(x => x.StartDate).HasColumnName("StartDate");
        entity.Property(x => x.EndDate).HasColumnName("EndDate");
        entity.Property(x => x.NextFollowUpDate).HasColumnName("NextFollowUpDate");
        entity.Property(x => x.AssignedToEmployeeId).HasColumnName("AssignedToEmployeeId");
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");
        entity.Property(x => x.UpdatedDate).HasColumnName("UpdatedDate");
        entity.Property(x => x.UpdatedBy).HasColumnName("UpdatedBy");
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.HasIndex(x => new { x.CompanyId, x.AssignedToEmployeeId, x.Status, x.NextFollowUpDate })
            .HasDatabaseName("IX_WorkPlans_Assigned_Status_NextFollowUp");
        entity.HasIndex(x => new { x.CompanyId, x.Status, x.NextFollowUpDate })
            .HasDatabaseName("IX_WorkPlans_Company_Status_NextFollowUp");
        entity.HasOne(x => x.Company).WithMany(x => x.WorkPlans).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkPlans_Company");
        entity.HasOne(x => x.AssignedToEmployee).WithMany(x => x.WorkPlanAssignedToEmployeeNavigations)
            .HasForeignKey(x => x.AssignedToEmployeeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkPlans_AssignedToEmployee");
        entity.HasOne(x => x.CreatedByNavigation).WithMany(x => x.WorkPlanCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkPlans_CreatedBy");
        entity.HasOne(x => x.UpdatedByNavigation).WithMany(x => x.WorkPlanUpdatedByNavigations)
            .HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkPlans_UpdatedBy");
    }
}
