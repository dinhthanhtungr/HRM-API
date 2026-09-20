using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingBomStageWorkInstructionConfiguration : IEntityTypeConfiguration<ManufacturingBomStageWorkInstruction>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageWorkInstruction> entity)
    {
        entity.ToTable("manufacturing_bom_stage_work_instructions", "bom");
        entity.HasKey(x => x.ManufacturingBomStageWorkInstructionId).HasName("pk_manufacturing_bom_stage_work_instructions");
        entity.Property(x => x.ManufacturingBomStageWorkInstructionId).HasColumnName("manufacturing_bom_stage_work_instruction_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id").IsRequired();
        entity.Property(x => x.SourceWorkInstructionTemplateId).HasColumnName("source_work_instruction_template_id");
        entity.Property(x => x.ExternalIdSnapshot).HasColumnName("external_id_snapshot").HasMaxLength(64).IsRequired();
        entity.Property(x => x.NameSnapshot).HasColumnName("name_snapshot").HasMaxLength(200).IsRequired();
        entity.Property(x => x.VersionNoSnapshot).HasColumnName("version_no_snapshot").IsRequired();
        entity.Property(x => x.PurposeSnapshot).HasColumnName("purpose_snapshot").HasColumnType("text");
        entity.Property(x => x.PreparationSnapshot).HasColumnName("preparation_snapshot").HasColumnType("text");
        entity.Property(x => x.ProcedureSnapshot).HasColumnName("procedure_snapshot").HasColumnType("text").IsRequired();
        entity.Property(x => x.QualityRequirementsSnapshot).HasColumnName("quality_requirements_snapshot").HasColumnType("text");
        entity.Property(x => x.SafetyNotesSnapshot).HasColumnName("safety_notes_snapshot").HasColumnType("text");
        entity.Property(x => x.SnapshottedDate).HasColumnName("snapshotted_date").IsRequired();
        entity.HasIndex(x => x.ManufacturingBomStageId).IsUnique().HasDatabaseName("ux_bom_stage_work_instructions_stage");
        entity.HasOne(x => x.ManufacturingStage).WithOne(x => x.WorkInstruction).HasForeignKey<ManufacturingBomStageWorkInstruction>(x => x.ManufacturingBomStageId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_bom_stage_work_instructions_stage");
        entity.HasOne<ManufacturingWorkInstructionTemplate>().WithMany().HasForeignKey(x => x.SourceWorkInstructionTemplateId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_bom_stage_work_instructions_source");
    }
}
