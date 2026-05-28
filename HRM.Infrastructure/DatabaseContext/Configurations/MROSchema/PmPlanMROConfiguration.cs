using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class PmPlanMROConfiguration : IEntityTypeConfiguration<PmPlanMRO>
{
    public void Configure(EntityTypeBuilder<PmPlanMRO> entity)
    {
        entity.ToTable("pmplan", "mro");
        entity.HasKey(x => x.PlanId).HasName("pk_pmplan");

        entity.Property(x => x.PlanId).UseIdentityByDefaultColumn().HasColumnName("plan_id");
        entity.Property(x => x.PlanExternalId).HasColumnName("plan_externalid").HasColumnType("text").IsRequired();
        entity.Property(x => x.EquipmentExternalId).HasColumnName("equipment_externalid").HasColumnType("text");
        entity.Property(x => x.DepartmentExternalId).HasColumnName("department_externalid").HasColumnType("text");
        entity.Property(x => x.FactoryExternalId).HasColumnName("factory_externalid").HasColumnType("text");
        entity.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        entity.Property(x => x.IntervalVal).HasColumnName("interval_val").IsRequired();
        entity.Property(x => x.IntervalUnit).HasColumnName("interval_unit").HasColumnType("text").IsRequired();
        entity.Property(x => x.AnchorDate).HasColumnName("anchor_date").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by");

        entity.HasIndex(x => new { x.FactoryExternalId, x.PlanExternalId })
              .IsUnique()
              .HasDatabaseName("ux_pmplan_factory_plan_external");

        entity.HasIndex(x => new { x.FactoryExternalId, x.Status, x.CreatedAt, x.PlanId })
              .IsDescending(false, false, true, true)
              .HasDatabaseName("ix_pmplan_factory_status_created_desc");

        entity.HasIndex(x => x.EquipmentExternalId).HasDatabaseName("ix_pmplan_equipment_externalid");
    }
}
