using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ProductionSelectVersionConfiguration : IEntityTypeConfiguration<ProductionSelectVersion>
    {
        public void Configure(EntityTypeBuilder<ProductionSelectVersion> entity)
        {
            entity.ToTable("production_select_versions", "manufacturing");

            entity.HasKey(x => x.ProductionSelectVersionId)
                  .HasName("pk_production_select_versions");

            entity.Property(x => x.ProductionSelectVersionId)
                  .HasColumnName("production_select_version_id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.MfgProductionOrderId).HasColumnName("mfg_production_order_id").IsRequired();
            entity.Property(x => x.ManufacturingFormulaId).HasColumnName("manufacturing_formula_id").IsRequired();


            //entity.Property(x => x.IsSelected)
            //      .HasColumnName("is_selected")
            //      .HasDefaultValue(false)
            //      .IsRequired();



            //entity.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
            entity.Property(x => x.ValidFrom).HasColumnName("valid_from");
            entity.Property(x => x.ValidTo).HasColumnName("valid_to");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(x => x.ClosedBy).HasColumnName("closed_by");
            entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

            entity.HasOne(x => x.MfgProductionOrder).WithMany(p => p.ProductionSelectVersions)
                  .HasForeignKey(x => x.MfgProductionOrderId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psv_mfg_production_order");

            entity.HasOne(x => x.ManufacturingFormula).WithMany(p => p.ProductionSelectVersions)
                  .HasForeignKey(x => x.ManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psv_manufacturing_formula");

            entity.HasOne(x => x.Company).WithMany(p => p.ProductionSelectVersions)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psv_company");

            entity.HasOne(x => x.CreatedByNavigation).WithMany(p => p.ProductionSelectVersionCreatedByNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_psv_created_by");

            entity.HasOne(x => x.ClosedByNavigation).WithMany(p => p.ProductionSelectVersionClosedByNavigations)
                  .HasForeignKey(x => x.ClosedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_psv_closed_by");

            entity.HasIndex(x => x.MfgProductionOrderId)
                  .IsUnique()
                  .HasFilter("valid_to IS NULL AND valid_from IS NOT NULL")
                  .HasDatabaseName("ux_psv_one_active_per_mpo");

            entity.HasIndex(x => x.MfgProductionOrderId)
                  .IsUnique()
                  .HasFilter("valid_to IS NULL AND valid_from IS NOT NULL")
                  .HasDatabaseName("ux_psv_one_active_per_mpo");


            entity.HasIndex(x => x.ManufacturingFormulaId).HasDatabaseName("ix_psv_formula");
            entity.HasIndex(x => new { x.MfgProductionOrderId, x.ValidFrom })
                  .IsDescending(false, true).HasDatabaseName("ix_psv_mpo_validfrom_desc");
            entity.HasIndex(x => new { x.CompanyId, x.MfgProductionOrderId })
                  .HasDatabaseName("ux_psv_current_per_order");
        }
    }
}
