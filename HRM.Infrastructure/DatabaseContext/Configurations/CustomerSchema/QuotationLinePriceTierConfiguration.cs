using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class QuotationLinePriceTierConfiguration
    : IEntityTypeConfiguration<QuotationLinePriceTier>
{
    public void Configure(EntityTypeBuilder<QuotationLinePriceTier> entity)
    {
        entity.ToTable("QuotationLinePriceTiers", "Customer");
        entity.HasKey(x => x.QuotationLinePriceTierId)
            .HasName("PK_QuotationLinePriceTiers");

        entity.Property(x => x.QuotationLinePriceTierId)
            .HasColumnName("QuotationLinePriceTierId")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.QuotationLineId)
            .HasColumnName("QuotationLineId")
            .IsRequired();

        entity.Property(x => x.QuantityRangeLabel)
            .HasColumnName("QuantityRangeLabel")
            .HasColumnType("citext")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.MinQuantity).HasColumnName("MinQuantity").HasPrecision(18, 3);
        entity.Property(x => x.MaxQuantity).HasColumnName("MaxQuantity").HasPrecision(18, 3);
        entity.Property(x => x.MinInclusive).HasColumnName("MinInclusive").HasDefaultValue(true);
        entity.Property(x => x.MaxInclusive).HasColumnName("MaxInclusive").HasDefaultValue(true);
        entity.Property(x => x.UnitPrice).HasColumnName("UnitPrice").HasPrecision(18, 6).IsRequired();
        entity.Property(x => x.SortOrder).HasColumnName("SortOrder").HasDefaultValue(0);

        entity.HasIndex(x => x.QuotationLineId)
            .HasDatabaseName("IX_QuotationLinePriceTiers_QuotationLineId");

        entity.HasIndex(x => new { x.QuotationLineId, x.SortOrder })
            .HasDatabaseName("IX_QuotationLinePriceTiers_Line_SortOrder");

        entity.HasOne(x => x.QuotationLine)
            .WithMany(x => x.PriceTiers)
            .HasForeignKey(x => x.QuotationLineId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuotationLinePriceTiers_QuotationLine");
    }
}
