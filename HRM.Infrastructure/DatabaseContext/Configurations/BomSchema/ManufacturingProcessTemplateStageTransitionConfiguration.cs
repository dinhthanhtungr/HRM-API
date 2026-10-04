using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateStageTransitionConfiguration : IEntityTypeConfiguration<ManufacturingProcessTemplateStageTransition>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplateStageTransition> entity)
    {
        entity.ToTable("manufacturing_process_template_stage_transitions", "bom", table =>
        {
            table.HasCheckConstraint("ck_process_template_transitions_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_process_template_transitions_distinct_stages", "from_manufacturing_process_template_stage_id <> to_manufacturing_process_template_stage_id");
            table.HasCheckConstraint("ck_process_template_transitions_event_count_positive", "default_event_count IS NULL OR default_event_count > 0");
            table.HasCheckConstraint("ck_process_template_transitions_type_valid", "transition_type BETWEEN 1 AND 5");
        });
        entity.HasKey(x => x.ManufacturingProcessTemplateStageTransitionId).HasName("pk_process_template_stage_transitions");
        entity.Property(x => x.ManufacturingProcessTemplateStageTransitionId).HasColumnName("manufacturing_process_template_stage_transition_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingProcessTemplateId).HasColumnName("manufacturing_process_template_id").IsRequired();
        entity.Property(x => x.FromManufacturingProcessTemplateStageId).HasColumnName("from_manufacturing_process_template_stage_id").IsRequired();
        entity.Property(x => x.ToManufacturingProcessTemplateStageId).HasColumnName("to_manufacturing_process_template_stage_id").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.TransitionType).HasColumnName("transition_type").HasColumnType("integer").IsRequired();
        entity.Property(x => x.DefaultEventCount).HasColumnName("default_event_count");
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.ExternalId }).IsUnique().HasDatabaseName("ux_process_template_transitions_external_id");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.Code }).IsUnique().HasDatabaseName("ux_process_template_transitions_code");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_process_template_transitions_sequence");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.FromManufacturingProcessTemplateStageId, x.ToManufacturingProcessTemplateStageId }).IsUnique().HasDatabaseName("ux_process_template_transitions_stage_pair");
        entity.HasOne(x => x.ProcessTemplate).WithMany(x => x.StageTransitions).HasForeignKey(x => x.ManufacturingProcessTemplateId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_process_template_transitions_template");
        entity.HasOne(x => x.FromStage).WithMany(x => x.OutgoingTransitions).HasForeignKey(x => x.FromManufacturingProcessTemplateStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_template_transitions_from_stage");
        entity.HasOne(x => x.ToStage).WithMany(x => x.IncomingTransitions).HasForeignKey(x => x.ToManufacturingProcessTemplateStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_template_transitions_to_stage");
    }
}
