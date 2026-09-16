using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema;

public sealed class MaterialPurchaseAvailabilityConfiguration
    : IEntityTypeConfiguration<MaterialPurchaseAvailability>
{
    public void Configure(EntityTypeBuilder<MaterialPurchaseAvailability> entity)
    {
        entity.ToTable("MaterialPurchaseAvailabilities", "Material", table =>
            table.HasCheckConstraint(
                "CK_MaterialPurchaseAvailabilities_ExpectedDate",
                "\"ExpectedAvailableDate\" IS NULL OR \"EffectiveFrom\" IS NULL OR \"ExpectedAvailableDate\" >= \"EffectiveFrom\""));

        entity.HasKey(x => x.MaterialPurchaseAvailabilityId)
            .HasName("PK_MaterialPurchaseAvailabilities");

        entity.Property(x => x.MaterialPurchaseAvailabilityId)
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.Status)
            .HasConversion<string>()
            .HasColumnType("citext")
            .HasMaxLength(32)
            .HasDefaultValue(MaterialPurchaseStatus.Available)
            .IsRequired();
        entity.Property(x => x.Reason).HasColumnType("text");
        entity.Property(x => x.Note).HasColumnType("text");
        entity.Property(x => x.CreatedDate).IsRequired();

        entity.HasIndex(x => x.MaterialId, "UX_MaterialPurchaseAvailabilities_MaterialId")
            .IsUnique();
        entity.HasIndex(x => x.Status, "IX_MaterialPurchaseAvailabilities_Status");
        entity.HasIndex(x => x.CreatedBy, "IX_MaterialPurchaseAvailabilities_CreatedBy");
        entity.HasIndex(x => x.UpdatedBy, "IX_MaterialPurchaseAvailabilities_UpdatedBy");

        entity.HasOne(x => x.Material)
            .WithOne(x => x.PurchaseAvailability)
            .HasForeignKey<MaterialPurchaseAvailability>(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_MaterialPurchaseAvailabilities_Material");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_MaterialPurchaseAvailabilities_CreatedBy");

        entity.HasOne(x => x.UpdatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_MaterialPurchaseAvailabilities_UpdatedBy");
    }
}
