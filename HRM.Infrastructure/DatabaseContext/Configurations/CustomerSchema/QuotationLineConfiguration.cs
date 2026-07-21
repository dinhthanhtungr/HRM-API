using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

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
            entity.Property(x => x.TaxPercent).HasPrecision(8, 4);
            entity.Property(x => x.LineTotal).HasPrecision(22, 6);

            entity.Property(x => x.Note).HasColumnType("text");

            entity.HasIndex(x => new { x.QuotationId, x.SortOrder })
                .HasDatabaseName("IX_QuotationLines_Quotation_SortOrder");

            entity.HasOne(x => x.Quotation)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ProductNavigation)
                .WithMany(x => x.QuotationLines)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
