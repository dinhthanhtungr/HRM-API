using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaConfiguration : IEntityTypeConfiguration<ManufacturingFormula>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormula> entity)
        {
            entity.ToTable("manufacturing_formulas", "manufacturing");

            entity.HasKey(e => e.ManufacturingFormulaId)
                  .HasName("PK__ManufacturingFormulas__manufacturingFormulaId");

            entity.Property(e => e.ManufacturingFormulaId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaId");

            entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasColumnType("citext").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("New");
            entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasPrecision(18, 2);

            entity.Property(x => x.SourceManufacturingFormulaId).HasColumnName("source_manufacturing_formula_id");
            entity.Property(x => x.SourceManufacturingExternalIdSnapshot).HasColumnName("source_manufacturing_externalid_snapshot").HasColumnType("citext");
            entity.Property(x => x.SourceVUFormulaId).HasColumnName("source_vu_formula_id");
            entity.Property(x => x.SourceBomVersionId).HasColumnName("source_bom_version_id");
            entity.Property(x => x.SourceVUExternalIdSnapshot).HasColumnName("source_vu_externalid_snapshot").HasColumnType("citext");

            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.Note).HasColumnName("note").HasColumnType("citext");

            entity.Property(x => x.CreatedDate).HasColumnName("created_date");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
            entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            entity.Property(x => x.CompanyId).HasColumnName("company_id");

            entity.HasIndex(x => new { x.CompanyId, x.ExternalId })
                  .IsUnique().HasDatabaseName("ux_mfg_formulas_company_external_id");

            entity.HasIndex(x => x.CompanyId).HasDatabaseName("ix_mfg_formulas_company_id");
            entity.HasIndex(x => x.CreatedBy).HasDatabaseName("ix_mfg_formulas_created_by");
            entity.HasIndex(x => x.SourceVUFormulaId).HasDatabaseName("ix_mfg_formulas_source_vu_formula_id");
            entity.HasIndex(x => x.SourceManufacturingFormulaId).HasDatabaseName("ix_mfg_formulas_source_mfg_formula_id");
            entity.HasIndex(x => x.SourceBomVersionId).HasDatabaseName("ix_mfg_formulas_source_bom_version_id");

            entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.CreatedDate, x.ManufacturingFormulaId })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("ix_mfg_formulas_company_active_created_desc");

            entity.HasOne(d => d.SourceVUFormula).WithMany(p => p.ManufacturingFormulaSources)
                  .HasForeignKey(d => d.SourceVUFormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mf__SourceVUFormulaId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ManufacturingFormulaCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mf__createdBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ManufacturingFormulaUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mf__updatedBy");

            entity.HasOne(d => d.Company).WithMany(p => p.ManufacturingFormulas)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mf__companyId");

            entity.HasOne(x => x.SourceManufacturingFormula).WithMany()
                  .HasForeignKey(x => x.SourceManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mf__SourceManufacturingFormulaId");

            entity.HasOne(x => x.SourceBomVersion).WithMany(x => x.ExecutionFormulas)
                  .HasForeignKey(x => x.SourceBomVersionId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_mfg_formulas_source_bom_version");
        }
    }
}
