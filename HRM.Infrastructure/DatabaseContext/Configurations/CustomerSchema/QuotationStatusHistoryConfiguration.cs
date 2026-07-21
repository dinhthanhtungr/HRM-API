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
    public sealed class QuotationStatusHistoryConfiguration
        : IEntityTypeConfiguration<QuotationStatusHistory>
    {
        public void Configure(EntityTypeBuilder<QuotationStatusHistory> entity)
        {
            entity.ToTable("QuotationStatusHistories", "Customer");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.FromStatus).HasConversion<int>();
            entity.Property(x => x.ToStatus).HasConversion<int>();
            entity.Property(x => x.Note).HasColumnType("text");

            entity.HasIndex(x => new { x.QuotationId, x.ChangedDate })
                .IsDescending(false, true)
                .HasDatabaseName("IX_QuotationStatusHistories_Quotation_Date");

            entity.HasOne(x => x.Quotation)
                .WithMany(x => x.StatusHistories)
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ChangedByNavigation)
                .WithMany(x => x.QuotationStatusHistoryChangedByNavigations)
                .HasForeignKey(x => x.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
