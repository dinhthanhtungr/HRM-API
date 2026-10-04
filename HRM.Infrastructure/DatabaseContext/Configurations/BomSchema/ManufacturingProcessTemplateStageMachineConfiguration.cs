using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateStageMachineConfiguration : IEntityTypeConfiguration<ManufacturingProcessTemplateStageMachine>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplateStageMachine> entity)
    {
        entity.ToTable("manufacturing_process_template_stage_machines", "bom", table =>
        {
            table.HasCheckConstraint("ck_process_template_stage_machines_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_process_template_stage_machines_group_name_requires_key", "configuration_group_name IS NULL OR configuration_group_key IS NOT NULL");
        });
        entity.HasKey(x => x.ManufacturingProcessTemplateStageMachineId).HasName("pk_manufacturing_process_template_stage_machines");
        entity.Property(x => x.ManufacturingProcessTemplateStageMachineId).HasColumnName("manufacturing_process_template_stage_machine_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingProcessTemplateStageId).HasColumnName("manufacturing_process_template_stage_id").IsRequired();
        entity.Property(x => x.EquipmentId).HasColumnName("equipment_id").IsRequired();
        entity.Property(x => x.ConfigurationGroupKey).HasColumnName("configuration_group_key");
        entity.Property(x => x.ConfigurationGroupName).HasColumnName("configuration_group_name").HasMaxLength(200);
        entity.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateStageId, x.EquipmentId }).IsUnique().HasDatabaseName("ux_process_template_stage_machines_equipment");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateStageId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_process_template_stage_machines_sequence");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateStageId, x.ConfigurationGroupKey }).HasDatabaseName("ix_process_template_stage_machines_configuration_group");
        entity.HasOne(x => x.Stage).WithMany(x => x.Machines).HasForeignKey(x => x.ManufacturingProcessTemplateStageId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_process_template_stage_machines_stage");
        entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_template_stage_machines_equipment");
    }
}
