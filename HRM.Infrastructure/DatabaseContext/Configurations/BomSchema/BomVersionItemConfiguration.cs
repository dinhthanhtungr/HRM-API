using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class BomVersionItemConfiguration : IEntityTypeConfiguration<BomVersionItem>
{
    public void Configure(EntityTypeBuilder<BomVersionItem> entity)
    {
        entity.ToTable("bom_version_items", "bom", table =>
        {
            table.HasCheckConstraint("ck_bom_version_items_line_no_positive", "line_no > 0");
            table.HasCheckConstraint("ck_bom_version_items_quantity_positive", "quantity > 0");
            table.HasCheckConstraint(
                "ck_bom_version_items_component",
                "(item_type = 'Material' AND material_id IS NOT NULL AND component_product_id IS NULL) OR " +
                "(item_type = 'Product' AND component_product_id IS NOT NULL AND material_id IS NULL)");
        });
        entity.HasKey(x => x.BomVersionItemId).HasName("pk_bom_version_items");

        entity.Property(x => x.BomVersionItemId).HasColumnName("bom_version_item_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").IsRequired();
        entity.Property(x => x.LineNo).HasColumnName("line_no").IsRequired();
        entity.Property(x => x.ItemType).HasColumnName("item_type").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.MaterialId).HasColumnName("material_id");
        entity.Property(x => x.ComponentProductId).HasColumnName("component_product_id");
        entity.Property(x => x.CategoryId).HasColumnName("category_id");
        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id");
        entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.MaterialExternalIdSnapshot).HasColumnName("material_external_id_snapshot").HasColumnType("citext").HasMaxLength(100);
        entity.Property(x => x.MaterialNameSnapshot).HasColumnName("material_name_snapshot").HasMaxLength(300);
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => new { x.BomVersionId, x.LineNo })
            .IsUnique().HasDatabaseName("ux_bom_version_items_version_line_no");
        entity.HasIndex(x => x.MaterialId).HasDatabaseName("ix_bom_version_items_material");
        entity.HasIndex(x => x.ComponentProductId).HasDatabaseName("ix_bom_version_items_component_product");
        entity.HasIndex(x => x.ManufacturingBomStageId).HasDatabaseName("ix_bom_version_items_stage");

        entity.HasOne(x => x.BomVersion).WithMany(x => x.Items).HasForeignKey(x => x.BomVersionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_items_version");
        entity.HasOne(x => x.Material).WithMany(x => x.BomVersionItems).HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_items_material");
        entity.HasOne(x => x.ComponentProduct).WithMany(x => x.ComponentBomVersionItems).HasForeignKey(x => x.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_items_component_product");
        entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_items_category");
        entity.HasOne(x => x.ManufacturingStage).WithMany(x => x.Items).HasForeignKey(x => x.ManufacturingBomStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_version_items_stage");
    }
}
