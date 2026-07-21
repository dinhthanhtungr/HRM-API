using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WorkTaskSchema;

public sealed class WorkPlanReferenceConfiguration : IEntityTypeConfiguration<WorkPlanReference>
{
    public void Configure(EntityTypeBuilder<WorkPlanReference> entity)
    {
        entity.ToTable("WorkPlanReferences", "Work");
        entity.HasKey(x => x.Id).HasName("PK_WorkPlanReferences_Id");
        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.WorkPlanId).HasColumnName("WorkPlanId").IsRequired();
        entity.Property(x => x.ReferenceType).HasColumnName("ReferenceType").HasConversion<int>().IsRequired();
        entity.Property(x => x.ReferenceId).HasColumnName("ReferenceId").IsRequired();
        entity.Property(x => x.ReferenceCodeSnapshot).HasColumnName("ReferenceCodeSnapshot").HasColumnType("citext");
        entity.Property(x => x.ReferenceNameSnapshot).HasColumnName("ReferenceNameSnapshot").HasColumnType("citext");
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
        entity.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("IX_WorkPlanReferences_Reference");
        entity.HasIndex(x => new { x.WorkPlanId, x.ReferenceType, x.ReferenceId }).IsUnique()
            .HasDatabaseName("UX_WorkPlanReferences_Plan_Reference");
        entity.HasIndex(x => x.WorkPlanId).IsUnique().HasDatabaseName("UX_WorkPlanReferences_Plan_Primary")
            .HasFilter("\"IsPrimary\" = true");
        entity.HasOne(x => x.WorkPlan).WithMany(x => x.References).HasForeignKey(x => x.WorkPlanId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkPlanReferences_WorkPlan");
    }
}
