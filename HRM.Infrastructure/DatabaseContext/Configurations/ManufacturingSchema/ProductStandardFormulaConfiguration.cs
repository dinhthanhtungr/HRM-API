using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ProductStandardFormulaConfiguration : IEntityTypeConfiguration<ProductStandardFormula>
    {
        public void Configure(EntityTypeBuilder<ProductStandardFormula> entity)
        {
            entity.ToTable("product_standard_formulas", "manufacturing");

            entity.HasKey(x => x.ProductStandardFormulaId)
                  .HasName("pk_product_standard_formulas");

            entity.Property(x => x.ProductStandardFormulaId)
                  .HasColumnName("product_standard_formula_id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(x => x.ManufacturingFormulaId).HasColumnName("manufacturing_formula_id").IsRequired();
            entity.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
            entity.Property(x => x.ValidTo).HasColumnName("valid_to");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(x => x.ClosedBy).HasColumnName("closed_by");
            entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(x => x.Note).HasColumnName("note").HasColumnType("citext");

            entity.HasIndex(x => x.ProductId).HasDatabaseName("ix_psf_product");
            entity.HasIndex(x => x.ManufacturingFormulaId).HasDatabaseName("ix_psf_formula");
            entity.HasIndex(x => x.CompanyId).HasDatabaseName("ix_psf_company");
            entity.HasIndex(x => new { x.CompanyId, x.ProductId })
                  .HasDatabaseName("ux_psf_company_product_current");
            entity.HasIndex(x => new { x.ProductId, x.ValidFrom })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_psf_product_validfrom_desc");

            entity.HasOne(x => x.Product).WithMany(p => p.ProductStandardFormulas)
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psf_product");

            entity.HasOne(x => x.ManufacturingFormula).WithMany(f => f.ProductStandardFormulas)
                  .HasForeignKey(x => x.ManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psf_manufacturing_formula");

            entity.HasOne(x => x.Company).WithMany(c => c.ProductStandardFormulas)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psf_company");

            entity.HasOne(x => x.CreatedByNavigation).WithMany(e => e.ProductStandardFormulaCreatedByNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psf_created_by");

            entity.HasOne(x => x.ClosedByNavigation).WithMany(e => e.ProductStandardFormulaClosedByNavigations)
                  .HasForeignKey(x => x.ClosedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_psf_closed_by");
        }
    }
}
