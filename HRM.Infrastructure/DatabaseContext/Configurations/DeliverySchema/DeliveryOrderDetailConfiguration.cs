using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryOrderDetailConfiguration : IEntityTypeConfiguration<DeliveryOrderDetail>
    {
        public void Configure(EntityTypeBuilder<DeliveryOrderDetail> entity)
        {
            entity.ToTable("DeliveryOrderDetail", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK__DeliveryOrderDetail__A2F6B5D8D1C1E3E3");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.HasIndex(x => x.DeliveryOrderId, "IX_DeliveryOrderDetail_DeliveryOrderId");
            entity.HasIndex(x => x.ProductId, "IX_DeliveryOrderDetail_ProductId");
            entity.HasIndex(x => x.MerchandiseOrderDetailId, "IX_DeliveryOrderDetail_MerchandiseOrderDetailId");

            entity.Property(x => x.ProductExternalIdSnapShot)
                  .HasColumnType("citext");

            entity.Property(x => x.ProductNameSnapShot).HasColumnType("citext");

            entity.Property(x => x.LotNoList)
                  .HasColumnType("citext");

            entity.Property(x => x.PONo)
                  .HasColumnType("citext");

            entity.Property(x => x.Quantity)
                  .HasPrecision(18, 3);

            entity.Property(x => x.NumOfBags);

            entity.Property(x => x.IsActive)
                  .HasDefaultValue(true);

            entity.Property(x => x.IsAttach)
                  .HasDefaultValue(false);

            // Detail thuộc 1 DeliveryOrder
            entity.HasOne(d => d.DeliveryOrder)
                  .WithMany(o => o.Details)
                  .HasForeignKey(d => d.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryOrderDetail_DeliveryOrder");

            // OPTIONAL: link tới MerchandiseOrderDetail
            entity.HasOne(d => d.MerchandiseOrderDetail)
                  .WithMany(m => m.DeliveryOrderDetails)
                  .HasForeignKey(d => d.MerchandiseOrderDetailId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryOrderDetail_MerchandiseOrderDetail");

            // OPTIONAL: Product
            entity.HasOne(d => d.Product)
                  .WithMany(p => p.DeliveryOrderDetails)
                  .HasForeignKey(d => d.ProductId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryOrderDetail_Product");
        }
    }
}
