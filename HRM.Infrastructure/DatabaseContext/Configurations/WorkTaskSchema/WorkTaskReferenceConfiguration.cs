using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkTaskReferenceConfiguration : IEntityTypeConfiguration<WorkTaskReference>
{
    public void Configure(EntityTypeBuilder<WorkTaskReference> entity)
    {
        entity.ToTable("WorkTaskReferences", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkTaskReferences_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.WorkTaskId).HasColumnName("WorkTaskId").IsRequired();
        entity.Property(x => x.ReferenceType).HasColumnName("ReferenceType").HasConversion<int>().IsRequired();
        entity.Property(x => x.ReferenceId).HasColumnName("ReferenceId").IsRequired();
        entity.Property(x => x.ReferenceCodeSnapshot).HasColumnName("ReferenceCodeSnapshot").HasColumnType("citext");
        entity.Property(x => x.ReferenceNameSnapshot).HasColumnName("ReferenceNameSnapshot").HasColumnType("citext");
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
        entity.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("IX_WorkTaskReferences_Reference");
        entity.HasIndex(x => new { x.WorkTaskId, x.ReferenceType, x.ReferenceId }).IsUnique()
            .HasDatabaseName("UX_WorkTaskReferences_Task_Reference");
        entity.HasIndex(x => x.WorkTaskId).IsUnique().HasDatabaseName("UX_WorkTaskReferences_Task_Primary")
            .HasFilter("\"IsPrimary\" = true");
        entity.HasOne(x => x.WorkTask).WithMany(x => x.References).HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskReferences_WorkTask");
    }
}
