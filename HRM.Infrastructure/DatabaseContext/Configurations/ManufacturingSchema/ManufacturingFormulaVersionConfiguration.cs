using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaVersionConfiguration : IEntityTypeConfiguration<ManufacturingFormulaVersion>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaVersion> entity)
        {
            entity.ToTable("ManufacturingFormulaVersions", "manufacturing");

            entity.HasKey(e => e.ManufacturingFormulaVersionId)
                  .HasName("PK__ManufacturingFormulaVersions__manufacturingFormulaVersionId");

            entity.Property(e => e.ManufacturingFormulaVersionId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaVersionId");

            entity.Property(x => x.VersionNo).HasColumnName("versionNo").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("citext").HasDefaultValue("Draft");
            entity.Property(x => x.EffectiveFrom).HasColumnName("effectiveFrom");
            entity.Property(x => x.EffectiveTo).HasColumnName("effectiveTo");
            entity.Property(x => x.Note).HasColumnName("note");

            entity.HasIndex(x => new { x.ManufacturingFormulaId, x.VersionNo })
                  .IsUnique().HasDatabaseName("ux_mf_versions_formula_versionno");

            entity.HasIndex(x => x.Status).HasDatabaseName("ix_mf_versions_status");
            entity.HasIndex(x => new { x.ManufacturingFormulaId, x.EffectiveFrom, x.EffectiveTo })
                  .HasDatabaseName("ix_mf_versions_period");

            entity.HasOne(x => x.ManufacturingFormula)
                  .WithMany(f => f.ManufacturingFormulaVersions)
                  .HasForeignKey(x => x.ManufacturingFormulaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_mf_versions_formula");

            entity.HasMany(x => x.Items)
                  .WithOne(i => i.Version)
                  .HasForeignKey(i => i.ManufacturingFormulaVersionId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_mf_version_items_version");
        }
    }
}
