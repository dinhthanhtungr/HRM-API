using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class ProductPricingTierConfiguration
    : IEntityTypeConfiguration<ProductPricingTier>
{
    public void Configure(EntityTypeBuilder<ProductPricingTier> entity)
    {
        entity.ToTable("ProductPricingTiers", "Customer");
        entity.HasKey(x => x.ProductPricingTierId)
            .HasName("PK_ProductPricingTiers");

        entity.Property(x => x.ProductPricingTierId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.QuantityRangeLabel)
            .HasColumnType("citext")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.MinQuantity).HasPrecision(18, 3);
        entity.Property(x => x.MaxQuantity).HasPrecision(18, 3);
        entity.Property(x => x.MinInclusive).HasDefaultValue(true);
        entity.Property(x => x.MaxInclusive).HasDefaultValue(true);
        entity.Property(x => x.UnitPrice).HasPrecision(22, 6);
        entity.Property(x => x.SortOrder).HasDefaultValue(0);

        entity.HasIndex(x => new { x.ProductPricingVersionId, x.SortOrder })
            .IsUnique()
            .HasDatabaseName("UX_ProductPricingTiers_Version_SortOrder");

        entity.HasOne(x => x.ProductPricingVersion)
            .WithMany(x => x.PriceTiers)
            .HasForeignKey(x => x.ProductPricingVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProductPricingTiers_ProductPricingVersion");
    }
}
