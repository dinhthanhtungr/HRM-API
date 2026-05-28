using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaVersionItemConfiguration : IEntityTypeConfiguration<ManufacturingFormulaVersionItem>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaVersionItem> entity)
        {
            entity.ToTable("ManufacturingFormulaVersionItems", "manufacturing");

            entity.HasKey(x => x.ManufacturingFormulaVersionItemId)
                  .HasName("pk_mf_version_items");

            entity.Property(x => x.ManufacturingFormulaVersionItemId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaVersionItemId");

            entity.Property(x => x.ManufacturingFormulaVersionId)
                  .HasColumnName("manufacturingFormulaVersionId")
                  .IsRequired();

            entity.HasOne(x => x.Version)
                  .WithMany(v => v.Items)
                  .HasForeignKey(x => x.ManufacturingFormulaVersionId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_mf_version_items_version");

            entity.Property(x => x.MaterialId).HasColumnName("materialId");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.itemType).HasColumnName("item_type");

            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 10);
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(22, 6);
            entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasPrecision(22, 6);
            entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("text").IsRequired();
            entity.Property(x => x.MaterialNameSnapshot).HasColumnName("materialNameSnapshot").HasColumnType("text").IsRequired();
            entity.Property(x => x.MaterialExternalIdSnapshot).HasColumnName("materialExternalIdSnapshot").HasColumnType("text").IsRequired();

            entity.HasIndex(x => x.ManufacturingFormulaVersionId).HasDatabaseName("ix_mf_version_items_version");
            entity.HasIndex(x => new { x.ManufacturingFormulaVersionId, x.MaterialId }).HasDatabaseName("ix_mfm_version_items_material");
            entity.HasIndex(x => new { x.ManufacturingFormulaVersionId, x.MaterialId }).IsUnique().HasDatabaseName("ux_mfm_version_items_version_material");

            entity.HasOne(x => x.Product).WithMany(c => c.ManufacturingFormulaVersionItems)
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfm__productId");

            entity.HasOne(x => x.Category).WithMany(c => c.Items)
                  .HasForeignKey(x => x.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfm__categoryId");

            entity.HasOne(x => x.Material).WithMany(m => m.Items)
                  .HasForeignKey(x => x.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfm__materialId");
        }
    }
}
