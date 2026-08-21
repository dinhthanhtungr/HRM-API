using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class ProductPricingVersionConfiguration
    : IEntityTypeConfiguration<ProductPricingVersion>
{
    public void Configure(EntityTypeBuilder<ProductPricingVersion> entity)
    {
        entity.ToTable("ProductPricingVersions", "Customer");
        entity.HasKey(x => x.ProductPricingVersionId)
            .HasName("PK_ProductPricingVersions");

        entity.Property(x => x.ProductPricingVersionId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.FormulaExternalIdSnapshot)
            .HasColumnType("citext")
            .HasMaxLength(100);

        entity.Property(x => x.BatchNoSnapshot)
            .HasColumnType("citext")
            .HasMaxLength(100);

        entity.Property(x => x.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("VND")
            .IsRequired();

        entity.Property(x => x.MaterialCostSnapshot).HasPrecision(22, 6);
        entity.Property(x => x.ManufacturingCost).HasPrecision(22, 6);
        entity.Property(x => x.StandardSellingPrice).HasPrecision(22, 6);
        entity.Property(x => x.ProfitMarginRate).HasPrecision(8, 4);

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(ProductPricingStatus.Draft);

        entity.Property(x => x.Version).HasDefaultValue(1);
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.HasManualTierAdjustment).HasDefaultValue(false);

        entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.Currency, x.Version })
            .IsUnique()
            .HasDatabaseName("UX_ProductPricingVersions_Company_Product_Currency_Version");

        entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.Currency, x.Status, x.IsActive })
            .HasDatabaseName("IX_ProductPricingVersions_Company_Product_Currency_Status_Active");

        entity.HasIndex(x => x.SourceFormulaId)
            .HasDatabaseName("IX_ProductPricingVersions_SourceFormulaId");

        entity.HasIndex(x => x.FormulaPricingPolicyId)
            .HasDatabaseName("IX_ProductPricingVersions_FormulaPricingPolicyId");

        entity.HasIndex(x => x.SourceManufacturingFormulaId)
            .HasDatabaseName("IX_ProductPricingVersions_SourceManufacturingFormulaId");

        entity.HasIndex(x => x.SourceSampleTrialId)
            .HasDatabaseName("IX_ProductPricingVersions_SourceSampleTrialId");

        entity.HasIndex(x => x.SourceManufacturingVUFormulaId)
            .HasDatabaseName("IX_ProductPricingVersions_SourceManufacturingVUFormulaId");

        entity.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_Company");

        entity.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_Product");

        entity.HasOne(x => x.FormulaPricingPolicy)
            .WithMany(x => x.ProductPricingVersions)
            .HasForeignKey(x => x.FormulaPricingPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_FormulaPricingPolicy");

        entity.HasOne(x => x.SourceFormula)
            .WithMany()
            .HasForeignKey(x => x.SourceFormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_SourceFormula");

        entity.HasOne(x => x.SourceManufacturingFormula)
            .WithMany()
            .HasForeignKey(x => x.SourceManufacturingFormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_SourceManufacturingFormula");

        entity.HasOne(x => x.SourceSampleTrial)
            .WithMany()
            .HasForeignKey(x => x.SourceSampleTrialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_SourceSampleTrial");

        entity.HasOne(x => x.SourceManufacturingVUFormula)
            .WithMany()
            .HasForeignKey(x => x.SourceManufacturingVUFormulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_SourceManufacturingVUFormula");

        entity.HasOne(x => x.CreatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductPricingVersions_CreatedBy");

        entity.HasOne(x => x.UpdatedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_ProductPricingVersions_UpdatedBy");

        entity.HasOne(x => x.ApprovedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_ProductPricingVersions_ApprovedBy");

        entity.HasMany(x => x.PriceTiers)
            .WithOne(x => x.ProductPricingVersion)
            .HasForeignKey(x => x.ProductPricingVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProductPricingTiers_ProductPricingVersion");
    }
}
