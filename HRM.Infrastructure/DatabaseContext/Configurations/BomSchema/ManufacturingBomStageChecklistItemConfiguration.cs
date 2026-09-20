using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingBomStageChecklistItemConfiguration : IEntityTypeConfiguration<ManufacturingBomStageChecklistItem>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageChecklistItem> entity)
    {
        entity.ToTable("manufacturing_bom_stage_checklist_items", "bom", table => table.HasCheckConstraint("ck_bom_stage_checklist_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingBomStageChecklistItemId).HasName("pk_manufacturing_bom_stage_checklist_items");
        entity.Property(x => x.ManufacturingBomStageChecklistItemId).HasColumnName("manufacturing_bom_stage_checklist_item_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingBomStageWorkInstructionId).HasColumnName("manufacturing_bom_stage_work_instruction_id").IsRequired();
        entity.Property(x => x.SourceChecklistItemId).HasColumnName("source_checklist_item_id");
        entity.Property(x => x.ExternalIdSnapshot).HasColumnName("external_id_snapshot").HasMaxLength(64).IsRequired();
        entity.Property(x => x.ContentSnapshot).HasColumnName("content_snapshot").HasColumnType("text").IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
        entity.Property(x => x.ExpectedValueSnapshot).HasColumnName("expected_value_snapshot").HasMaxLength(200);
        entity.Property(x => x.UnitSnapshot).HasColumnName("unit_snapshot").HasMaxLength(32);
        entity.HasIndex(x => new { x.ManufacturingBomStageWorkInstructionId, x.ExternalIdSnapshot }).IsUnique().HasDatabaseName("ux_bom_stage_checklist_external_id");
        entity.HasIndex(x => new { x.ManufacturingBomStageWorkInstructionId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_bom_stage_checklist_sequence");
        entity.HasOne(x => x.WorkInstruction).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.ManufacturingBomStageWorkInstructionId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_bom_stage_checklist_work_instruction");
        entity.HasOne<ManufacturingWorkInstructionChecklistItem>().WithMany().HasForeignKey(x => x.SourceChecklistItemId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_bom_stage_checklist_source");
    }
}
