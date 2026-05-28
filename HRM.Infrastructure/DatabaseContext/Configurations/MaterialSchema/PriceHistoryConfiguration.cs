using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema
{
    public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
    {
        public void Configure(EntityTypeBuilder<PriceHistory> entity)
        {
            entity.HasKey(e => e.PriceHistoryId).HasName("PK__PriceHis__A927CACB4B3A2EAC");
            entity.ToTable("PriceHistory", "Material");

            // Columns
            entity.Property(e => e.MaterialsSuppliersId)
                  .HasColumnName("materialsSuppliersId")
                  .IsRequired();

            entity.Property(e => e.OldPrice)
                  .HasPrecision(22, 6)
                  .HasColumnName("oldPrice");

            entity.Property(e => e.Currency)
                  .HasMaxLength(10)
                  .HasColumnName("currency");

            entity.Property(e => e.CreateDate)
                  .HasColumnName("createDate");

            entity.Property(e => e.CreatedBy)
                  .HasColumnName("createdBy");

            // Indexes
            entity.HasIndex(e => e.CreatedBy, "IX_PriceHistory_CreatedBy");
            entity.HasIndex(e => e.MaterialsSuppliersId, "IX_PriceHistory_MaterialsSuppliersId");
            entity.HasIndex(e => e.CreateDate).HasDatabaseName("ix_pricehistory_createDate");

            // (khuyến nghị) thường truy vấn theo nhà cung cấp + thời gian
            entity.HasIndex(e => new { e.MaterialsSuppliersId, e.CreateDate })
                  .HasDatabaseName("ix_pricehistory_supplier_date");

            // FKs
            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(p => p.PriceHistoryCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_PriceHistory_Materials_CreatedBy");

            entity.HasOne(d => d.MaterialsSuppliers)
                  .WithMany(p => p.PriceHistories)   // cần ICollection<PriceHistory> trong MaterialsSupplier
                  .HasForeignKey(d => d.MaterialsSuppliersId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_pricehistory_materialsSuppliersId");
        }
    }
}
