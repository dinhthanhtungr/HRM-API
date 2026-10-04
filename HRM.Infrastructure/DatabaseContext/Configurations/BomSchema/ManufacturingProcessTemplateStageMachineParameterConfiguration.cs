using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateStageMachineParameterConfiguration : IEntityTypeConfiguration<ManufacturingProcessTemplateStageMachineParameter>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplateStageMachineParameter> entity)
    {
        entity.ToTable("manufacturing_process_template_stage_machine_parameters", "bom", table =>
        {
            table.HasCheckConstraint("ck_process_template_machine_parameters_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_process_template_machine_parameters_range", "(min_value IS NULL OR max_value IS NULL OR min_value <= max_value) AND (target_value IS NULL OR min_value IS NULL OR target_value >= min_value) AND (target_value IS NULL OR max_value IS NULL OR target_value <= max_value)");
        });
        entity.HasKey(x => x.ManufacturingProcessTemplateStageMachineParameterId).HasName("pk_process_template_stage_machine_parameters");
        entity.Property(x => x.ManufacturingProcessTemplateStageMachineParameterId).HasColumnName("manufacturing_process_template_stage_machine_parameter_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingProcessTemplateStageMachineId).HasColumnName("manufacturing_process_template_stage_machine_id").IsRequired();
        entity.Property(x => x.ParameterCode).HasColumnName("parameter_code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.ParameterName).HasColumnName("parameter_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.TargetValue).HasColumnName("target_value").HasPrecision(18, 6);
        entity.Property(x => x.MinValue).HasColumnName("min_value").HasPrecision(18, 6);
        entity.Property(x => x.MaxValue).HasColumnName("max_value").HasPrecision(18, 6);
        entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.IsRequired).HasColumnName("is_required").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateStageMachineId, x.ParameterCode }).IsUnique().HasDatabaseName("ux_process_template_machine_parameters_code");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateStageMachineId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_process_template_machine_parameters_sequence");
        entity.HasOne(x => x.StageMachine).WithMany(x => x.Parameters).HasForeignKey(x => x.ManufacturingProcessTemplateStageMachineId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_process_template_machine_parameters_machine");
    }
}
