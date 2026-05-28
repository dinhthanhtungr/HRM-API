using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaMaterialConfiguration : IEntityTypeConfiguration<ManufacturingFormulaMaterial>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaMaterial> entity)
        {
            entity.ToTable("ManufacturingFormulaMaterials", "manufacturing");

            entity.HasKey(e => e.ManufacturingFormulaMaterialId)
                  .HasName("PK__ManufacturingFormulaMaterials__manufacturingFormulaMaterialId");

            entity.Property(e => e.ManufacturingFormulaMaterialId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaMaterialId");

            entity.Property(x => x.ManufacturingFormulaId).HasColumnName("manufacturing_formula_id");
            entity.Property(x => x.MaterialId).HasColumnName("material_id");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.itemType).HasColumnName("item_type");

            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 10);
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(22, 6);
            entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasPrecision(22, 6);
            entity.Property(x => x.Unit).HasColumnName("unit");
            entity.Property(x => x.LotNo).HasColumnName("lot_no");
            entity.Property(x => x.MaterialNameSnapshot).HasColumnName("material_name_snapshot");
            entity.Property(x => x.MaterialExternalIdSnapshot).HasColumnName("material_externalid_snapshot");

            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

            entity.HasIndex(x => x.ManufacturingFormulaId).HasDatabaseName("ix_mfm_formula_id");
            entity.HasIndex(x => new { x.ManufacturingFormulaId, x.MaterialId }).HasDatabaseName("ix_mfm_formula_material");
            entity.HasIndex(x => new { x.ManufacturingFormulaId, x.MaterialId, x.CategoryId })
                  .HasDatabaseName("ux_mfm_formula_material_unique_active");

            entity.HasOne(x => x.ManufacturingFormula)
                  .WithMany(f => f.ManufacturingFormulaMaterials)
                  .HasForeignKey(x => x.ManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK__Mfm__manufacturingFormulaId");

            entity.HasOne(x => x.Product)
                  .WithMany(f => f.ManufacturingFormulaMaterials)
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK__Mfm__productId");

            entity.HasOne(x => x.Category)
                  .WithMany(c => c.ManufacturingFormulaMaterials)
                  .HasForeignKey(x => x.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfm__categoryId");

            entity.HasOne(x => x.Material)
                  .WithMany(m => m.ManufacturingFormulaMaterials)
                  .HasForeignKey(x => x.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfm__materialId");
        }
    }
}
