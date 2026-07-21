using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkTaskListConfiguration : IEntityTypeConfiguration<WorkTaskList>
{
    public void Configure(EntityTypeBuilder<WorkTaskList> entity)
    {
        entity.ToTable("WorkTaskLists", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkTaskLists_Id");

        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.OwnerEmployeeId).HasColumnName("OwnerEmployeeId").IsRequired();
        entity.Property(x => x.Name).HasColumnName("Name").HasColumnType("citext").IsRequired();
        entity.Property(x => x.SortOrder).HasColumnName("SortOrder").HasDefaultValue(0);
        entity.Property(x => x.IsDefault).HasColumnName("IsDefault").HasDefaultValue(false);
        entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");
        entity.Property(x => x.UpdatedDate).HasColumnName("UpdatedDate");
        entity.Property(x => x.UpdatedBy).HasColumnName("UpdatedBy");

        entity.HasIndex(x => new { x.CompanyId, x.OwnerEmployeeId, x.IsActive, x.SortOrder })
            .HasDatabaseName("IX_WorkTaskLists_Owner_Active_SortOrder");
        entity.HasIndex(x => new { x.CompanyId, x.OwnerEmployeeId, x.Name })
            .HasDatabaseName("IX_WorkTaskLists_Owner_Name");
        entity.HasIndex(x => new { x.CompanyId, x.OwnerEmployeeId, x.IsDefault })
            .IsUnique()
            .HasDatabaseName("UX_WorkTaskLists_Default_PerOwner")
            .HasFilter("\"IsDefault\" = true AND \"IsActive\" = true");

        entity.HasOne(x => x.Company).WithMany(x => x.WorkTaskLists).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTaskLists_Company");
        entity.HasOne(x => x.OwnerEmployee).WithMany(x => x.WorkTaskListOwnerEmployeeNavigations)
            .HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTaskLists_OwnerEmployee");
        entity.HasOne(x => x.CreatedByNavigation).WithMany(x => x.WorkTaskListCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkTaskLists_CreatedBy");
        entity.HasOne(x => x.UpdatedByNavigation).WithMany(x => x.WorkTaskListUpdatedByNavigations)
            .HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTaskLists_UpdatedBy");
    }
}
