using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingBomStageMachineParameterConfiguration : IEntityTypeConfiguration<ManufacturingBomStageMachineParameter>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageMachineParameter> entity)
    {
        entity.ToTable("manufacturing_bom_stage_machine_parameters", "bom", table =>
        {
            table.HasCheckConstraint("ck_bom_stage_machine_parameters_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_bom_stage_machine_parameters_range", "(min_value_snapshot IS NULL OR max_value_snapshot IS NULL OR min_value_snapshot <= max_value_snapshot) AND (target_value_snapshot IS NULL OR min_value_snapshot IS NULL OR target_value_snapshot >= min_value_snapshot) AND (target_value_snapshot IS NULL OR max_value_snapshot IS NULL OR target_value_snapshot <= max_value_snapshot)");
        });
        entity.HasKey(x => x.ManufacturingBomStageMachineParameterId).HasName("pk_manufacturing_bom_stage_machine_parameters");
        entity.Property(x => x.ManufacturingBomStageMachineParameterId).HasColumnName("manufacturing_bom_stage_machine_parameter_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingBomStageMachineId).HasColumnName("manufacturing_bom_stage_machine_id").IsRequired();
        entity.Property(x => x.ParameterCodeSnapshot).HasColumnName("parameter_code_snapshot").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.ParameterNameSnapshot).HasColumnName("parameter_name_snapshot").HasMaxLength(200).IsRequired();
        entity.Property(x => x.TargetValueSnapshot).HasColumnName("target_value_snapshot").HasPrecision(18, 6);
        entity.Property(x => x.MinValueSnapshot).HasColumnName("min_value_snapshot").HasPrecision(18, 6);
        entity.Property(x => x.MaxValueSnapshot).HasColumnName("max_value_snapshot").HasPrecision(18, 6);
        entity.Property(x => x.UnitSnapshot).HasColumnName("unit_snapshot").HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.IsRequiredSnapshot).HasColumnName("is_required_snapshot").IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.NoteSnapshot).HasColumnName("note_snapshot").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingBomStageMachineId, x.ParameterCodeSnapshot }).IsUnique().HasDatabaseName("ux_bom_stage_machine_parameters_code");
        entity.HasIndex(x => new { x.ManufacturingBomStageMachineId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_bom_stage_machine_parameters_sequence");
        entity.HasOne(x => x.StageMachine).WithMany(x => x.Parameters).HasForeignKey(x => x.ManufacturingBomStageMachineId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_bom_stage_machine_parameters_machine");
    }
}
