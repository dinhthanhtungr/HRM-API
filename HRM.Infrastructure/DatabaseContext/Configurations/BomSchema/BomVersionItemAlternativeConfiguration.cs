using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class BomVersionItemAlternativeConfiguration : IEntityTypeConfiguration<BomVersionItemAlternative>
{
    public void Configure(EntityTypeBuilder<BomVersionItemAlternative> entity)
    {
        entity.ToTable("bom_version_item_alternatives", "bom", table =>
        {
            table.HasCheckConstraint("ck_bom_version_item_alternatives_ratio_positive", "replacement_ratio > 0");
            table.HasCheckConstraint("ck_bom_version_item_alternatives_priority_positive", "priority > 0");
            table.HasCheckConstraint(
                "ck_bom_version_item_alternatives_target",
                "(alternative_item_type = 'Material' AND alternative_material_id IS NOT NULL AND alternative_component_product_id IS NULL) OR " +
                "(alternative_item_type = 'Product' AND alternative_component_product_id IS NOT NULL AND alternative_material_id IS NULL)");
        });
        entity.HasKey(x => x.BomVersionItemAlternativeId).HasName("pk_bom_version_item_alternatives");

        entity.Property(x => x.BomVersionItemAlternativeId).HasColumnName("bom_version_item_alternative_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomVersionItemId).HasColumnName("bom_version_item_id").IsRequired();
        entity.Property(x => x.AlternativeItemType).HasColumnName("alternative_item_type").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.AlternativeMaterialId).HasColumnName("alternative_material_id");
        entity.Property(x => x.AlternativeComponentProductId).HasColumnName("alternative_component_product_id");
        entity.Property(x => x.MaterialReplacementId).HasColumnName("material_replacement_id");
        entity.Property(x => x.ReplacementRatio).HasColumnName("replacement_ratio").HasPrecision(18, 6).HasDefaultValue(1m).IsRequired();
        entity.Property(x => x.Priority).HasColumnName("priority").HasDefaultValue(1).IsRequired();
        entity.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.ApplicableContextSnapshot).HasColumnName("applicable_context_snapshot").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        entity.Property(x => x.TechnicalNoteSnapshot).HasColumnName("technical_note_snapshot").HasColumnType("text");
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.BomVersionItemId, x.AlternativeMaterialId })
            .IsUnique().HasFilter("alternative_material_id IS NOT NULL")
            .HasDatabaseName("ux_bom_version_item_alternatives_item_material");
        entity.HasIndex(x => new { x.BomVersionItemId, x.AlternativeComponentProductId })
            .IsUnique().HasFilter("alternative_component_product_id IS NOT NULL")
            .HasDatabaseName("ux_bom_version_item_alternatives_item_product");
        entity.HasIndex(x => x.BomVersionItemId)
            .IsUnique().HasFilter("is_default = true")
            .HasDatabaseName("ux_bom_version_item_alternatives_item_default");
        entity.HasIndex(x => new { x.BomVersionItemId, x.IsActive, x.Priority })
            .HasDatabaseName("ix_bom_version_item_alternatives_item_active_priority");
        entity.HasIndex(x => x.MaterialReplacementId).HasDatabaseName("ix_bom_version_item_alternatives_replacement");

        entity.HasOne(x => x.BomVersionItem).WithMany(x => x.Alternatives).HasForeignKey(x => x.BomVersionItemId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_item");
        entity.HasOne(x => x.AlternativeMaterial).WithMany(x => x.BomVersionItemAlternatives).HasForeignKey(x => x.AlternativeMaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_material");
        entity.HasOne(x => x.AlternativeComponentProduct).WithMany(x => x.AlternativeBomVersionItemAlternatives).HasForeignKey(x => x.AlternativeComponentProductId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_product");
        entity.HasOne(x => x.MaterialReplacement).WithMany(x => x.BomVersionItemAlternatives).HasForeignKey(x => x.MaterialReplacementId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_replacement");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_item_alternatives_updated_by");
    }
}
