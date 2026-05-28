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
    public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrder> entity)
        {
            entity.HasKey(e => e.PurchaseOrderId)
                  .HasName("PK__Purchase__036BACA44B53DA7A");

            entity.ToTable("PurchaseOrders", "Orders");

            entity.HasIndex(e => e.CompanyId, "IX_PurchaseOrders_CompanyId");
            entity.HasIndex(e => e.CreatedBy, "IX_PurchaseOrders_CreatedBy");
            entity.HasIndex(e => e.SupplierId, "IX_PurchaseOrders_SupplierId");
            entity.HasIndex(e => e.UpdatedBy, "IX_PurchaseOrders_UpdatedBy");

            // theo code gốc: không gen tự động
            entity.Property(e => e.PurchaseOrderId)
                  .ValueGeneratedNever();

            entity.Property(e => e.ExternalId).HasMaxLength(50);
            entity.Property(e => e.OrderType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.PurchaseOrderSnapshot)
                  .WithMany()
                  .HasForeignKey(d => d.PurchaseOrderSnapshotId)
                  .HasConstraintName("FK_PurchaseOrders_PurchaseOrderSnapshot");

            entity.HasOne(d => d.Company)
                  .WithMany(p => p.PurchaseOrders)
                  .HasForeignKey(d => d.CompanyId)
                  .HasConstraintName("FK_PurchaseOrders_Company");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(p => p.PurchaseOrderCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .HasConstraintName("FK_PurchaseOrders_CreatedBy");

            entity.HasOne(d => d.Supplier)
                  .WithMany(p => p.PurchaseOrders)
                  .HasForeignKey(d => d.SupplierId)
                  .HasConstraintName("FK_PurchaseOrders_SupplierId");

            entity.HasOne(d => d.UpdatedByNavigation)
                  .WithMany(p => p.PurchaseOrderUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .HasConstraintName("FK_PurchaseOrders_UpdatedBy");
        }
    }
}
