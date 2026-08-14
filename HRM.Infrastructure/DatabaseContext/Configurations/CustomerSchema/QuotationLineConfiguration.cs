using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
    {
        public void Configure(EntityTypeBuilder<QuotationLine> entity)
        {
            entity.ToTable("QuotationLines", "Customer");
            entity.HasKey(x => x.QuotationLineId);

            entity.Property(x => x.QuotationLineId)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ProductExternalIdSnapshot)
                .HasColumnType("citext")
                .IsRequired();

            entity.Property(x => x.ProductNameSnapshot)
                .HasColumnType("citext")
                .IsRequired();

            entity.Property(x => x.Unit)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Quantity).HasPrecision(22, 6);
            entity.Property(x => x.UnitPrice).HasPrecision(22, 6);
            entity.Property(x => x.DiscountPercent).HasPrecision(8, 4);
            entity.Property(x => x.LineTotal).HasPrecision(22, 6);

            entity.Property(x => x.SampleRequestId)
                .HasColumnName("SampleRequestId");

            entity.Property(x => x.ProductPricingVersionId)
                .HasColumnName("ProductPricingVersionId");

            entity.Property(x => x.PriceMode)
                .HasColumnName("PriceMode")
                .HasConversion<int>()
                .HasDefaultValue(QuotationLinePriceMode.Fixed);

            entity.Property(x => x.Note).HasColumnType("text");

            entity.HasIndex(x => new { x.QuotationId, x.SortOrder })
                .HasDatabaseName("IX_QuotationLines_Quotation_SortOrder");

            entity.HasIndex(x => x.SampleRequestId)
                .HasDatabaseName("IX_QuotationLines_SampleRequestId");

            entity.HasIndex(x => x.ProductPricingVersionId)
                .HasDatabaseName("IX_QuotationLines_ProductPricingVersionId");

            entity.HasOne(x => x.Quotation)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ProductNavigation)
                .WithMany(x => x.QuotationLines)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SampleRequest)
                .WithMany(x => x.QuotationLines)
                .HasForeignKey(x => x.SampleRequestId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_QuotationLines_SampleRequest");

            entity.HasOne(x => x.ProductPricingVersion)
                .WithMany(x => x.QuotationLines)
                .HasForeignKey(x => x.ProductPricingVersionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_QuotationLines_ProductPricingVersion");

            entity.HasMany(x => x.PriceTiers)
                .WithOne(x => x.QuotationLine)
                .HasForeignKey(x => x.QuotationLineId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QuotationLinePriceTiers_QuotationLine");
        }
    }
}
