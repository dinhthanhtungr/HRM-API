using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateStageConfiguration : IEntityTypeConfiguration<ManufacturingProcessTemplateStage>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplateStage> entity)
    {
        entity.ToTable("manufacturing_process_template_stages", "bom", table => table.HasCheckConstraint("ck_process_template_stages_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingProcessTemplateStageId).HasName("pk_manufacturing_process_template_stages");
        entity.Property(x => x.ManufacturingProcessTemplateStageId).HasColumnName("manufacturing_process_template_stage_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingProcessTemplateId).HasColumnName("manufacturing_process_template_id").IsRequired();
        entity.Property(x => x.ManufacturingWorkInstructionTemplateId).HasColumnName("manufacturing_work_instruction_template_id");
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.ExternalId }).IsUnique().HasDatabaseName("ux_process_template_stages_external_id");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.Code }).IsUnique().HasDatabaseName("ux_process_template_stages_code");
        entity.HasIndex(x => new { x.ManufacturingProcessTemplateId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_process_template_stages_sequence");
        entity.HasOne(x => x.ProcessTemplate).WithMany(x => x.Stages).HasForeignKey(x => x.ManufacturingProcessTemplateId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_process_template_stages_template");
        entity.HasOne(x => x.WorkInstructionTemplate).WithMany(x => x.ProcessStages).HasForeignKey(x => x.ManufacturingWorkInstructionTemplateId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_template_stages_work_instruction");
    }
}
