using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> entity)
    {
        entity.ToTable("WorkTasks", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkTasks_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.Title).HasColumnName("Title").HasColumnType("citext").IsRequired();
        entity.Property(x => x.Description).HasColumnName("Description").HasColumnType("text");
        entity.Property(x => x.NextAction).HasColumnName("NextAction").HasColumnType("text");
        entity.Property(x => x.Status).HasColumnName("Status").HasConversion<int>().HasDefaultValue(WorkTaskStatus.Pending);
        entity.Property(x => x.Priority).HasColumnName("Priority").HasConversion<int>().HasDefaultValue(WorkTaskPriority.Normal);
        entity.Property(x => x.DueDate).HasColumnName("DueDate");
        entity.Property(x => x.DueReminderSentAt).HasColumnName("DueReminderSentAt");
        entity.Property(x => x.CompletedDate).HasColumnName("CompletedDate");
        entity.Property(x => x.CompletedBy).HasColumnName("CompletedBy");
        entity.Property(x => x.CompletionNote).HasColumnName("CompletionNote").HasColumnType("text");
        entity.Property(x => x.AssignedToEmployeeId).HasColumnName("AssignedToEmployeeId");
        entity.Property(x => x.WorkTaskListId).HasColumnName("WorkTaskListId");
        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");
        entity.Property(x => x.UpdatedDate).HasColumnName("UpdatedDate");
        entity.Property(x => x.UpdatedBy).HasColumnName("UpdatedBy");
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

        entity.HasIndex(x => new { x.CompanyId, x.AssignedToEmployeeId, x.Status, x.DueDate })
            .HasDatabaseName("IX_WorkTasks_Assigned_Status_DueDate");
        entity.HasIndex(x => new { x.CompanyId, x.Status, x.DueDate })
            .HasDatabaseName("IX_WorkTasks_Company_Status_DueDate");
        entity.HasIndex(x => new { x.CompanyId, x.WorkTaskListId, x.Status, x.DueDate })
            .HasDatabaseName("IX_WorkTasks_List_Status_DueDate");
        entity.HasIndex(x => new { x.Status, x.DueDate })
            .HasDatabaseName("IX_WorkTasks_DueReminder")
            .HasFilter("\"IsActive\" = true AND \"DueReminderSentAt\" IS NULL");

        entity.HasOne(x => x.Company).WithMany(x => x.WorkTasks).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTasks_Company");
        entity.HasOne(x => x.CompletedByNavigation).WithMany(x => x.WorkTaskCompletedByNavigations)
            .HasForeignKey(x => x.CompletedBy).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_CompletedBy");
        entity.HasOne(x => x.AssignedToEmployee).WithMany(x => x.WorkTaskAssignedToEmployeeNavigations)
            .HasForeignKey(x => x.AssignedToEmployeeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_AssignedToEmployee");
        entity.HasOne(x => x.WorkTaskList).WithMany(x => x.WorkTasks)
            .HasForeignKey(x => x.WorkTaskListId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_WorkTaskList");
        entity.HasOne(x => x.CreatedByNavigation).WithMany(x => x.WorkTaskCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTasks_CreatedBy");
        entity.HasOne(x => x.UpdatedByNavigation).WithMany(x => x.WorkTaskUpdatedByNavigations)
            .HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_UpdatedBy");
    }
}
