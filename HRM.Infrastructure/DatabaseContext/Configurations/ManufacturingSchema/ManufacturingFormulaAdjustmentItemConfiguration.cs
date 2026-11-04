using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustmentItemConfiguration : IEntityTypeConfiguration<ManufacturingFormulaAdjustmentItem>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaAdjustmentItem> entity)
        {
            entity.ToTable("ManufacturingFormulaAdjustmentItems", "manufacturing", table =>
            {
                table.HasCheckConstraint(
                    "ck_mfg_formula_adjustment_items_one_source",
                    "(material_id IS NOT NULL AND product_id IS NULL) OR (material_id IS NULL AND product_id IS NOT NULL)");
            });

            entity.HasKey(e => e.ManufacturingFormulaAdjustmentItemId)
                  .HasName("PK__ManufacturingFormulaAdjustmentItems__manufacturingFormulaAdjustmentItemId");

            entity.Property(e => e.ManufacturingFormulaAdjustmentItemId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaAdjustmentItemId");

            entity.Property(e => e.ManufacturingFormulaAdjustmentBatchId).HasColumnName("manufacturing_formula_adjustment_batch_id");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.itemType)
                  .HasColumnName("item_type")
                  .HasConversion<int>();

            entity.Property(e => e.BaseQuantity).HasColumnName("base_quantity").HasPrecision(16, 10);
            entity.Property(e => e.AdjustedQuantity).HasColumnName("adjusted_quantity").HasPrecision(16, 10);
            entity.Property(e => e.DeltaQuantity).HasColumnName("delta_quantity").HasPrecision(16, 10);

            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.LotNo).HasColumnName("lot_no").HasColumnType("citext");

            entity.Property(e => e.MaterialNameSnapshot).HasColumnName("material_name_snapshot");
            entity.Property(e => e.MaterialExternalIdSnapshot).HasColumnName("material_externalid_snapshot");
            entity.Property(e => e.ProductNameSnapshot).HasColumnName("product_name_snapshot");
            entity.Property(e => e.ProductExternalIdSnapshot).HasColumnName("product_externalid_snapshot");

            entity.HasIndex(e => e.ManufacturingFormulaAdjustmentBatchId)
                  .HasDatabaseName("ix_mfg_formula_adjustment_items_batch_id");

            entity.HasIndex(e => new { e.ManufacturingFormulaAdjustmentBatchId, e.LineNo })
                  .HasDatabaseName("ix_mfg_formula_adjustment_items_batch_line");

            entity.HasOne(e => e.ManufacturingFormulaAdjustmentBatch)
                  .WithMany(e => e.Items)
                  .HasForeignKey(e => e.ManufacturingFormulaAdjustmentBatchId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK__Mfai__manufacturingFormulaAdjustmentBatchId");

            entity.HasOne(e => e.Material)
                  .WithMany()
                  .HasForeignKey(e => e.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfai__materialId");

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfai__productId");

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfai__categoryId");
        }
    }
}
