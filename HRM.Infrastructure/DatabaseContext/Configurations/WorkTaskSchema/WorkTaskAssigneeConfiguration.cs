using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkTaskAssigneeConfiguration : IEntityTypeConfiguration<WorkTaskAssignee>
{
    public void Configure(EntityTypeBuilder<WorkTaskAssignee> entity)
    {
        entity.ToTable("WorkTaskAssignees", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkTaskAssignees_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.WorkTaskId).HasColumnName("WorkTaskId").IsRequired();
        entity.Property(x => x.EmployeeId).HasColumnName("EmployeeId").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");
        entity.HasIndex(x => new { x.WorkTaskId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("UX_WorkTaskAssignees_Task_Employee_Active").HasFilter("\"IsActive\" = true");
        entity.HasIndex(x => new { x.EmployeeId, x.IsActive }).HasDatabaseName("IX_WorkTaskAssignees_Employee_IsActive");
        entity.HasOne(x => x.WorkTask).WithMany(x => x.Assignees).HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskAssignees_WorkTask");
        entity.HasOne(x => x.Employee).WithMany(x => x.WorkTaskAssigneeEmployees).HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTaskAssignees_Employee");
        entity.HasOne(x => x.CreatedByNavigation).WithMany(x => x.WorkTaskAssigneeCreatedByNavigations).HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTaskAssignees_CreatedBy");
    }
}
