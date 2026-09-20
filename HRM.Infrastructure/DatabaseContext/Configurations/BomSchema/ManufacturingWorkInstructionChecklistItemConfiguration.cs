using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingWorkInstructionChecklistItemConfiguration : IEntityTypeConfiguration<ManufacturingWorkInstructionChecklistItem>
{
    public void Configure(EntityTypeBuilder<ManufacturingWorkInstructionChecklistItem> entity)
    {
        entity.ToTable("manufacturing_work_instruction_checklist_items", "bom", table => table.HasCheckConstraint("ck_work_instruction_checklist_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingWorkInstructionChecklistItemId).HasName("pk_manufacturing_work_instruction_checklist_items");
        entity.Property(x => x.ManufacturingWorkInstructionChecklistItemId).HasColumnName("manufacturing_work_instruction_checklist_item_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingWorkInstructionTemplateId).HasColumnName("manufacturing_work_instruction_template_id").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
        entity.Property(x => x.ExpectedValue).HasColumnName("expected_value").HasMaxLength(200);
        entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(32);
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.HasIndex(x => new { x.ManufacturingWorkInstructionTemplateId, x.ExternalId }).IsUnique().HasDatabaseName("ux_work_instruction_checklist_external_id");
        entity.HasIndex(x => new { x.ManufacturingWorkInstructionTemplateId, x.SequenceNo }).IsUnique().HasDatabaseName("ux_work_instruction_checklist_sequence");
        entity.HasOne(x => x.WorkInstructionTemplate).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.ManufacturingWorkInstructionTemplateId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_work_instruction_checklist_template");
    }
}
