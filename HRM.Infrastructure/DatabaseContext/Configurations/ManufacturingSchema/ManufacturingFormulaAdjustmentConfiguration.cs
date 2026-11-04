using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustmentConfiguration : IEntityTypeConfiguration<ManufacturingFormulaAdjustment>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaAdjustment> entity)
        {
            entity.ToTable("ManufacturingFormulaAdjustments", "manufacturing");

            entity.HasKey(e => e.ManufacturingFormulaAdjustmentId)
                  .HasName("PK__ManufacturingFormulaAdjustments__manufacturingFormulaAdjustmentId");

            entity.Property(e => e.ManufacturingFormulaAdjustmentId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaAdjustmentId");

            entity.Property(e => e.MfgProductionOrderId).HasColumnName("mfg_production_order_id");
            entity.Property(e => e.ManufacturingFormulaId).HasColumnName("manufacturing_formula_id");

            entity.Property(e => e.Status).HasColumnName("status").HasColumnType("citext").HasDefaultValue("Draft").IsRequired();

            entity.Property(e => e.Note).HasColumnName("note");

            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnName("created_date");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => new { e.CompanyId, e.MfgProductionOrderId, e.IsActive })
                  .HasDatabaseName("ix_mfg_formula_adjustments_company_order_active");

            entity.HasIndex(e => new { e.CompanyId, e.Status, e.IsActive })
                  .HasDatabaseName("ix_mfg_formula_adjustments_company_status_active");

            entity.HasOne(e => e.MfgProductionOrder)
                  .WithMany()
                  .HasForeignKey(e => e.MfgProductionOrderId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfa__mfgProductionOrderId");

            entity.HasOne(e => e.ManufacturingFormula)
                  .WithMany()
                  .HasForeignKey(e => e.ManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfa__manufacturingFormulaId");

            entity.HasOne(e => e.Company)
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfa__companyId");

            entity.HasOne(e => e.CreatedByNavigation)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfa__createdBy");

            entity.HasOne(e => e.UpdatedByNavigation)
                  .WithMany()
                  .HasForeignKey(e => e.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfa__updatedBy");
        }
    }
}
