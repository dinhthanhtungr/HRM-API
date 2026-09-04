using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class FormulaPricingPolicyTierConfiguration : IEntityTypeConfiguration<FormulaPricingPolicyTier>
{
    public void Configure(EntityTypeBuilder<FormulaPricingPolicyTier> entity)
    {
        entity.ToTable("FormulaPricingPolicyTiers", "Customer");
        entity.HasKey(x => x.FormulaPricingPolicyTierId).HasName("PK_FormulaPricingPolicyTiers");
        entity.Property(x => x.FormulaPricingPolicyTierId).ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.QuantityRangeLabel).HasColumnType("citext")
            .HasMaxLength(50).IsRequired();
        entity.Property(x => x.MinQuantity).HasPrecision(18, 3);
        entity.Property(x => x.MaxQuantity).HasPrecision(18, 3);
        entity.Property(x => x.MinInclusive).HasDefaultValue(true);
        entity.Property(x => x.MaxInclusive).HasDefaultValue(true);
        entity.Property(x => x.PriceOffset).HasPrecision(22, 6);
        entity.Property(x => x.SortOrder).HasDefaultValue(0);
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.HasIndex(x => new { x.FormulaPricingPolicyId, x.SortOrder })
            .IsUnique().HasDatabaseName("UX_FormulaPricingPolicyTiers_Policy_SortOrder");
    }
}
