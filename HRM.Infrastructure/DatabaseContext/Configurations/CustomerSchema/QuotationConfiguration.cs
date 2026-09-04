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
    public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
    {
        public void Configure(EntityTypeBuilder<Quotation> entity)
        {
            entity.ToTable("Quotations", "Customer");
            entity.HasKey(x => x.QuotationId);

            entity.Property(x => x.QuotationId)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ExternalId)
                .HasColumnType("citext")
                .IsRequired();

            entity.Property(x => x.ContactName).HasColumnType("citext");
            entity.Property(x => x.ContactPhone)
                .HasColumnType("citext")
                .HasMaxLength(50);
            entity.Property(x => x.CustomerAddressSnapshot)
                .HasColumnType("text");

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .HasDefaultValue(QuotationStatus.Draft);

            entity.Property(x => x.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND");

            entity.Property(x => x.ExchangeRate).HasPrecision(22, 6);
            entity.Property(x => x.SubTotal).HasPrecision(22, 6);
            entity.Property(x => x.DiscountAmount).HasPrecision(22, 6);
            entity.Property(x => x.TaxPercent).HasPrecision(8, 4);
            entity.Property(x => x.TaxAmount).HasPrecision(22, 6);
            entity.Property(x => x.TotalAmount).HasPrecision(22, 6);

            entity.Property(x => x.PaymentTerms).HasColumnType("text");
            entity.Property(x => x.DeliveryTerms).HasColumnType("text");
            entity.Property(x => x.Note).HasColumnType("text");

            entity.Property(x => x.Version).HasDefaultValue(1);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.UpdatedDate).IsConcurrencyToken();
            entity.Property(x => x.UpdatedDate).IsConcurrencyToken();

            entity.HasIndex(x => new { x.CompanyId, x.ExternalId, x.Version })
                .IsUnique()
                .HasDatabaseName("UX_Quotations_Company_ExternalId_Version");

            entity.HasIndex(x => new { x.CompanyId, x.SaleEmployeeId, x.Status, x.QuotationDate })
                .IsDescending(false, false, false, true)
                .HasDatabaseName("IX_Quotations_Company_Sale_Status_Date");

            entity.HasIndex(x => new { x.CompanyId, x.CustomerId, x.QuotationDate })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_Quotations_Company_Customer_Date");

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Quotations)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Contact)
                .WithMany(x => x.Quotations)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Quotations)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SaleEmployee)
                .WithMany(x => x.QuotationSaleEmployeeNavigations)
                .HasForeignKey(x => x.SaleEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByNavigation)
                .WithMany(x => x.QuotationCreatedByNavigations)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedByNavigation)
                .WithMany(x => x.QuotationUpdatedByNavigations)
                .HasForeignKey(x => x.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.PreviousQuotation)
                .WithMany(x => x.RevisedQuotations)
                .HasForeignKey(x => x.PreviousQuotationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
