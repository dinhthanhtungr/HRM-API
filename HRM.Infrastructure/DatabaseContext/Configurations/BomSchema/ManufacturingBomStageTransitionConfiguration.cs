using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingBomStageTransitionConfiguration : IEntityTypeConfiguration<ManufacturingBomStageTransition>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageTransition> entity)
    {
        entity.ToTable("manufacturing_bom_stage_transitions", "bom", table =>
        {
            table.HasCheckConstraint("ck_manufacturing_bom_stage_transitions_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_manufacturing_bom_stage_transitions_distinct_stages", "from_manufacturing_bom_stage_id <> to_manufacturing_bom_stage_id");
            table.HasCheckConstraint("ck_manufacturing_bom_stage_transitions_type_valid", "transition_type BETWEEN 1 AND 5");
        });
        entity.HasKey(x => x.ManufacturingBomStageTransitionId).HasName("pk_manufacturing_bom_stage_transitions");
        entity.Property(x => x.ManufacturingBomStageTransitionId).HasColumnName("manufacturing_bom_stage_transition_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").IsRequired();
        entity.Property(x => x.FromManufacturingBomStageId).HasColumnName("from_manufacturing_bom_stage_id").IsRequired();
        entity.Property(x => x.ToManufacturingBomStageId).HasColumnName("to_manufacturing_bom_stage_id").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.TransitionType)
            .HasColumnName("transition_type")
            .HasColumnType("integer")
            .IsRequired();
        entity.Property(x => x.DefaultEventCount).HasColumnName("default_event_count");
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.BomVersionId, x.ExternalId }).IsUnique().HasDatabaseName("ux_manufacturing_bom_stage_transitions_version_code");
        entity.HasIndex(x => new { x.BomVersionId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_manufacturing_bom_stage_transitions_version_sequence");
        entity.HasOne(x => x.BomVersion).WithMany(x => x.ManufacturingStageTransitions).HasForeignKey(x => x.BomVersionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_transitions_version");
        entity.HasOne(x => x.FromStage).WithMany(x => x.OutgoingTransitions).HasForeignKey(x => x.FromManufacturingBomStageId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_transitions_from_stage");
        entity.HasOne(x => x.ToStage).WithMany(x => x.IncomingTransitions).HasForeignKey(x => x.ToManufacturingBomStageId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_transitions_to_stage");
    }
}
