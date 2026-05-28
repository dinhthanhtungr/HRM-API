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
    public class PurchaseOrderDetailConfiguration : IEntityTypeConfiguration<PurchaseOrderDetail>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrderDetail> entity)
        {
            entity.HasKey(e => e.PurchaseOrderDetailId)
                  .HasName("PK__Purchase__5026B698A94EFE60");

            entity.ToTable("PurchaseOrderDetails", "Orders");

            entity.Property(e => e.PurchaseOrderDetailId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("PurchaseOrderDetailId");

            entity.HasIndex(e => e.MaterialId, "IX_PurchaseOrderDetails_MaterialId");
            entity.HasIndex(e => e.PurchaseOrderId, "IX_PurchaseOrderDetails_PurchaseOrderId");

            entity.Property(e => e.LineNo).HasColumnName("LineNo");
            entity.Property(e => e.Note);
            entity.Property(e => e.TotalPriceAgreed).HasPrecision(22, 6);
            entity.Property(e => e.BaseCostSnapshot).HasPrecision(22, 6);
            entity.Property(e => e.UnitPriceAgreed).HasPrecision(22, 6);
            entity.Property(e => e.RealQuantity).HasPrecision(18, 2);
            entity.Property(e => e.RequestQuantity).HasPrecision(18, 2);

            entity.Property(x => x.IsActive)
                  .HasDefaultValue(true)
                  .HasColumnName("isActive");

            entity.Property(e => e.MaterialExternalIDSnapshot).HasColumnType("citext");
            entity.Property(e => e.MaterialNameSnapshot).HasColumnType("citext");

            entity.HasOne(d => d.Material)
                  .WithMany(p => p.PurchaseOrderDetails)
                  .HasForeignKey(d => d.MaterialId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_PurchaseOrderDetails_MaterialId");

            entity.HasOne(d => d.PurchaseOrder)
                  .WithMany(p => p.PurchaseOrderDetails)
                  .HasForeignKey(d => d.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_PurchaseOrderDetails_PurchaseOrderId");
        }
    }
}
