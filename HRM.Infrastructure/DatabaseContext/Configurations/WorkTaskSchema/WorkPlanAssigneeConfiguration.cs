using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkPlanAssigneeConfiguration : IEntityTypeConfiguration<WorkPlanAssignee>
{
    public void Configure(EntityTypeBuilder<WorkPlanAssignee> entity)
    {
        entity.ToTable("WorkPlanAssignees", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkPlanAssignees_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.WorkPlanId).HasColumnName("WorkPlanId").IsRequired();
        entity.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");
        entity.HasIndex(x => new { x.WorkPlanId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("UX_WorkPlanAssignees_Plan_Employee_Active").HasFilter("\"IsActive\" = true");
        entity.HasIndex(x => new { x.EmployeeId, x.IsActive }).HasDatabaseName("IX_WorkPlanAssignees_Employee_IsActive");
        entity.HasOne(x => x.WorkPlan).WithMany(x => x.Assignees).HasForeignKey(x => x.WorkPlanId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkPlanAssignees_WorkPlan");
        entity.HasOne(x => x.Employee).WithMany(x => x.WorkPlanAssigneeEmployees).HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkPlanAssignees_Employee");
        entity.HasOne(x => x.CreatedByNavigation).WithMany(x => x.WorkPlanAssigneeCreatedByNavigations).HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkPlanAssignees_CreatedBy");
    }
}
