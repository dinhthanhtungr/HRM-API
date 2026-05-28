using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema
{
    public class MerchandiseOrderDetailConfiguration : IEntityTypeConfiguration<MerchandiseOrderDetail>
    {
        public void Configure(EntityTypeBuilder<MerchandiseOrderDetail> entity)
        {
            entity.ToTable("MerchandiseOrderDetails", "Orders");

            // Key
            entity.HasKey(e => e.MerchandiseOrderDetailId)
                  .HasName("PK__Merchand__FE0FB3FF67BDE750");

            // Columns
            entity.Property(e => e.MerchandiseOrderDetailId)
                  .HasColumnName("MerchandiseOrderDetailId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.MerchandiseOrderId).HasColumnName("MerchandiseOrderId");
            entity.Property(e => e.ProductId).HasColumnName("ProductId");
            entity.Property(e => e.FormulaId).HasColumnName("FormulaId");

            entity.Property(e => e.ProductExternalIdSnapshot)
                  .HasColumnName("ProductExternalIdSnapshot")
                  .HasColumnType("citext");

            entity.Property(e => e.ProductNameSnapshot)
                  .HasColumnName("ProductNameSnapshot")
                  .HasColumnType("citext");

            entity.Property(e => e.FormulaExternalIdSnapshot)
                  .HasColumnName("FormulaExternalIdSnapshot")
                  .HasColumnType("citext");

            entity.Property(e => e.ExpectedQuantity)
                  .HasColumnName("ExpectedQuantity")
                  .HasPrecision(18, 3);

            entity.Property(e => e.RealQuantity)
                  .HasColumnName("RealQuantity")
                  .HasPrecision(18, 3);

            entity.Property(e => e.BagType)
                  .HasColumnName("BagType")
                  .HasMaxLength(50);

            entity.Property(e => e.PackageWeight)
                  .HasColumnName("PackageWeight")
                  .HasMaxLength(50);

            entity.Property(e => e.Status)
                  .HasColumnName("Status")
                  .HasMaxLength(50)
                  .HasDefaultValue("New");

            entity.Property(e => e.Comment)
                  .HasColumnName("Comment")
                  .HasColumnType("citext");

            entity.Property(e => e.DeliveryRequestDate).HasColumnName("DeliveryRequestDate");
            entity.Property(e => e.DeliveryActualDate).HasColumnName("DeliveryActualDate");
            entity.Property(e => e.ExpectedDeliveryDate).HasColumnName("ExpectedDeliveryDate");

            entity.Property(e => e.BaseCostSnapshot)
                  .HasColumnName("BaseCostSnapshot")
                  .HasPrecision(22, 6);

            entity.Property(e => e.RecommendedUnitPrice)
                  .HasColumnName("RecommendedUnitPrice")
                  .HasPrecision(22, 6);

            entity.Property(e => e.UnitPriceAgreed)
                  .HasColumnName("UnitPriceAgreed")
                  .HasPrecision(22, 6);

            entity.Property(e => e.TotalPriceAgreed)
                  .HasColumnName("TotalPriceAgreed")
                  .HasPrecision(22, 6);

            entity.Property(e => e.IsActive)
                  .HasColumnName("IsActive")
                  .HasDefaultValue(true);

            // Indexes
            entity.HasIndex(e => e.MerchandiseOrderId)
                  .HasDatabaseName("IX_MO_Details_OrderId");

            entity.HasIndex(e => new { e.MerchandiseOrderId, e.ProductId })
                  .HasDatabaseName("IX_MO_Details_Order_Product");

            entity.HasIndex(e => new { e.MerchandiseOrderId, e.FormulaId })
                  .HasDatabaseName("IX_MO_Details_Order_Formula");

            // (EF Core 8) sort index: ExpectedDeliveryDate DESC, MerchandiseOrderDetailId DESC
            entity.HasIndex(e => new { e.MerchandiseOrderId, e.IsActive, e.Status, e.ExpectedDeliveryDate, e.MerchandiseOrderDetailId })
                  .IsDescending(false, false, false, true, true)
                  .HasDatabaseName("IX_MO_Details_Order_Status_DateDesc");

            // Relationships
            entity.HasOne(d => d.MerchandiseOrder)
                  .WithMany(p => p.MerchandiseOrderDetails)
                  .HasForeignKey(d => d.MerchandiseOrderId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_MerchandiseOrderDetails_MerchandiseOrderId");

            entity.HasOne(d => d.Product)
                  .WithMany(p => p.MerchandiseOrderDetails)
                  .HasForeignKey(d => d.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_MerchandiseOrderDetails_Product");

            entity.HasOne(d => d.Formula)
                  .WithMany(p => p.MerchandiseOrderDetails)
                  .HasForeignKey(d => d.FormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_MerchandiseOrderDetails_FormulaId");

            // // Optional unique active-line rule:
            // entity.HasIndex(e => new { e.MerchandiseOrderId, e.ProductId, e.FormulaId })
            //       .IsUnique()
            //       .HasFilter("\"IsActive\" = TRUE")
            //       .HasDatabaseName("UX_MO_Detail_Order_Product_Formula_Active");
        }
    }
}
