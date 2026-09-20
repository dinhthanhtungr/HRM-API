using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingBomStageMachineConfiguration : IEntityTypeConfiguration<ManufacturingBomStageMachine>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageMachine> entity)
    {
        entity.ToTable("manufacturing_bom_stage_machines", "bom", table =>
            table.HasCheckConstraint("ck_manufacturing_bom_stage_machines_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingBomStageMachineId).HasName("pk_manufacturing_bom_stage_machines");
        entity.Property(x => x.ManufacturingBomStageMachineId).HasColumnName("manufacturing_bom_stage_machine_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id").IsRequired();
        entity.Property(x => x.EquipmentId).HasColumnName("equipment_id").IsRequired();
        entity.Property(x => x.EquipmentExternalIdSnapshot).HasColumnName("equipment_externalid_snapshot").HasColumnType("text").IsRequired();
        entity.Property(x => x.EquipmentNameSnapshot).HasColumnName("equipment_name_snapshot").HasColumnType("text").IsRequired();
        entity.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingBomStageId, x.EquipmentId }).IsUnique().HasDatabaseName("ux_manufacturing_bom_stage_machines_stage_equipment");
        entity.HasOne(x => x.ManufacturingStage).WithMany(x => x.Machines).HasForeignKey(x => x.ManufacturingBomStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_machines_stage");
        entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_machines_equipment");
    }
}
