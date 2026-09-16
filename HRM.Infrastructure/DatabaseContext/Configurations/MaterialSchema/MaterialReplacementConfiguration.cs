using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema;

public sealed class MaterialReplacementConfiguration : IEntityTypeConfiguration<MaterialReplacement>
{
    public void Configure(EntityTypeBuilder<MaterialReplacement> entity)
    {
        entity.ToTable("MaterialReplacements", "Material", table =>
        {
            table.HasCheckConstraint(
                "CK_MaterialReplacements_DifferentMaterials",
                "\"SourceMaterialId\" <> \"ReplacementMaterialId\"");
            table.HasCheckConstraint(
                "CK_MaterialReplacements_ReplacementRatio",
                "\"ReplacementRatio\" IS NULL OR \"ReplacementRatio\" > 0");
            table.HasCheckConstraint(
                "CK_MaterialReplacements_Priority",
                "\"Priority\" > 0");
        });

        entity.HasKey(x => x.MaterialReplacementId)
            .HasName("PK_MaterialReplacements");

        entity.Property(x => x.MaterialReplacementId)
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ApplicableContext)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();
        entity.Property(x => x.TechnicalNote).HasColumnType("text");
        entity.Property(x => x.ReplacementRatio).HasPrecision(18, 6);
        entity.Property(x => x.Priority).HasDefaultValue(1).IsRequired();
        entity.Property(x => x.IsRecommended).HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        entity.Property(x => x.CreatedDate).IsRequired();

        entity.HasIndex(
                x => new { x.SourceMaterialId, x.ReplacementMaterialId },
                "UX_MaterialReplacements_Source_Replacement")
            .IsUnique();
        entity.HasIndex(x => x.ReplacementMaterialId, "IX_MaterialReplacements_ReplacementMaterialId");
        entity.HasIndex(x => x.CreatedBy, "IX_MaterialReplacements_CreatedBy");
        entity.HasIndex(x => x.UpdatedBy, "IX_MaterialReplacements_UpdatedBy");

        entity.HasOne(x => x.SourceMaterial)
            .WithMany(x => x.ReplacementOptions)
            .HasForeignKey(x => x.SourceMaterialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MaterialReplacements_SourceMaterial");

        entity.HasOne(x => x.ReplacementMaterial)
            .WithMany(x => x.ReplacementForMaterials)
            .HasForeignKey(x => x.ReplacementMaterialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MaterialReplacements_ReplacementMaterial");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_MaterialReplacements_CreatedBy");

        entity.HasOne(x => x.UpdatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_MaterialReplacements_UpdatedBy");
    }
}
